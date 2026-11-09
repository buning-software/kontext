using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal static class Expect
{
    public static T[] Seq<T>(params T[] items) => items;
}
