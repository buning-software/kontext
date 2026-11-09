namespace Kontext.GitHub;

internal sealed record DeviceTokenResult(
    DeviceTokenStatus Status,
    string? Token = null,
    int? IntervalSeconds = null,
    string? Message = null);
