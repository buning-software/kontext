namespace Kontext.Claude;

internal sealed class ProcessEnvironmentVariables : IEnvironmentVariables
{
    public bool IsSet(string name) => Environment.GetEnvironmentVariable(name) is not null;

    public void Remove(string name) => Environment.SetEnvironmentVariable(name, null);
}
