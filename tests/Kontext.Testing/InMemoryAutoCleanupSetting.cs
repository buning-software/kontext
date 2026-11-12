using Kontext.Contracts;

namespace Kontext.Testing;

public sealed class InMemoryAutoCleanupSetting : IAutoCleanupSetting
{
    public bool IsEnabled { get; set; } = true;
}
