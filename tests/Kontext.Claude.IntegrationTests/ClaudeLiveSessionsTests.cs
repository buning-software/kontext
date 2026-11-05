using System.Diagnostics;
using System.Globalization;
using Kontext.Domain;

namespace Kontext.Claude.IntegrationTests;

[TestFixture]
public class ClaudeLiveSessionsTests
{
    private string _root = null!;

    [SetUp]
    public void CreateScratchDirectory()
    {
        _root = Path.Combine(Path.GetTempPath(), "kontext-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void DeleteScratchDirectory() => Directory.Delete(_root, recursive: true);

    private ClaudeLiveSessions CreateLiveSessions() => new(new ClaudeOptions(_root));

    private static (int Pid, long ProcessStart) ThisProcess()
    {
        using var process = Process.GetCurrentProcess();
        return (process.Id, process.StartTime.ToFileTimeUtc());
    }

    // The shape Claude Code writes to <config>/sessions/<pid>.json for every running session: procStart is a string.
    private void RegisterRunning(TerminalSessionId id, int pid, long processStart, bool processStartAsNumber = false)
    {
        var folder = Path.Combine(_root, "sessions");
        Directory.CreateDirectory(folder);
        var start = processStartAsNumber
            ? processStart.ToString(CultureInfo.InvariantCulture)
            : $"\"{processStart.ToString(CultureInfo.InvariantCulture)}\"";
        var json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"pid":{{pid}},"sessionId":"{{id.Value:D}}","cwd":"C:\\wt","procStart":{{start}},"kind":"interactive","status":"busy"}""");
        File.WriteAllText(Path.Combine(folder, pid.ToString(CultureInfo.InvariantCulture) + ".json"), json);
    }

    [Test]
    public void AlsoReadsTheProcessStartWhenItIsWrittenAsANumber()
    {
        var id = TerminalSessionId.New();
        var (pid, start) = ThisProcess();
        RegisterRunning(id, pid, start, processStartAsNumber: true);

        Assert.That(CreateLiveSessions().FindRunningProcess(id), Is.EqualTo(pid));
    }

    [Test]
    public void FindsNothingWhenClaudeHasNeverRun()
    {
        Assert.That(CreateLiveSessions().FindRunningProcess(TerminalSessionId.New()), Is.Null);
    }

    [Test]
    public void FindsTheProcessOfASessionThatIsStillRunning()
    {
        var id = TerminalSessionId.New();
        var (pid, start) = ThisProcess();
        RegisterRunning(id, pid, start);

        Assert.That(CreateLiveSessions().FindRunningProcess(id), Is.EqualTo(pid));
    }

    [Test]
    public void IgnoresOtherSessions()
    {
        var (pid, start) = ThisProcess();
        RegisterRunning(TerminalSessionId.New(), pid, start);

        Assert.That(CreateLiveSessions().FindRunningProcess(TerminalSessionId.New()), Is.Null);
    }

    [Test]
    public void IgnoresAStaleFileWhoseProcessIdNowBelongsToAnotherProcess()
    {
        var id = TerminalSessionId.New();
        var (pid, start) = ThisProcess();
        RegisterRunning(id, pid, start - TimeSpan.FromHours(1).Ticks);

        Assert.That(CreateLiveSessions().FindRunningProcess(id), Is.Null);
    }

    [Test]
    public void IgnoresAStaleFileWhoseProcessHasEnded()
    {
        var id = TerminalSessionId.New();
        using var ended = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
        var start = ended.StartTime.ToFileTimeUtc();
        ended.WaitForExit();
        RegisterRunning(id, ended.Id, start);

        Assert.That(CreateLiveSessions().FindRunningProcess(id), Is.Null);
    }

    [Test]
    public void SkipsFilesItCannotRead()
    {
        var id = TerminalSessionId.New();
        var (pid, start) = ThisProcess();
        Directory.CreateDirectory(Path.Combine(_root, "sessions"));
        File.WriteAllText(Path.Combine(_root, "sessions", "1.json"), "{ not json");
        RegisterRunning(id, pid, start);

        Assert.That(CreateLiveSessions().FindRunningProcess(id), Is.EqualTo(pid));
    }
}
