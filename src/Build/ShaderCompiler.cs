using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using SdvKb.Validation;

namespace SdvKb.Build
{
    public sealed class ShaderCompileResult
    {
        public bool Success { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string Profile { get; set; } = "OpenGL";
        public int ByteLength { get; set; }
        public int MgfxVersion { get; set; }
        public string Diagnostics { get; set; } = string.Empty;
    }

    /// <summary>
    /// 内置免安装 HLSL (.fx) 像素/顶点着色器编译器（内嵌 MonoGame 3.8.0.1641 原版对齐的 mgfxc + MojoShader）。
    ///
    /// 【当初为什么要写这个功能】：
    /// 星露谷物语 1.6（基于 MonoGame 3.8.0.1641 DesktopGL 跨平台 OpenGL 后端）在加载自定义 `Effect` 时，
    /// 既不能直接读取 `.fx` 文本，也不能加载新版 MonoGame 3.8.1+ (MGFX v10) 或 DirectX 的字节码，
    /// 必须先经 D3DCompiler 编译再由 MojoShader 转译为 **MGFX v9 OpenGL 字节码（`.mgfxo`）**。
    /// 以往用户或 AI 开发水波折射、菲涅尔反射、Bloom 泛光、高斯模糊、遮罩擦除等视觉模组时，因环境里没有匹配的
    /// `mgfxc` 工具链，只能拿五六个 `BlendState` 和逐行 `SpriteBatch` 切片硬凑，既费力又有画质上限。
    /// 因此将对齐星露谷 1.6 引擎版本的 `mgfxc 3.8.0.1641` 直接内嵌进单文件 worker 并在进程内调用：
    ///   1. `sdv-dev build` 自动将工程内全部 `*.fx` 编译为同目录同名 `.mgfxo`；
    ///   2. `sdv-dev check` 静态预编译校验 HLSL 语法与 OpenGL `ps_3_0`/`vs_3_0` 兼容性，进游戏前拦截着色器报错；
    ///   3. `sdv-dev shader` 支持单文件极速试编，零外部 `.NET SDK` 或全局工具依赖。
    /// </summary>
    public static class ShaderCompiler
    {
        private static readonly object InitLock = new object();
        private static readonly object InvokeLock = new object();
        private static MethodInfo? _mgfxcEntryPoint;
        private static string? _extractedDir;

        private static readonly string[] RequiredFiles =
        {
            "libmojoshader_64.dll",
            "CppNet.dll",
            "SharpDX.dll",
            "SharpDX.D3DCompiler.dll",
            "mgfxc.dll"
        };

        private static readonly HashSet<string> SkippedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "releases", "dist", "node_modules"
        };

        /// <summary>
        /// 查找模组工程目录下所有需要编译的 `.fx` 着色器源文件（排除 `.fxh` 头文件与 `bin`/`obj`/`releases` 目录）。
        /// </summary>
        public static List<string> FindProjectShaderFiles(string modDir)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(modDir) || !Directory.Exists(modDir))
                return list;

            string root = Path.GetFullPath(modDir);
            foreach (var file in Directory.GetFiles(root, "*.fx", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".fx", StringComparison.OrdinalIgnoreCase))
                    continue; // Exclude `.fxh` include headers picked up by legacy 8.3 wildcard matching on Windows

                string rel = file.Substring(root.Length).TrimStart('\\', '/');
                string[] parts = rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Take(Math.Max(0, parts.Length - 1)).Any(p => SkippedDirs.Contains(p)))
                    continue;

                list.Add(file);
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>
        /// 编译工程内全部 `.fx` 为同目录 `.mgfxo`（供 `sdv-dev build` 自动调用）。
        /// </summary>
        public static (bool AllSucceeded, List<string> CompiledOutputs, List<string> ErrorMessages) CompileAllInProject(string modDir)
        {
            var fxFiles = FindProjectShaderFiles(modDir);
            var outputs = new List<string>();
            var errors = new List<string>();
            if (fxFiles.Count == 0)
                return (true, outputs, errors);

            string root = Path.GetFullPath(modDir);
            foreach (var fx in fxFiles)
            {
                string outMgfxo = Path.ChangeExtension(fx, ".mgfxo");
                var res = CompileShaderFile(fx, outMgfxo, profile: "OpenGL");
                string relFx = fx.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    ? fx.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                    : Path.GetFileName(fx);

                if (!res.Success)
                {
                    errors.Add($"[ERROR] {relFx}: {CleanDiag(res.Diagnostics)}");
                }
                else
                {
                    string relOut = outMgfxo.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                        ? outMgfxo.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                        : outMgfxo;
                    outputs.Add(relOut);
                }
            }

            return (errors.Count == 0, outputs, errors);
        }

        /// <summary>
        /// 对工程内所有 `.fx` 执行离线静态语法/MojoShader 预编译检查（供 `sdv-dev check` 调用，不污染工作区文件）。
        /// </summary>
        public static void ValidateModShaders(string modDir, List<ValidationIssue> issues)
        {
            var fxFiles = FindProjectShaderFiles(modDir);
            if (fxFiles.Count == 0)
                return;

            string root = Path.GetFullPath(modDir);
            string tempDir = Path.Combine(GetSafeNativeCacheRoot(), "shader-check-tmp");
            Directory.CreateDirectory(tempDir);

            foreach (var fx in fxFiles)
            {
                string relFx = fx.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    ? fx.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                    : Path.GetFileName(fx);

                string tmpOut = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".mgfxo");
                try
                {
                    var res = CompileShaderFile(fx, tmpOut, profile: "OpenGL");
                    if (!res.Success)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "ERROR",
                            File = relFx,
                            Message = $"HLSL / OpenGL MojoShader compilation failed: {CleanDiag(res.Diagnostics)}"
                        });
                    }
                    else
                    {
                        // Remind if sibling .mgfxo hasn't been built yet or is older than .fx source
                        string expectedMgfxo = Path.ChangeExtension(fx, ".mgfxo");
                        if (!File.Exists(expectedMgfxo))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "WARN",
                                File = relFx,
                                Message = $"Shader source `{relFx}` is valid HLSL, but compiled `{Path.GetFileName(expectedMgfxo)}` does not exist yet. Run `sdv-dev build` to generate `.mgfxo`."
                            });
                        }
                        else if (File.GetLastWriteTimeUtc(expectedMgfxo) < File.GetLastWriteTimeUtc(fx))
                        {
                            issues.Add(new ValidationIssue
                            {
                                Severity = "WARN",
                                File = relFx,
                                Message = $"Compiled `{Path.GetFileName(expectedMgfxo)}` is older than `{relFx}`. Run `sdv-dev build` to refresh shader bytecode."
                            });
                        }
                    }
                }
                finally
                {
                    try { if (File.Exists(tmpOut)) File.Delete(tmpOut); } catch { }
                }
            }
        }

        /// <summary>
        /// 将单个 `.fx` HLSL 着色器编译为星露谷 1.6 MonoGame DesktopGL 兼容的 `.mgfxo` 字节码。
        /// </summary>
        public static ShaderCompileResult CompileShaderFile(
            string fxPath,
            string? outMgfxoPath = null,
            string profile = "OpenGL",
            string? defines = null,
            bool debug = false)
        {
            var result = new ShaderCompileResult
            {
                Profile = string.IsNullOrWhiteSpace(profile) ? "OpenGL" : profile.Trim()
            };

            if (string.IsNullOrWhiteSpace(fxPath) || !File.Exists(fxPath))
            {
                result.Diagnostics = $"Shader source file not found: `{fxPath}`.";
                return result;
            }

            string fullFx = Path.GetFullPath(fxPath);
            string fullOut = string.IsNullOrWhiteSpace(outMgfxoPath)
                ? Path.ChangeExtension(fullFx, ".mgfxo")
                : Path.GetFullPath(outMgfxoPath!);

            result.SourcePath = fullFx;
            result.OutputPath = fullOut;

            MethodInfo entry = EnsureMgfxcLoaded();

            string? outParent = Path.GetDirectoryName(fullOut);
            if (!string.IsNullOrEmpty(outParent))
                Directory.CreateDirectory(outParent);

            string stagingOut = fullOut + ".compiling";
            try { if (File.Exists(stagingOut)) File.Delete(stagingOut); } catch { }

            // 若源路径或输出路径含中文/特殊字符，D3DCompileFromFile / MojoShader 底层可能受路径字符集影响；
            // 自动拷贝源目录中的 .fx/.fxh 到纯 ASCII 临时目录编译后再拷回目标路径，100% 兼容中文路径！
            bool needsAsciiStage = ContainsNonAscii(fullFx) || ContainsNonAscii(stagingOut);
            string? asciiStageDir = null;
            string effectiveIn = fullFx;
            string effectiveOut = stagingOut;

            try
            {
                if (needsAsciiStage)
                {
                    // 【修复中文路径下子目录 #include "common/noise.fxh" 漏拷问题】：
                    // 向上定位模组根目录（含 manifest.json）作为拷贝基准根，递归保留全部子目录相对层级拷贝 .fx/.fxh/.hlsl/.hlsli，
                    // 确保无论是 `#include "common/noise.fxh"` 还是 `#include "../shared/util.fxh"` 都能在 ASCII 临时目录原样命中！
                    asciiStageDir = Path.Combine(GetSafeNativeCacheRoot(), "fx-stage-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(asciiStageDir);

                    string copyRoot = FindShaderContextRoot(fullFx);
                    foreach (var shaderFile in Directory.GetFiles(copyRoot, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(shaderFile);
                        if (!ext.Equals(".fx", StringComparison.OrdinalIgnoreCase) &&
                            !ext.Equals(".fxh", StringComparison.OrdinalIgnoreCase) &&
                            !ext.Equals(".hlsl", StringComparison.OrdinalIgnoreCase) &&
                            !ext.Equals(".hlsli", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string rel = Path.GetRelativePath(copyRoot, shaderFile);
                        string[] relParts = rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (relParts.Take(Math.Max(0, relParts.Length - 1)).Any(p => SkippedDirs.Contains(p)))
                            continue;

                        string stagedTarget = Path.Combine(asciiStageDir, rel);
                        string? stagedParent = Path.GetDirectoryName(stagedTarget);
                        if (!string.IsNullOrEmpty(stagedParent))
                            Directory.CreateDirectory(stagedParent);
                        File.Copy(shaderFile, stagedTarget, overwrite: true);
                    }

                    string relFxFromRoot = Path.GetRelativePath(copyRoot, fullFx);
                    effectiveIn = Path.Combine(asciiStageDir, relFxFromRoot);
                    effectiveOut = Path.Combine(asciiStageDir, "__compiled_out.mgfxo");
                }

                var cliArgs = new List<string>
                {
                    effectiveIn,
                    effectiveOut,
                    $"/Profile:{result.Profile}"
                };
                if (!string.IsNullOrWhiteSpace(defines))
                    cliArgs.Add($"/Defines:{defines!.Trim()}");
                if (debug)
                    cliArgs.Add("/Debug");

                int exitCode;
                string capturedOut;
                lock (InvokeLock)
                {
                    TextWriter origOut = Console.Out;
                    TextWriter origErr = Console.Error;
                    using var swOut = new StringWriter();
                    using var swErr = new StringWriter();
                    try
                    {
                        Console.SetOut(swOut);
                        Console.SetError(swErr);
                        object? ret = entry.Invoke(null, new object[] { cliArgs.ToArray() });
                        exitCode = ret is int rc ? rc : 1;
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        exitCode = 1;
                        swErr.WriteLine(tie.InnerException.Message);
                    }
                    finally
                    {
                        Console.SetOut(origOut);
                        Console.SetError(origErr);
                    }

                    var sb = new StringBuilder();
                    if (swErr.GetStringBuilder().Length > 0)
                        sb.AppendLine(swErr.ToString().Trim());
                    if (swOut.GetStringBuilder().Length > 0)
                        sb.AppendLine(swOut.ToString().Trim());
                    capturedOut = sb.ToString().Trim();
                }

                if (exitCode != 0 || !File.Exists(effectiveOut))
                {
                    // Replace staged temp path back with user's original fx path for clean error reporting
                    if (asciiStageDir != null)
                        capturedOut = capturedOut.Replace(effectiveIn, fullFx);
                    result.Success = false;
                    result.Diagnostics = string.IsNullOrWhiteSpace(capturedOut)
                        ? $"mgfxc exited with code {exitCode}."
                        : capturedOut;
                    return result;
                }

                byte[] bytes = File.ReadAllBytes(effectiveOut);
                if (bytes.Length < 8 || bytes[0] != (byte)'M' || bytes[1] != (byte)'G' || bytes[2] != (byte)'F' || bytes[3] != (byte)'X')
                {
                    result.Success = false;
                    result.Diagnostics = "Compiled output did not contain a valid MGFX header.";
                    return result;
                }

                File.Move(effectiveOut, fullOut, overwrite: true);
                result.Success = true;
                result.ByteLength = bytes.Length;
                result.MgfxVersion = bytes[4];
                result.Diagnostics = capturedOut;
                return result;
            }
            finally
            {
                try { if (File.Exists(stagingOut)) File.Delete(stagingOut); } catch { }
                if (asciiStageDir != null)
                {
                    try { Directory.Delete(asciiStageDir, recursive: true); } catch { }
                }
            }
        }

        private static MethodInfo EnsureMgfxcLoaded()
        {
            lock (InitLock)
            {
                if (_mgfxcEntryPoint != null)
                    return _mgfxcEntryPoint;

                string dir = EnsureMgfxcFilesOnDisk();
                _extractedDir = dir;

                // Load native MojoShader x64 DLL first so P/Invoke inside mgfxc resolves immediately
                string mojoPath = Path.Combine(dir, "libmojoshader_64.dll");
                NativeLibrary.Load(mojoPath);

                // Must load into AssemblyLoadContext.Default so MonoGame.Effect.ShaderProfile's
                // AppDomain type scan discovers OpenGLShaderProfile & DirectX11ShaderProfile!
                foreach (var dll in new[] { "CppNet.dll", "SharpDX.dll", "SharpDX.D3DCompiler.dll", "mgfxc.dll" })
                {
                    AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir, dll));
                }

                Assembly? mgfxcAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "mgfxc", StringComparison.OrdinalIgnoreCase));

                if (mgfxcAsm?.EntryPoint == null)
                    throw new InvalidOperationException("Failed to initialize embedded MonoGame Effect Compiler (`mgfxc.dll`).");

                _mgfxcEntryPoint = mgfxcAsm.EntryPoint;
                return _mgfxcEntryPoint;
            }
        }

        private static string EnsureMgfxcFilesOnDisk()
        {
            string targetDir = Path.Combine(GetSafeNativeCacheRoot(), "mgfxc-3.8.0.1641");
            bool allExist = RequiredFiles.All(f => File.Exists(Path.Combine(targetDir, f)));
            if (allExist)
                return targetDir;

            Directory.CreateDirectory(targetDir);

            // 1. Primary path: extract from embedded `MgfxcBundle.zip` inside single-file `sdv-dev-worker.exe`
            var asm = typeof(ShaderCompiler).Assembly;
            using Stream? zipStream = asm.GetManifestResourceStream("MgfxcBundle.zip");
            if (zipStream != null)
            {
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;
                    string dst = Path.Combine(targetDir, entry.Name);
                    entry.ExtractToFile(dst, overwrite: true);
                }
                return targetDir;
            }

            // 2. Local unbundled dev fallback: check repo `tools/mgfxc-3.8.0`
            string devToolsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "tools", "mgfxc-3.8.0"));
            if (!Directory.Exists(devToolsDir))
                devToolsDir = Path.Combine(AppContext.BaseDirectory, "tools", "mgfxc-3.8.0");

            if (Directory.Exists(devToolsDir) && RequiredFiles.All(f => File.Exists(Path.Combine(devToolsDir, f))))
            {
                foreach (var f in RequiredFiles)
                    File.Copy(Path.Combine(devToolsDir, f), Path.Combine(targetDir, f), overwrite: true);
                return targetDir;
            }

            throw new FileNotFoundException("Embedded `MgfxcBundle.zip` not found and `tools/mgfxc-3.8.0` missing. Run `build.ps1`.");
        }

        private static string GetSafeNativeCacheRoot()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!ContainsNonAscii(localAppData))
                return Path.Combine(localAppData, "sdv-dev");

            try
            {
                string progData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "sdv-dev");
                Directory.CreateDirectory(progData);
                return progData;
            }
            catch
            {
                return Path.Combine(localAppData, "sdv-dev");
            }
        }

        private static string FindShaderContextRoot(string fullFxPath)
        {
            string fallbackDir = Path.GetDirectoryName(fullFxPath)!;
            DirectoryInfo? cur = new DirectoryInfo(fallbackDir);
            for (int depth = 0; depth < 5 && cur != null; depth++)
            {
                if (File.Exists(Path.Combine(cur.FullName, "manifest.json")))
                    return cur.FullName;
                cur = cur.Parent;
            }
            return fallbackDir;
        }

        private static bool ContainsNonAscii(string s)
        {
            foreach (char c in s)
            {
                if (c > 127 || c == '&' || c == ';' || c == '\'' || c == '#')
                    return true;
            }
            return false;
        }

        private static string CleanDiag(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unknown shader compilation error.";
            var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !l.StartsWith("Compiled '", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return lines.Count > 0 ? string.Join(" | ", lines) : raw.Trim();
        }
    }
}
