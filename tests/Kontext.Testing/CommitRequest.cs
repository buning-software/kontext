using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Testing;

public sealed record CommitRequest(string WorktreePath, IReadOnlyList<FileChange> Changes, string Message);
