using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeFolderRemover : IFolderRemover
{
    public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Deleted { get; } = [];

    public Exception? FailWith { get; set; }

    public bool Exists(string path) => Existing.Contains(path);

    public void Delete(string path)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Deleted.Add(path);
        Existing.Remove(path);
    }
}
