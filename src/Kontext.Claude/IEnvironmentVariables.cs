namespace Kontext.Claude;

public interface IEnvironmentVariables
{
    bool IsSet(string name);

    void Remove(string name);
}
