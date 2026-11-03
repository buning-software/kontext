using Kontext.Domain;

namespace Kontext.Contracts;

public interface IEnterpriseServerPrompt
{
    Task<EnterpriseCredentials?> AskAsync();
}
