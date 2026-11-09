using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.IntegrationTests;

internal sealed class NoGitAuthentication : IGitAuthentication
{
    public IReadOnlyDictionary<string, string> EnvironmentVariables() => new Dictionary<string, string>();
}
