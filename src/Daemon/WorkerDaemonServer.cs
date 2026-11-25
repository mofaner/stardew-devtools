using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SdvKb.Database;
using SdvKb.Indexers;

namespace SdvKb.Daemon
{
    public static class WorkerDaemonServer
    {
        // 升级为 SDVD2 协议：每次请求携带磁盘上 `sdv-dev-worker.exe` 的修改时间+体积指纹 (`SelfWorkerStamp`)。
        // 【当初为什么要加这个指纹校验】：
        // 重新运行 `build.ps1` 或覆盖更新安装包后，若后台仍驻留着 10 分钟内未超时的旧版 `--daemon`，
        // 客户端直接连上去会读到旧版帮助文本与旧逻辑。加入二进制指纹对账后，一旦发现磁盘上的 worker 已更新，
        // 旧守护进程立即返回 `StaleDaemonExitCode` 并主动自退，客户端无感拉起新进程接管。
        private const string ProtocolMagic = "SDVD2";
        private const int StaleDaemonExitCode = -32099;
        private static readonly long SelfWorkerStamp = ComputeSelfBinaryStamp();
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);

        private static readonly object ExecLock = new object();
        private static DateTime _lastActivityUtc = DateTime.UtcNow;
        private static readonly DateTime _startedAtUtc = DateTime.UtcNow;
        private static int _activeRequests;
        private static int _servedCount;
        private static int _prewarmScheduled;
        private static volatile bool _prewarmCompleted;

        private static KbDatabase? _warmDb;
        private static string _warmDbPath = string.Empty;
        private static DateTime _warmDbWriteUtc;

        public static string ComputeUserToken()
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

        public static string GetDaemonPipeName()
        {
            return $"sdv-dev-daemon-{ComputeUserToken()}";
        }

        public static int RunDaemon()
        {
            using var mutex = new Mutex(initiallyOwned: true, $"Local\\{GetDaemonPipeName()}-mutex", out bool createdNew);
            if (!createdNew)
            {
                return 0;
            }

            using var cts = new CancellationTokenSource();
            _lastActivityUtc = DateTime.UtcNow;

            // Note: Do NOT prewarm heavy ILSpy decompilers here at startup!
            // Startup immediately enters AcceptLoopAsync so the 1st query (`api`/`data`/`doc`) finishes with zero CPU/GC contention.
            // PrewarmMainAssemblies is scheduled lazily 350ms AFTER the first request completes while idle.

            // If explicitly started via `sdv-dev-worker --daemon` (IsDaemon: true) with no immediate request,
            // schedule gentle idle prewarm after 400ms if still idle.
            ScheduleDeferredIdlePrewarm(cts.Token, delayMs: 400);

            // 10-minute idle auto-exit watchdog
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(15), cts.Token).ConfigureAwait(false);
                        if (Volatile.Read(ref _activeRequests) == 0 && (DateTime.UtcNow - _lastActivityUtc) >= IdleTimeout)
                        {
                            cts.Cancel();
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });

            try
            {
                // Run 2 concurrent pipe accept loops so back-to-back CLI invocations NEVER hit a gap between connections!
                Task.WhenAll(
                    AcceptLoopAsync(cts),
                    AcceptLoopAsync(cts)
                ).GetAwaiter().GetResult();
            }
            finally
            {
                CleanupResources();
            }

            return 0;
        }

        private static void ScheduleDeferredIdlePrewarm(CancellationToken token, int delayMs)
        {
            if (_prewarmCompleted || Interlocked.CompareExchange(ref _prewarmScheduled, 1, 0) != 0)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token).ConfigureAwait(false);

                    // Wait until any active foreground query finishes before touching CPU/GC
                    while (Volatile.Read(ref _activeRequests) > 0 && !token.IsCancellationRequested)
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    if (token.IsCancellationRequested)
                        return;

                    var cfg = KbConfig.Load();
                    lock (ExecLock)
                    {
                        EnsureWarmDb(cfg.DbPath);
                    }

                    if (!string.IsNullOrWhiteSpace(cfg.GamePath) && Directory.Exists(cfg.GamePath) && !token.IsCancellationRequested)
                    {
                        DecompileService.PrewarmMainAssemblies(cfg.GamePath);
                        _prewarmCompleted = true;
                    }
                }
                catch
                {
                }
            });
        }

        private static async Task AcceptLoopAsync(CancellationTokenSource cts)
        {
            var pipeSecurity = new PipeSecurity();
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            }

            while (!cts.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = NamedPipeServerStreamAcl.Create(
                        GetDaemonPipeName(),
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 8,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 65536,
                        outBufferSize: 65536,
                        pipeSecurity);

                    await server.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);

                    _lastActivityUtc = DateTime.UtcNow;
                    Interlocked.Increment(ref _activeRequests);

                    try
                    {
                        using var br = new BinaryReader(server, Encoding.UTF8, leaveOpen: true);
                        using var bw = new BinaryWriter(server, new UTF8Encoding(false), leaveOpen: true);

                        string magic = br.ReadString();
                        if (magic != ProtocolMagic)
                        {
                            // 若旧版/新版协议不一致，旧守护进程直接退出让位给磁盘上的新 worker
                            cts.Cancel();
                            break;
                        }

                        long clientExpectedStamp = br.ReadInt64();
                        string mode = br.ReadString();
                        string cwd = br.ReadString();
                        int argc = br.ReadInt32();
                        string[] args = new string[argc];
                        for (int i = 0; i < argc; i++)
                            args[i] = br.ReadString();

                        if (mode != "SHUTDOWN" && clientExpectedStamp != 0L && SelfWorkerStamp != 0L && clientExpectedStamp != SelfWorkerStamp)
                        {
                            bw.Write(StaleDaemonExitCode);
                            bw.Write(string.Empty);
                            bw.Write(string.Empty);
                            bw.Flush();
                            cts.Cancel();
                            break;
                        }

                        if (mode == "SHUTDOWN")
                        {
                            bw.Write(0);
                            bw.Write("Shutting down daemon.\n");
                            bw.Write(string.Empty);
                            bw.Flush();
                            cts.Cancel();
                            break;
                        }

                        if (mode == "PING")
                        {
                            TimeSpan up = DateTime.UtcNow - _startedAtUtc;
                            TimeSpan idleLeft = IdleTimeout - (DateTime.UtcNow - _lastActivityUtc);
                            if (idleLeft < TimeSpan.Zero) idleLeft = TimeSpan.Zero;
                            string statusMsg =
                                $"Daemon PID      : {Process.GetCurrentProcess().Id}\n" +
                                $"Uptime          : {up.TotalMinutes:F1} min\n" +
                                $"Idle Auto-Exit  : in {idleLeft.TotalMinutes:F1} min (timeout=10m)\n" +
                                $"Served Queries  : {_servedCount}\n" +
                                $"Warm DB Open    : {_warmDb != null}\n" +
                                $"ILSpy Prewarmed : {_prewarmCompleted}\n";
                            bw.Write(0);
                            bw.Write(statusMsg);
                            bw.Write(string.Empty);
                            bw.Flush();
                            continue;
                        }

                        (int code, string stdout, string stderr) = ExecuteCommandIsolated(cwd, args);
                        _lastActivityUtc = DateTime.UtcNow;

                        bw.Write(code);
                        bw.Write(stdout);
                        bw.Write(stderr);
                        bw.Flush();
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _activeRequests);
                        // Trigger deferred prewarm 300ms after first request has completely flushed & returned
                        ScheduleDeferredIdlePrewarm(cts.Token, delayMs: 300);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    if (cts.IsCancellationRequested)
                        break;
                }
                finally
                {
                    try { server?.Dispose(); } catch { }
                }
            }
        }

        private static (int ExitCode, string Stdout, string Stderr) ExecuteCommandIsolated(string cwd, string[] args)
        {
            lock (ExecLock)
            {
                _servedCount++;
                TextWriter origOut = Console.Out;
                TextWriter origErr = Console.Error;
                string origCwd = Environment.CurrentDirectory;

                using var swOut = new StringWriter();
                using var swErr = new StringWriter();

                try
                {
                    if (!string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd))
                        Environment.CurrentDirectory = cwd;

                    Console.SetOut(swOut);
                    Console.SetError(swErr);

                    var cfg = KbConfig.Load();
                    KbDatabase? db = EnsureWarmDb(cfg.DbPath);

                    int code;
                    try
                    {
                        code = CliApp.Run(args, db);
                    }
                    catch (Exception ex)
                    {
                        swErr.WriteLine($"[ERROR] {ex.Message}");
                        code = 1;
                    }

                    swOut.Flush();
                    swErr.Flush();
                    return (code, swOut.ToString(), swErr.ToString());
                }
                finally
                {
                    Console.SetOut(origOut);
                    Console.SetError(origErr);
                    try { Environment.CurrentDirectory = origCwd; } catch { }
                }
            }
        }

        private static KbDatabase? EnsureWarmDb(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
                return null;

            DateTime writeUtc = File.GetLastWriteTimeUtc(dbPath);
            if (_warmDb != null && _warmDbPath.Equals(dbPath, StringComparison.OrdinalIgnoreCase) && _warmDbWriteUtc == writeUtc)
                return _warmDb;

            try { _warmDb?.Dispose(); } catch { }
            _warmDb = new KbDatabase(dbPath);
            _warmDbPath = dbPath;
            _warmDbWriteUtc = writeUtc;
            return _warmDb;
        }

        private static long ComputeSelfBinaryStamp()
        {
            try
            {
                string? selfPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(selfPath) || !File.Exists(selfPath))
                    return 0L;
                var fi = new FileInfo(selfPath);
                return fi.LastWriteTimeUtc.Ticks ^ (fi.Length * 397L);
            }
            catch
            {
                return 0L;
            }
        }

        private static void CleanupResources()
        {
            lock (ExecLock)
            {
                try { _warmDb?.Dispose(); } catch { }
                _warmDb = null;
                DecompileService.ClearCache();
            }
        }
    }
}
