using System.Diagnostics;
using System.Text.Json;
using Kontext.Domain;

namespace Kontext.Claude;

/// <summary>
/// Reads the files Claude Code keeps for its running sessions (<c>&lt;config&gt;/sessions/&lt;pid&gt;.json</c>). A file only
/// counts while its process still runs and started when the file says, so a leftover file whose process id has since been
/// reused by another program is ignored. Anything unreadable is skipped: this check may only prevent a failed resume,
/// never block a good one.
/// </summary>
internal sealed class ClaudeLiveSessions(ClaudeOptions options) : IClaudeLiveSessions
{
    public int? FindRunningProcess(TerminalSessionId sessionId)
    {
        var folder = Path.Combine(options.ConfigDirectory, "sessions");
        if (!Directory.Exists(folder))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            if (Read(file) is { } entry && entry.SessionId == sessionId.Value && IsRunning(entry.Pid, entry.ProcessStart))
            {
                return entry.Pid;
            }
        }

        return null;
    }

    private static (int Pid, Guid SessionId, long ProcessStart)? Read(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;

            return root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var processId)
                && root.TryGetProperty("sessionId", out var session) && Guid.TryParse(session.GetString(), out var id)
                && root.TryGetProperty("procStart", out var start) && TryGetInt64(start, out var processStart)
                ? (processId, id, processStart)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    // Claude Code writes procStart as a string (it does not fit a JavaScript number); accept a plain number too.
    private static bool TryGetInt64(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return long.TryParse(element.GetString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt64(out value);
        }

        value = 0;
        return false;
    }

    private static bool IsRunning(int pid, long processStart)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited && process.StartTime.ToFileTimeUtc() == processStart;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
