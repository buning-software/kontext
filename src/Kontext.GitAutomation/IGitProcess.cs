
namespace Kontext.GitAutomation;

public interface IGitProcess
{
    Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
