using Kontext.TestSupport;

namespace Kontext.Sessions.IntegrationTests;

[TestFixture]
public class JsonHiddenRepositoryStoreTests
{
    private string _root = null!;

    [SetUp]
    public void CreateScratchDirectory()
    {
        _root = Path.Combine(Path.GetTempPath(), "kontext-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void DeleteScratchDirectory() => Directory.Delete(_root, recursive: true);

    private JsonHiddenRepositoryStore CreateStore(string? directory = null) => new(new SessionsOptions(directory ?? _root));

    [Test]
    public void HidesNothingBeforeAnythingIsSaved()
    {
        Assert.That(CreateStore().Load(), Is.Empty);
    }

    [Test]
    public void RoundTripsTheHiddenRepositories()
    {
        CreateStore().Save(new HashSet<string> { "acme/memento", "acme/gallery" });

        Assert.That(CreateStore().Load(), Is.EquivalentTo(Expect.Seq("acme/gallery", "acme/memento")));
    }

    [Test]
    public void IgnoresRepositoryCaseWhenLookingUp()
    {
        CreateStore().Save(new HashSet<string> { "Acme/Memento" });

        Assert.That(CreateStore().Load().Contains("acme/memento"), Is.True);
    }

    [Test]
    public void ForgetsRepositoriesThatAreNoLongerHidden()
    {
        var store = CreateStore();
        store.Save(new HashSet<string> { "acme/memento", "acme/gallery" });

        store.Save(new HashSet<string> { "acme/gallery" });

        Assert.That(CreateStore().Load(), Is.EquivalentTo(Expect.Seq("acme/gallery")));
    }

    [Test]
    public void TreatsACorruptFileAsEmptyAndRecoversOnNextSave()
    {
        File.WriteAllText(Path.Combine(_root, "hidden-repositories.json"), "[ not json");
        var store = CreateStore();

        Assert.That(store.Load(), Is.Empty);

        store.Save(new HashSet<string> { "acme/memento" });
        Assert.That(store.Load(), Is.EquivalentTo(Expect.Seq("acme/memento")));
    }

    [Test]
    public void CreatesTheDataDirectoryWhenItDoesNotExistYet()
    {
        var store = CreateStore(Path.Combine(_root, "not", "yet"));

        store.Save(new HashSet<string> { "acme/memento" });

        Assert.That(store.Load(), Is.EquivalentTo(Expect.Seq("acme/memento")));
    }
}
