using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeSessionStore : ISessionStore
{
    public Dictionary<IssueKey, StoredSession> Saved { get; } = [];

    public StoredSession? Find(IssueKey issue) => Saved.GetValueOrDefault(issue);

    public Exception? SaveFailsWith { get; set; }

    public void Save(StoredSession session)
    {
        if (SaveFailsWith is not null)
        {
            throw SaveFailsWith;
        }

        Saved[session.Issue] = session;
    }

    public void Remove(IssueKey issue) => Saved.Remove(issue);
}
