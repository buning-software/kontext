namespace Kontext.GitAutomation.UnitTests;

/// <summary>Answers each git call by its subcommand (the first argument), so one test can script several different calls.</summary>
internal sealed class ScriptedGitProcess : IGitProcess
{
    public List<string[]> Runs { get; } = [];

    public Dictionary<string, GitResult> Answers { get; } = [];

    public Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Runs.Add([.. arguments]);
        return Task.FromResult(Answers.TryGetValue(arguments[0], out var answer) ? answer : new GitResult(0, string.Empty, string.Empty));
    }
}
