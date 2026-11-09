using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using NUnit.Framework;

namespace Kontext.Testing;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
