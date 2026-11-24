using System;
using System.Collections.Generic;
using System.IO;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using BepInEx.AssemblyPublicizer;

namespace SdvKb.Build
{
    /// <summary>
    /// Publicizes and strips game assemblies into cached reference DLLs using the open-source
    /// `BepInEx.AssemblyPublicizer` + `AsmResolver.DotNet` libraries, while preserving `protected virtual`
    /// (`Family` / `FamilyOrAssembly`) modifiers so user mods with `protected override` compile with zero CS0507 errors!
    /// Paired with `[assembly: IgnoresAccessChecksTo(...)]` so .NET 6 CoreCLR JIT skips runtime visibility checks.
    /// </summary>
    public static class PublicizedAssemblyCache
    {
        private static readonly string[] AssembliesToPublicize =
        {
            "Stardew Valley",
            "StardewValley.GameData",
            "xTile"
        };

        private static readonly object CacheLock = new object();

        public static IReadOnlyList<string> PublicizedAssemblyNames => AssembliesToPublicize;

        public static void ApplyPublicizedReferences(string gamePath, IDictionary<string, string> refsBySimpleName)
        {
            lock (CacheLock)
            {
                string cacheRoot = Path.Combine(KbConfig.DataDir, "publicized-refs-v3");
                Directory.CreateDirectory(cacheRoot);

                var options = new AssemblyPublicizerOptions
                {
                    Target = PublicizeTarget.All,
                    Strip = true,
                    IncludeOriginalAttributesAttribute = false
                };

                foreach (var simpleName in AssembliesToPublicize)
                {
                    if (!refsBySimpleName.TryGetValue(simpleName, out string? origPath) ||
                        string.IsNullOrWhiteSpace(origPath) ||
                        !File.Exists(origPath))
                    {
                        continue;
                    }

                    try
                    {
                        var fi = new FileInfo(origPath);
                        string stamp = $"{fi.Length}_{fi.LastWriteTimeUtc.Ticks}";
                        string outFile = Path.Combine(cacheRoot, $"{simpleName}.{stamp}.pub.dll");

                        if (!File.Exists(outFile))
                        {
                            foreach (var old in Directory.GetFiles(cacheRoot, $"{simpleName}.*.pub.dll"))
                            {
                                try { File.Delete(old); } catch { }
                            }

                            PublicizePreservingProtectedVirtuals(origPath, outFile, options);
                        }

                        if (File.Exists(outFile))
                        {
                            refsBySimpleName[simpleName] = outFile;
                        }
                    }
                    catch
                    {
                        // Gracefully fall back to original DLL if publicizing fails
                    }
                }
            }
        }

        private static void PublicizePreservingProtectedVirtuals(
            string origPath,
            string outFile,
            AssemblyPublicizerOptions options)
        {
            var asm = AssemblyDefinition.FromFile(origPath);
            var preservedAccess = new Dictionary<MethodDefinition, MethodAttributes>();

            if (asm.ManifestModule != null)
            {
                foreach (var type in asm.ManifestModule.GetAllTypes())
                {
                    foreach (var m in type.Methods)
                    {
                        if (m.IsVirtual && (m.IsFamily || m.IsFamilyOrAssembly))
                        {
                            preservedAccess[m] = m.Attributes & MethodAttributes.MemberAccessMask;
                        }
                    }

                    foreach (var prop in type.Properties)
                    {
                        bool getPreserved = prop.GetMethod != null && preservedAccess.ContainsKey(prop.GetMethod);
                        bool setPreserved = prop.SetMethod != null && preservedAccess.ContainsKey(prop.SetMethod);
                        if (getPreserved || setPreserved)
                        {
                            if (prop.GetMethod != null && !preservedAccess.ContainsKey(prop.GetMethod))
                                preservedAccess[prop.GetMethod] = prop.GetMethod.Attributes & MethodAttributes.MemberAccessMask;
                            if (prop.SetMethod != null && !preservedAccess.ContainsKey(prop.SetMethod))
                                preservedAccess[prop.SetMethod] = prop.SetMethod.Attributes & MethodAttributes.MemberAccessMask;
                        }
                    }
                }
            }

            AssemblyPublicizer.Publicize(asm, options);

            foreach (var kv in preservedAccess)
            {
                kv.Key.Attributes = (kv.Key.Attributes & ~MethodAttributes.MemberAccessMask) | kv.Value;
            }

            asm.Write(outFile);
        }
    }
}
