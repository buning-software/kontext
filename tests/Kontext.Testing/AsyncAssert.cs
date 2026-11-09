using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using NUnit.Framework;

namespace Kontext.Testing;

public static class AsyncAssert
{
    public static async Task EventuallyAsync(Func<bool> condition, string because)
    {
        var deadline = Task.Delay(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (deadline.IsCompleted)
            {
                Assert.Fail($"Timed out waiting for: {because}");
            }

            await Task.Delay(10);
        }
    }
}
