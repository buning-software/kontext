using System.Collections.Concurrent;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeClaudeLauncher : IClaudeLauncher
{
    public bool ReportsItself { get; set; } = true;

    public bool Resumed { get; set; }

    public ClaudeLaunch? Next { get; set; }

    public List<(StoredSession? Earlier, WorktreeInfo Worktree)> Calls { get; } = [];

    public List<string> GeneralCalls { get; } = [];

    public ClaudeLaunch PrepareGeneral(string workingDirectory)
    {
        GeneralCalls.Add(workingDirectory);

        return new ClaudeLaunch(
            TerminalSessionId.New(),
            new TerminalSessionOptions(workingDirectory, "claude", ["--general"]),
            ReportsItself: false,
            Resumed: false);
    }

    public ClaudeLaunch Prepare(StoredSession? earlier, WorktreeInfo worktree)
    {
        Calls.Add((earlier, worktree));

        return Next ?? new ClaudeLaunch(
            earlier?.SessionId ?? TerminalSessionId.New(),
            new TerminalSessionOptions(worktree.Path, "claude", ["--fake"]),
            ReportsItself,
            Resumed);
    }
}
