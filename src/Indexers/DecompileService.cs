using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler.TypeSystem;
using SdvKb.Database;

namespace SdvKb.Indexers
{
    public static class DecompileService
    {
        private sealed class CachedDecompilerEntry
        {
            public string DllPath { get; }
            public DateTime LastWriteUtc { get; }
            public AssemblyApiIndexer.DecompilerHandle Handle { get; }

            public CachedDecompilerEntry(string dllPath, DateTime lastWriteUtc, AssemblyApiIndexer.DecompilerHandle handle)
            {
                DllPath = dllPath;
                LastWriteUtc = lastWriteUtc;
                Handle = handle;
            }
        }

        // 覆盖星露谷本体、SMAPI 本体与内部 Toolkit、GameData、MonoGame 渲染底层、xTile 地图渲染与 0Harmony
        private static readonly string[] SearchAssemblies =
        {
            "Stardew Valley.dll",
            "StardewModdingAPI.dll",
            "StardewValley.GameData.dll",
            "MonoGame.Framework.dll",
            "xTile.dll",
            Path.Combine("smapi-internal", "0Harmony.dll"),
            Path.Combine("smapi-internal", "SMAPI.Toolkit.dll")
        };

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, CachedDecompilerEntry> MemoryCache =
            new Dictionary<string, CachedDecompilerEntry>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                foreach (var kv in MemoryCache)
                {
                    try { kv.Value.Handle.Dispose(); } catch { }
                }
                MemoryCache.Clear();
            }
        }

        public static void PrewarmMainAssemblies(string gamePath)
        {
            foreach (var dllName in SearchAssemblies)
            {
                string dllPath = Path.Combine(gamePath, dllName);
                if (File.Exists(dllPath))
                {
                    var handle = GetOrOpenCached(gamePath, dllPath);
                    // Pre-JIT ILSpy's AST & C# output pipeline once in background so even the 1st user `decompile` is <60ms!
                    if (dllName == "Stardew Valley.dll")
                    {
                        var tinyMethod = handle.TypeSystem.MainModule.TypeDefinitions
                            .SelectMany(t => t.Methods)
                            .FirstOrDefault(m => !m.IsConstructor && m.HasBody);
                        if (tinyMethod != null)
                        {
                            try { _ = SafeDecompileEntity(handle, tinyMethod.MetadataToken); } catch { }
                        }
                    }
                }
            }
        }

        private static AssemblyApiIndexer.DecompilerHandle GetOrOpenCached(string gamePath, string dllPath)
        {
            string fullPath = Path.GetFullPath(dllPath);
            DateTime writeUtc = File.GetLastWriteTimeUtc(fullPath);

            lock (CacheLock)
            {
                if (MemoryCache.TryGetValue(fullPath, out var existing))
                {
                    if (existing.LastWriteUtc == writeUtc)
                        return existing.Handle;

                    try { existing.Handle.Dispose(); } catch { }
                    MemoryCache.Remove(fullPath);
                }

                var fresh = AssemblyApiIndexer.OpenDecompiler(gamePath, fullPath);
                MemoryCache[fullPath] = new CachedDecompilerEntry(fullPath, writeUtc, fresh);
                return fresh;
            }
        }

        /// <summary>
        /// 反编译指定类型或成员源码。
        /// 【当初为什么要加 `grepPattern` / `grepContextLines`】：
        /// 像 `Game1.DrawWorld`、`GameLocation.draw`、`SModHooks` 这类核心大方法/大类动辄几百上千行，默认 200 行会被截断，
        /// 开 `--full` 又会一次性吐出几千行代码撑爆 AI 上下文。支持 `--grep <关键词> -C 15` 后，会在内部完整反编译后
        /// 仅提取命中关键词前后的带行号代码片段，定位内部逻辑既准又省 Token。
        /// </summary>
        public static string DecompileTarget(
            string gamePath,
            string targetQuery,
            int maxLines = 220,
            bool fullType = false,
            KbDatabase? db = null,
            string? grepPattern = null,
            int grepContextLines = 15)
        {
            if (string.IsNullOrWhiteSpace(targetQuery))
                throw new ArgumentException("Please specify a type or Type.Member to decompile (e.g. ShopMenu.tryToPurchaseItem).");

            string query = targetQuery.Trim();
            bool hasGrep = !string.IsNullOrWhiteSpace(grepPattern);
            bool effectiveFull = fullType || hasGrep;
            int effectiveMaxLines = effectiveFull ? int.MaxValue : maxLines;

            // 1. O(1) Fast Path via SQLite MetadataToken index!
            if (db != null)
            {
                var tokenMatches = db.LookupDecompileTargets(query);
                if (tokenMatches.Count > 0)
                {
                    // Check if bare method matched multiple unrelated types
                    var distinctTypes = tokenMatches.Select(t => t.FullTypeName).Distinct(StringComparer.Ordinal).ToList();
                    if (distinctTypes.Count > 1 && !query.Contains('.'))
                    {
                        var ambSb = new StringBuilder();
                        ambSb.AppendLine($"Multiple types contain method `{query}`. Specify `Type.Method`:");
                        foreach (var item in tokenMatches)
                            ambSb.AppendLine($"  - {item.FullTypeName}.{item.MemberName}  ({item.Signature})");
                        return ambSb.ToString();
                    }

                    string? fastOutput = DecompileFromTokenRecords(gamePath, tokenMatches, effectiveMaxLines, effectiveFull);
                    if (fastOutput != null)
                        return hasGrep ? ApplyGrepFilter(fastOutput, grepPattern!, grepContextLines) : fastOutput;
                }
            }

            // 2. Fallback: Direct Type / Type.Member scan using in-memory cached TypeSystem
            foreach (var relDll in SearchAssemblies)
            {
                string dllPath = Path.Combine(gamePath, relDll);
                if (!File.Exists(dllPath))
                    continue;

                string dllName = Path.GetFileName(relDll);
                var handle = GetOrOpenCached(gamePath, dllPath);
                var mainMod = handle.TypeSystem.MainModule;

                var exactTypes = FindMatchingTypes(mainMod, query);
                if (exactTypes.Count > 0)
                {
                    string raw = DecompileTypeDefinition(dllName, handle, exactTypes[0], effectiveMaxLines, effectiveFull);
                    return hasGrep ? ApplyGrepFilter(raw, grepPattern!, grepContextLines) : raw;
                }

                int lastDot = query.LastIndexOf('.');
                if (lastDot > 0 && lastDot < query.Length - 1)
                {
                    string typePart = query.Substring(0, lastDot);
                    string memberPart = query.Substring(lastDot + 1);

                    var candidateTypes = FindMatchingTypes(mainMod, typePart);
                    foreach (var t in candidateTypes)
                    {
                        var methods = t.Methods.Where(m =>
                            m.Name.Equals(memberPart, StringComparison.OrdinalIgnoreCase) ||
                            (m.IsConstructor && (memberPart.Equals(".ctor", StringComparison.OrdinalIgnoreCase) || memberPart.Equals(t.Name, StringComparison.OrdinalIgnoreCase))))
                            .ToList();

                        if (methods.Count > 0)
                        {
                            string raw = FormatMethodsDecompilation(dllName, handle, t, methods, effectiveMaxLines, effectiveFull);
                            return hasGrep ? ApplyGrepFilter(raw, grepPattern!, grepContextLines) : raw;
                        }

                        var prop = t.Properties.FirstOrDefault(p => p.Name.Equals(memberPart, StringComparison.OrdinalIgnoreCase));
                        if (prop != null)
                        {
                            string pCode = SafeDecompileEntity(handle, prop.MetadataToken);
                            string raw = $"// Assembly: {dllName}\n// Property: {t.FullName}.{prop.Name}\n\n{pCode}";
                            return hasGrep ? ApplyGrepFilter(raw, grepPattern!, grepContextLines) : raw;
                        }

                        var field = t.Fields.FirstOrDefault(f => f.Name.Equals(memberPart, StringComparison.OrdinalIgnoreCase));
                        if (field != null)
                        {
                            string fCode = SafeDecompileEntity(handle, field.MetadataToken);
                            string raw = $"// Assembly: {dllName}\n// Field: {t.FullName}.{field.Name}\n\n{fCode}";
                            return hasGrep ? ApplyGrepFilter(raw, grepPattern!, grepContextLines) : raw;
                        }
                    }
                }
            }

            return $"No type or member matching `{targetQuery}` found in Stardew Valley / SMAPI / MonoGame / xTile / Harmony assemblies. Try `sdv-dev api \"{targetQuery}\"` first.";
        }

        public static string DecompileExternalModApi(string modDllPath, string gamePath, string uniqueId, string version, string? typeOrMemberFilter = null, int maxLines = 320)
        {
            var handle = GetOrOpenCached(gamePath, modDllPath);
            var mainMod = handle.TypeSystem.MainModule;
            string dllName = Path.GetFileName(modDllPath);
            string asmStem = Path.GetFileNameWithoutExtension(modDllPath);

            if (!string.IsNullOrWhiteSpace(typeOrMemberFilter))
            {
                string q = typeOrMemberFilter!.Trim();
                var matchedTypes = FindMatchingTypes(mainMod, q);
                if (matchedTypes.Count > 0)
                    return DecompileTypeDefinition(dllName, handle, matchedTypes[0], maxLines: maxLines, fullType: true);

                int dot = q.LastIndexOf('.');
                if (dot > 0)
                {
                    var cts = FindMatchingTypes(mainMod, q.Substring(0, dot));
                    string mem = q.Substring(dot + 1);
                    foreach (var ct in cts)
                    {
                        var ms = ct.Methods.Where(m => m.Name.Equals(mem, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (ms.Count > 0)
                            return FormatMethodsDecompilation(dllName, handle, ct, ms, maxLines: maxLines, fullType: true);
                    }
                }
                return $"// No type or member `{q}` found in `{dllName}` ({uniqueId} v{version}).";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"// Prerequisite Mod: {uniqueId} (v{version})");
            sb.AppendLine($"// Assembly: {dllName}");
            sb.AppendLine($"// Usage via SMAPI Pintail proxy: Helper.ModRegistry.GetApi<I...Api>(\"{uniqueId}\")");
            sb.AppendLine();

            // Rank the mod's OWN public API interface first (avoiding bundled third-party client proxies in SpaceShared.APIs.*)
            var apiInterfaces = mainMod.TypeDefinitions
                .Where(t => t.Kind == TypeKind.Interface &&
                            t.Accessibility == Accessibility.Public &&
                            t.Name.IndexOf("Api", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(t =>
                {
                    bool nameMatchesAsm = t.Name.IndexOf(asmStem, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          (t.Namespace != null && t.Namespace.StartsWith(asmStem, StringComparison.OrdinalIgnoreCase));
                    bool isSharedForeignProxy = t.Namespace != null &&
                                                (t.Namespace.IndexOf("SpaceShared.APIs", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 t.Namespace.EndsWith(".Integrations", StringComparison.OrdinalIgnoreCase));
                    if (nameMatchesAsm && !isSharedForeignProxy) return 0;
                    if (!isSharedForeignProxy) return 1;
                    return 2;
                })
                .ThenBy(t => t.Name.Length)
                .ToList();

            if (apiInterfaces.Count > 0)
            {
                // If there is a clear primary interface matching the mod assembly, show primary interface(s) cleanly without foreign proxies
                var ownInterfaces = apiInterfaces.Where(t =>
                    t.Namespace == null ||
                    (t.Namespace.IndexOf("SpaceShared.APIs", StringComparison.OrdinalIgnoreCase) < 0 &&
                     !t.Namespace.EndsWith(".Integrations", StringComparison.OrdinalIgnoreCase))).ToList();
                var toShow = ownInterfaces.Count > 0 ? ownInterfaces : apiInterfaces;

                foreach (var iface in toShow)
                {
                    sb.AppendLine(SafeDecompileEntity(handle, iface.MetadataToken).TrimEnd());
                    sb.AppendLine();
                }
                return TruncateLines(sb.ToString(), maxLines);
            }

            // Fallback: list public types & GetApi() method
            var getApiMethods = mainMod.TypeDefinitions
                .SelectMany(t => t.Methods)
                .Where(m => m.Name == "GetApi")
                .ToList();
            foreach (var gm in getApiMethods)
            {
                sb.AppendLine(SafeDecompileEntity(handle, gm.MetadataToken).TrimEnd());
                sb.AppendLine();
            }

            sb.AppendLine("// Public Types in Assembly:");
            foreach (var pt in mainMod.TypeDefinitions.Where(t => t.Accessibility == Accessibility.Public).Take(40))
            {
                sb.AppendLine($"//   - {pt.FullName} ({pt.Kind})");
            }
            return sb.ToString();
        }

        private static string SafeDecompileEntity(AssemblyApiIndexer.DecompilerHandle handle, EntityHandle entityHandle)
        {
            try
            {
                var dec = handle.CreateDecompiler();
                return dec.DecompileAsString(entityHandle);
            }
            catch (Exception ex)
            {
                string detail = ex.InnerException != null
                    ? $"{ex.Message} -> {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                    : $"{ex.GetType().Name}: {ex.Message}";
                throw new InvalidOperationException($"Decompile failed for token 0x{MetadataTokens.GetToken(entityHandle):X8}: {detail}", ex);
            }
        }

        private static string? ResolveAssemblyFilePath(string gamePath, string assemblyName)
        {
            string dllFile = assemblyName + ".dll";
            string rootCand = Path.Combine(gamePath, dllFile);
            if (File.Exists(rootCand))
                return rootCand;
            string internalCand = Path.Combine(gamePath, "smapi-internal", dllFile);
            if (File.Exists(internalCand))
                return internalCand;
            return null;
        }

        private static string? DecompileFromTokenRecords(
            string gamePath,
            List<ApiRecord> records,
            int maxLines,
            bool fullType)
        {
            var first = records[0];
            string? dllPath = ResolveAssemblyFilePath(gamePath, first.Assembly);
            if (dllPath == null)
                return null;
            string dllName = Path.GetFileName(dllPath);

            var handle = GetOrOpenCached(gamePath, dllPath);
            var mainMod = handle.TypeSystem.MainModule;

            // Type record (MemberName is empty)
            if (string.IsNullOrEmpty(first.MemberName))
            {
                EntityHandle eh = MetadataTokens.EntityHandle(first.MetadataToken);
                if (eh.Kind == HandleKind.TypeDefinition)
                {
                    var typeDef = mainMod.GetDefinition((TypeDefinitionHandle)eh);
                    if (typeDef != null)
                        return DecompileTypeDefinition(dllName, handle, typeDef, maxLines, fullType);
                }
                return null;
            }

            // Method / Constructor overloads: jump straight to each MethodDefinitionHandle in O(1)!
            var methodDefs = new List<IMethod>();
            ITypeDefinition? declaringType = null;

            foreach (var rec in records)
            {
                EntityHandle eh = MetadataTokens.EntityHandle(rec.MetadataToken);
                if (eh.Kind == HandleKind.MethodDefinition)
                {
                    var mDef = mainMod.GetDefinition((MethodDefinitionHandle)eh);
                    if (mDef != null)
                    {
                        declaringType ??= mDef.DeclaringTypeDefinition;
                        methodDefs.Add(mDef);
                    }
                }
                else if (eh.Kind == HandleKind.PropertyDefinition)
                {
                    string pCode = SafeDecompileEntity(handle, eh);
                    return $"// Assembly: {dllName}\n// Property: {rec.FullTypeName}.{rec.MemberName}\n\n{pCode}";
                }
                else if (eh.Kind == HandleKind.FieldDefinition)
                {
                    string fCode = SafeDecompileEntity(handle, eh);
                    return $"// Assembly: {dllName}\n// Field: {rec.FullTypeName}.{rec.MemberName}\n\n{fCode}";
                }
                else if (eh.Kind == HandleKind.EventDefinition)
                {
                    string eCode = SafeDecompileEntity(handle, eh);
                    return $"// Assembly: {dllName}\n// Event: {rec.FullTypeName}.{rec.MemberName}\n\n{eCode}";
                }
            }

            if (methodDefs.Count > 0 && declaringType != null)
            {
                return FormatMethodsDecompilation(dllName, handle, declaringType, methodDefs, maxLines, fullType);
            }

            return null;
        }

        private static string DecompileTypeDefinition(
            string dllName,
            AssemblyApiIndexer.DecompilerHandle handle,
            ITypeDefinition targetType,
            int maxLines,
            bool fullType)
        {
            if (!fullType && targetType.Methods.Count() > 15)
            {
                string code = SafeDecompileEntity(handle, targetType.MetadataToken);
                var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length > maxLines)
                {
                    return FormatTypeOverviewWithHint(dllName, targetType, lines.Length);
                }
                return $"// Assembly: {dllName}\n// Type: {targetType.FullName}\n\n{code}";
            }
            else
            {
                string code = SafeDecompileEntity(handle, targetType.MetadataToken);
                return TruncateLines($"// Assembly: {dllName}\n// Type: {targetType.FullName}\n\n{code}", fullType ? int.MaxValue : maxLines);
            }
        }

        private static string FormatMethodsDecompilation(
            string dllName,
            AssemblyApiIndexer.DecompilerHandle handle,
            ITypeDefinition declaringType,
            List<IMethod> methods,
            int maxLines,
            bool fullType)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Assembly: {dllName}");
            sb.AppendLine($"// Declaring Type: {declaringType.FullName} (typeof({declaringType.FullName.Replace('+', '.')}))");
            sb.AppendLine($"// Matched Overloads: {methods.Count}");
            sb.AppendLine();

            foreach (var m in methods)
            {
                sb.AppendLine("// --- Harmony Target Info ---");
                sb.AppendLine($"// Signature: {AssemblyApiIndexer.FormatMethodSignature(declaringType, m)}");
                string paramTypes = string.Join(", ", m.Parameters.Select(p => $"typeof({p.Type.FullName.Replace('+', '.')})"));
                sb.AppendLine($"// AccessTools.Method(typeof({declaringType.FullName.Replace('+', '.')}), \"{m.Name}\"{(m.Parameters.Count > 0 ? $", new Type[] {{ {paramTypes} }}" : "")})");
                if (m.ReturnType.FullName != "System.Void")
                    sb.AppendLine($"// Harmony Postfix return injection: ref {AssemblyApiIndexer.ShortTypeName(m.ReturnType)} __result");
                if (!m.IsStatic)
                    sb.AppendLine($"// Harmony instance injection: {declaringType.Name} __instance");
                sb.AppendLine();

                string mCode = SafeDecompileEntity(handle, m.MetadataToken);
                sb.AppendLine(mCode.TrimEnd());
                sb.AppendLine();
            }

            return TruncateLines(sb.ToString(), fullType ? int.MaxValue : maxLines);
        }

        private static List<ITypeDefinition> FindMatchingTypes(MetadataModule module, string typeQuery)
        {
            string normalized = typeQuery.Replace('+', '.');
            var exactFull = module.TypeDefinitions
                .Where(t => t.FullName.Equals(typeQuery, StringComparison.OrdinalIgnoreCase) ||
                            t.FullName.Replace('+', '.').Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exactFull.Count > 0)
                return exactFull;

            return module.TypeDefinitions
                .Where(t => t.Name.Equals(typeQuery, StringComparison.OrdinalIgnoreCase) ||
                            t.FullName.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Namespace?.StartsWith("StardewValley") == true ? 0 : 1)
                .ThenBy(t => t.FullName.Length)
                .ToList();
        }

        private static string FormatTypeOverviewWithHint(string dllName, ITypeDefinition type, int totalLines)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Assembly: {dllName}");
            sb.AppendLine($"// Large type `{type.FullName}` ({totalLines} lines). Showing type summary & member directory.");
            sb.AppendLine($"// Tip: Run `sdv-dev decompile {type.Name}.<MethodName>` for a specific method, or add `--full` / `--lines 1000` for full class source.");
            sb.AppendLine();
            sb.AppendLine(AssemblyApiIndexer.FormatTypeSignature(type));
            sb.AppendLine("{");

            foreach (var p in type.Properties.Where(p => p.Accessibility == Accessibility.Public || p.Accessibility == Accessibility.Protected).Take(40))
            {
                sb.AppendLine("    " + AssemblyApiIndexer.FormatPropertySignature(type, p));
            }
            foreach (var m in type.Methods.Where(m => !m.IsAccessor && !m.Name.Contains('<')).Take(80))
            {
                sb.AppendLine("    " + AssemblyApiIndexer.FormatMethodSignature(type, m) + ";");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ApplyGrepFilter(string fullCode, string pattern, int contextLines)
        {
            int ctx = Math.Clamp(contextLines, 0, 200);
            var lines = fullCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var hitIndices = new List<int>();

            System.Text.RegularExpressions.Regex? regex = null;
            try
            {
                regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch
            {
                // Fallback to literal substring match if pattern is not valid regex
            }

            for (int i = 0; i < lines.Length; i++)
            {
                bool matched = regex != null
                    ? regex.IsMatch(lines[i])
                    : lines[i].IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                if (matched)
                    hitIndices.Add(i);
            }

            var sb = new StringBuilder();
            // Always keep top `// Assembly: ...` metadata header lines
            int headerEnd = 0;
            while (headerEnd < Math.Min(6, lines.Length) && lines[headerEnd].StartsWith("//", StringComparison.Ordinal))
            {
                sb.AppendLine(lines[headerEnd]);
                headerEnd++;
            }

            if (hitIndices.Count == 0)
            {
                sb.AppendLine($"// [grep] 0 lines matched `{pattern}` across {lines.Length} decompiled lines.");
                return sb.ToString();
            }

            sb.AppendLine($"// [grep] {hitIndices.Count} line(s) matched `{pattern}` across {lines.Length} total lines (context: ±{ctx} lines):");
            sb.AppendLine();

            var hitSet = new HashSet<int>(hitIndices);
            var ranges = new List<(int Start, int End)>();
            foreach (int h in hitIndices)
            {
                int s = Math.Max(0, h - ctx);
                int e = Math.Min(lines.Length - 1, h + ctx);
                if (ranges.Count > 0 && s <= ranges[ranges.Count - 1].End + 1)
                {
                    var prev = ranges[ranges.Count - 1];
                    ranges[ranges.Count - 1] = (prev.Start, Math.Max(prev.End, e));
                }
                else
                {
                    ranges.Add((s, e));
                }
            }

            for (int rIdx = 0; rIdx < ranges.Count; rIdx++)
            {
                if (rIdx > 0)
                    sb.AppendLine("// ---");

                var (s, e) = ranges[rIdx];
                for (int i = s; i <= e; i++)
                {
                    char marker = hitSet.Contains(i) ? '>' : ' ';
                    sb.AppendLine($"{marker} {i + 1,5}: {lines[i]}");
                }
            }

            return sb.ToString();
        }

        private static string TruncateLines(string text, int maxLines)
        {
            if (maxLines <= 0 || maxLines == int.MaxValue)
                return text;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length <= maxLines)
                return text;

            var sb = new StringBuilder();
            for (int i = 0; i < maxLines; i++)
                sb.AppendLine(lines[i]);
            sb.AppendLine($"// ... Truncated ({lines.Length - maxLines} more lines). Use `--lines 500` or `--full` to view all.");
            return sb.ToString();
        }
    }
}
