namespace Kontext.Presentation;

public sealed record IssueGroup<T>(string Repository, IReadOnlyList<T> Items);
