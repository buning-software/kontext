using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeEnterprisePrompt : IEnterpriseServerPrompt
{
    public EnterpriseCredentials? Answer { get; set; } = new(new GitHubServer("github.corp.com"), "ghp_corporate");

    public int Asked { get; private set; }

    public Task<EnterpriseCredentials?> AskAsync()
    {
        Asked++;
        return Task.FromResult(Answer);
    }
}
