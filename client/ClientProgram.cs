using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SdvDevClient
{
    /// <summary>
    /// Zero-NuGet-dependency thin CLI launcher (`sdv-dev.exe`).
    /// Forwards commands over a binary Named Pipe to the 10-minute auto-idle warm worker (`sdv-dev-worker.exe`),
    /// captures piped stdin for `eval --stdin`, and automatically shuts down the daemon before `sync` or `daemon-stop`.
    /// </summary>
    internal static class ClientProgram
    {
        private const string ProtocolMagic = "SDVD1";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        public static int Main(string[] args)
        {
            TryEnableWin10Utf8Console();
            EnsureSafeBundleExtractDir();

            string first = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

            if (first == "daemon-stop")
            {
                bool wasRunning = StopDaemonAndWait();
                Console.WriteLine(wasRunning ? "Stopped running sdv-dev warm daemon." : "sdv-dev daemon was not running.");
                return 0;
            }

            if (first == "daemon-status")
            {
                if (TrySendToDaemon("PING", Environment.CurrentDirectory, Array.Empty<string>(), connectTimeoutMs: 250, out int code, out string so, out string se))
                {
                    if (!string.IsNullOrEmpty(so)) Console.Out.Write(so);
                    if (!string.IsNullOrEmpty(se)) Console.Error.Write(se);
                    return code;
                }
                Console.WriteLine("sdv-dev warm daemon is not running (idle / stopped).");
                return 0;
            }

            string workerExe = ResolveWorkerExePath();
            if (!File.Exists(workerExe))
            {
                Console.Error.WriteLine($"[ERROR] Worker executable not found: {workerExe}");
                return 1;
            }

            // Explicit foreground daemon entry (used when AI launches `sdv-dev --daemon` with IsDaemon: true)
            if (first == "--daemon" || first == "daemon")
            {
                return RunWorkerDirect(workerExe, new[] { "--daemon" });
            }

            // Capture piped standard input for `sdv-dev eval --stdin` or `sdv-dev event-play --stdin` before forwarding over Named Pipe!
            if ((first == "eval" || first == "event-play" || first == "event") && ShouldCaptureStdin(first, args))
            {
                string stdinText = Console.In.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stdinText))
                {
                    string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(stdinText));
                    var expanded = new List<string>(args.Length + 2);
                    foreach (var a in args)
                    {
                        if (!string.Equals(a, "--stdin", StringComparison.OrdinalIgnoreCase) && a != "-")
                            expanded.Add(a);
                    }
                    expanded.Add(first == "eval" ? "--code-b64" : "--script-b64");
                    expanded.Add(b64);
                    args = expanded.ToArray();
                }
            }

            // `sync` stops daemon first so sdv-dev.db is 100% unlocked, then streams console output directly.
            if (first == "sync")
            {
                StopDaemonAndWait();
                return RunWorkerDirect(workerExe, args);
            }

            // Route through active daemon when alive (including `start` / `restart` so when daemon runs under `IsDaemon: true`,
            // StardewModdingAPI.exe is spawned inside the persistent daemon job and survives across AI turns!)
            string cwd = Environment.CurrentDirectory;
            if (IsDaemonAlive())
            {
                if (TrySendToDaemon("EXEC", cwd, args, connectTimeoutMs: 1500, out int exitCode, out string stdout, out string stderr))
                {
                    if (!string.IsNullOrEmpty(stdout)) Console.Out.Write(stdout);
                    if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
                    return exitCode;
                }
            }

            // If no persistent daemon is running yet and user invoked slow `start`/`restart`, run directly
            if (first == "start" || first == "restart")
            {
                return RunWorkerDirect(workerExe, args);
            }

            // No daemon alive: spawn background auto-idle daemon and connect immediately
            if (SpawnBackgroundDaemon(workerExe))
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 2500)
                {
                    Thread.Sleep(8);
                    if (!IsDaemonAlive())
                        continue;
                    if (TrySendToDaemon("EXEC", cwd, args, connectTimeoutMs: 400, out int exitCode, out string stdout, out string stderr))
                    {
                        if (!string.IsNullOrEmpty(stdout)) Console.Out.Write(stdout);
                        if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
                        return exitCode;
                    }
                }
            }

            // Graceful fallback: execute worker directly if pipe connection couldn't be established
            return RunWorkerDirect(workerExe, args);
        }

        private static bool ShouldCaptureStdin(string first, string[] args)
        {
            bool explicitStdin = false;
            bool hasFileOrB64 = false;
            int positionalCount = 0;

            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--stdin", StringComparison.OrdinalIgnoreCase) || a == "-")
                {
                    explicitStdin = true;
                }
                else if (string.Equals(a, "--file", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "-f", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--code-b64", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--script-b64", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--script", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--game-path", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "-g", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--screen", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(a, "--file", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "-f", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "--script", StringComparison.OrdinalIgnoreCase))
                        hasFileOrB64 = true;
                    i++;
                }
                else if (!a.StartsWith("-", StringComparison.Ordinal))
                {
                    positionalCount++;
                }
            }

            if (explicitStdin)
                return true;
            if (first == "eval" && !hasFileOrB64 && positionalCount == 0 && Console.IsInputRedirected)
                return true;
            return false;
        }

        private static bool IsDaemonAlive()
        {
            if (Mutex.TryOpenExisting($"Local\\{GetDaemonPipeName()}-mutex", out var existing))
            {
                existing.Dispose();
                return true;
            }
            return false;
        }

        private static void TryEnableWin10Utf8Console()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                    SetConsoleOutputCP(65001);
                if (!Console.IsInputRedirected)
                    SetConsoleCP(65001);
            }
            catch { }

            try
            {
                Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                Console.InputEncoding = Encoding.UTF8;
            }
            catch { }
        }

        private static void EnsureSafeBundleExtractDir()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR")))
                    return;

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                bool hasNonAsciiOrSpecial = false;
                foreach (char ch in localAppData)
                {
                    if (ch > 127 || ch == '&' || ch == ';' || ch == '\'' || ch == '#')
                    {
                        hasNonAsciiOrSpecial = true;
                        break;
                    }
                }

                // Prefer ASCII C:\ProgramData\sdv-dev\bundle-<token> when user profile contains Chinese/special chars on Win10,
                // and otherwise %LOCALAPPDATA%\sdv-dev\bundle so Windows Storage Sense never deletes e_sqlite3.dll mid-session!
                if (hasNonAsciiOrSpecial)
                {
                    try
                    {
                        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        if (!string.IsNullOrWhiteSpace(progData))
                        {
                            string sharedCache = Path.Combine(progData, "sdv-dev", $"bundle-{ComputeUserToken()}");
                            Directory.CreateDirectory(sharedCache);
                            Environment.SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", sharedCache);
                            return;
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    string userCache = Path.Combine(localAppData, "sdv-dev", "bundle");
                    Directory.CreateDirectory(userCache);
                    Environment.SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", userCache);
                }
            }
            catch { }
        }

        private static string ComputeUserToken()
        {
            string raw = (Environment.UserName ?? "default").Trim().ToLowerInvariant();
            byte[] bytes = Encoding.UTF8.GetBytes(raw);
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619u;
            }
            return $"u{hash:x8}";
        }

        private static string GetDaemonPipeName()
        {
            return $"sdv-dev-daemon-{ComputeUserToken()}";
        }

        private static string ResolveWorkerExePath()
        {
            // Prefer co-located `sdv-dev-worker.exe` right beside `sdv-dev.exe` (2-file portable layout in ~/.local/bin or dist/)
            string coLocated = Path.Combine(AppContext.BaseDirectory, "sdv-dev-worker.exe");
            if (File.Exists(coLocated))
                return coLocated;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string binWorker = Path.Combine(home, ".local", "bin", "sdv-dev-worker.exe");
            if (File.Exists(binWorker))
                return binWorker;

            return Path.Combine(home, ".local", "lib", "sdv-dev", "sdv-dev-worker.exe");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcessW(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static bool SpawnBackgroundDaemon(string workerExe)
        {
            try
            {
                const uint CREATE_NO_WINDOW = 0x08000000;
                const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;

                var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
                var cmdLine = new StringBuilder($"{QuoteWindowsArg(workerExe)} --daemon");
                string? workDir = Path.GetDirectoryName(workerExe);

                bool ok = CreateProcessW(
                    workerExe,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    bInheritHandles: false,
                    dwCreationFlags: CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP,
                    IntPtr.Zero,
                    workDir,
                    ref si,
                    out var pi);

                if (ok)
                {
                    if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                    if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                }
                return ok;
            }
            catch
            {
                return false;
            }
        }

        public static bool StopDaemonAndWait()
        {
            bool signaled = TrySendToDaemon("SHUTDOWN", Environment.CurrentDirectory, Array.Empty<string>(), connectTimeoutMs: 250, out _, out _, out _);

            var procs = Process.GetProcessesByName("sdv-dev-worker");
            if (procs.Length == 0)
                return signaled;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3500)
            {
                bool anyAlive = false;
                foreach (var p in procs)
                {
                    try
                    {
                        if (!p.HasExited)
                            anyAlive = true;
                    }
                    catch { }
                }
                if (!anyAlive)
                    return true;
                Thread.Sleep(40);
            }

            foreach (var p in procs)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                }
                catch { }
            }
            return true;
        }

        private static bool TrySendToDaemon(
            string mode,
            string cwd,
            string[] args,
            int connectTimeoutMs,
            out int exitCode,
            out string stdout,
            out string stderr)
        {
            exitCode = 1;
            stdout = string.Empty;
            stderr = string.Empty;

            try
            {
                using var pipe = new NamedPipeClientStream(".", GetDaemonPipeName(), PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(connectTimeoutMs);

                using var bw = new BinaryWriter(pipe, new UTF8Encoding(false), leaveOpen: true);
                using var br = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);

                bw.Write(ProtocolMagic);
                bw.Write(mode);
                bw.Write(cwd ?? string.Empty);
                bw.Write(args.Length);
                for (int i = 0; i < args.Length; i++)
                    bw.Write(args[i] ?? string.Empty);
                bw.Flush();

                exitCode = br.ReadInt32();
                stdout = br.ReadString();
                stderr = br.ReadString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int RunWorkerDirect(string workerExe, string[] args)
        {
            var sbArgs = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sbArgs.Append(' ');
                sbArgs.Append(QuoteWindowsArg(args[i]));
            }

            var psi = new ProcessStartInfo
            {
                FileName = workerExe,
                Arguments = sbArgs.ToString(),
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            return proc.ExitCode;
        }

        private static string QuoteWindowsArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";
            if (arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '\"' }) < 0)
                return arg;

            var sb = new StringBuilder();
            sb.Append('\"');
            for (int i = 0; ; i++)
            {
                int numBackslashes = 0;
                while (i < arg.Length && arg[i] == '\\')
                {
                    i++;
                    numBackslashes++;
                }

                if (i == arg.Length)
                {
                    sb.Append('\\', numBackslashes * 2);
                    break;
                }
                else if (arg[i] == '\"')
                {
                    sb.Append('\\', numBackslashes * 2 + 1);
                    sb.Append('\"');
                }
                else
                {
                    sb.Append('\\', numBackslashes);
                    sb.Append(arg[i]);
                }
            }
            sb.Append('\"');
            return sb.ToString();
        }
    }
}
