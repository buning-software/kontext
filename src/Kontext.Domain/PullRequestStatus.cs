namespace Kontext.Domain;

public sealed record PullRequestStatus(int Number, Uri Url, PullRequestState State, ChecksState Checks);
