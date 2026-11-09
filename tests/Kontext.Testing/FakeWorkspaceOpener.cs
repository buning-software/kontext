using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeWorkspaceOpener : IWorkspaceOpener
{
    public List<(WorkspaceTool Tool, string Folder)> Opened { get; } = [];

    public Exception? FailWith { get; set; }

    public void Open(WorkspaceTool tool, string folder)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Opened.Add((tool, folder));
    }
}
