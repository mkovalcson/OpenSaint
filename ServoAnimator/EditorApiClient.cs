using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ServoAnimator;

internal static class EditorApiClient
{
    public static async Task<int> Run(string[] args)
    {
        try
        {
            int processId = 0;
            string requestPath = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--process" && i + 1 < args.Length && int.TryParse(args[++i], out int id) && id > 0)
                    processId = id;
                else if (args[i] == "--request" && i + 1 < args.Length) requestPath = args[++i];
                else throw new ArgumentException("Usage: --editor-api [--process PID] [--request request.json]");
            }
            if (processId == 0)
            {
                var editors = Process.GetProcessesByName("AnimationEditorPlayer");
                try
                {
                    var ids = editors.Where(p => p.Id != Environment.ProcessId).Select(p => p.Id).ToArray();
                    if (ids.Length != 1) throw new InvalidOperationException("Specify --process PID: exactly one running editor could not be identified.");
                    processId = ids[0];
                }
                finally { foreach (var editor in editors) editor.Dispose(); }
            }
            string json = requestPath == null ? "{\"method\":\"get_state\"}" : File.ReadAllText(requestPath);
            if (json.Length > 1024 * 1024) throw new InvalidOperationException("Request exceeds 1 MiB.");
            using var parsed = JsonDocument.Parse(json);
            json = JsonSerializer.Serialize(parsed.RootElement);
            using var pipe = new NamedPipeClientStream(".", EditorApiServer.PipeName(processId), PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            await writer.WriteLineAsync(json);
            string response = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(35));
            if (response == null) throw new IOException("Editor disconnected. Retry only the identical requestId after checking state.");
            Console.WriteLine(response);
            using var result = JsonDocument.Parse(response);
            return result.RootElement.GetProperty("ok").GetBoolean() ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message,
                retry = "If an edit may have been submitted, retry only the identical request with the same requestId." }));
            return 1;
        }
    }
}
