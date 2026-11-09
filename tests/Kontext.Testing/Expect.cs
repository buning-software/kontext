using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using NUnit.Framework;

namespace Kontext.Testing;

public static class Expect
{
    public static T[] Seq<T>(params T[] items) => items;
}
