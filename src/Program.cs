using System;
using System.Text;
using SdvKb.Daemon;

namespace SdvKb
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.InputEncoding = Encoding.UTF8;

            if (args.Length > 0 && string.Equals(args[0], "--daemon", StringComparison.OrdinalIgnoreCase))
            {
                return WorkerDaemonServer.RunDaemon();
            }

            try
            {
                return CliApp.Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] {ex.Message}");
                if (Environment.GetEnvironmentVariable("SDV_DEV_DEBUG") == "1")
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                return 1;
            }
        }
    }
}
