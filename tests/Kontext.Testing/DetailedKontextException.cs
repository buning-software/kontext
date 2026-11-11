using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class DetailedKontextException(string message, string details) : KontextException(message)
{
    public override string? Details { get; } = details;
}
