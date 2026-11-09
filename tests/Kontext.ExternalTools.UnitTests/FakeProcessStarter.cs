using Kontext.Domain;

namespace Kontext.ExternalTools.UnitTests;

internal sealed class FakeProcessStarter : IProcessStarter
{
    public List<ProcessLaunch> Started { get; } = [];

    public HashSet<string> Missing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Start(ProcessLaunch launch)
    {
        if (Missing.Contains(launch.FileName))
        {
            throw new ExternalToolException($"{launch.FileName} could not be started.");
        }

        Started.Add(launch);
    }
}
