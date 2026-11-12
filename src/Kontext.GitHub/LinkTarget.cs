namespace Kontext.GitHub;

/// <summary>The GraphQL ids needed to link a branch, and the names of the branches already linked.</summary>
internal sealed record LinkTarget(string RepositoryId, string IssueId, IReadOnlySet<string> LinkedBranches);
