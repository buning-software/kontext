namespace Kontext.Domain;

public enum FileChangeStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted,
}
