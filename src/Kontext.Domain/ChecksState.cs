namespace Kontext.Domain;

/// <summary>The combined result of the workflow runs and checks on a pull request's latest commit.</summary>
public enum ChecksState
{
    None,
    Pending,
    Passing,
    Failing,
}
