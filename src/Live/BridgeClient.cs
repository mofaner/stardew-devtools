using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace SdvKb.Live
{
    public static class BridgeClient
    {
        public static string GetPipeName()
        {
            return $"sdv-dev-bridge-{SdvKb.Daemon.WorkerDaemonServer.ComputeUserToken()}";
        }

        public static (bool Connected, string ResponseJson, string? Error) TrySendJson(string requestJson, int connectTimeoutMs = 1500)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(connectTimeoutMs);

                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), bufferSize: 8192, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);

                writer.WriteLine(requestJson);
                string? resp = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(resp))
                    return (false, string.Empty, "Empty response from SdvDevBridge.");

                return (true, resp!, null);
            }
            catch (TimeoutException)
            {
                return (false, string.Empty, "Timed out connecting to SdvDevBridge named pipe.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        public static string SendRequired(object payload, int connectTimeoutMs = 3000)
        {
            string json = JsonSerializer.Serialize(payload);
            var (connected, resp, err) = TrySendJson(json, connectTimeoutMs);
            if (!connected)
                throw new InvalidOperationException($"Game bridge is not connected ({err}). Run `sdv-dev live` or `sdv-dev start` first.");
            return PrettyFormatJson(resp);
        }

        public static string SendJsonAllowingLogicalFailure(object payload, out bool logicalOk, int connectTimeoutMs = 3000)
        {
            string formatted = SendRequired(payload, connectTimeoutMs);
            logicalOk = true;
            try
            {
                using var doc = JsonDocument.Parse(formatted);
                if (doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.False)
                    logicalOk = false;
            }
            catch { }
            return formatted;
        }

        public static string SendrawOrCheckedWarp(object payload, out bool warpOk, int connectTimeoutMs = 3000)
        {
            return SendJsonAllowingLogicalFailure(payload, out warpOk, connectTimeoutMs);
        }

        public static string PrettyFormatJson(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch
            {
                return rawJson;
            }
        }
    }
}
