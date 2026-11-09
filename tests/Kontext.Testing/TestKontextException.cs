using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using NUnit.Framework;

namespace Kontext.Testing;

public sealed class TestKontextException(string message) : KontextException(message);
