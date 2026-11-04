using Kontext.TestSupport;

namespace Kontext.Sessions.IntegrationTests;

[TestFixture]
public class JsonCollapsedRepositoryStoreTests
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

    private JsonCollapsedRepositoryStore CreateStore() => new(new SessionsOptions(_root));

    [Test]
    public void CollapsesNothingBeforeAnythingIsSaved() => Assert.That(CreateStore().Load(), Is.Empty);

    [Test]
    public void RoundTripsTheCollapsedRepositoriesIgnoringCase()
    {
        CreateStore().Save(new HashSet<string> { "Acme/Memento", "acme/gallery" });

        var loaded = CreateStore().Load();

        Assert.That(loaded, Is.EquivalentTo(Expect.Seq("acme/gallery", "Acme/Memento")));
        Assert.That(loaded.Contains("acme/memento"), Is.True);
    }

    [Test]
    public void KeepsItsOwnFileApartFromTheHiddenRepositories()
    {
        new JsonHiddenRepositoryStore(new SessionsOptions(_root)).Save(new HashSet<string> { "acme/hidden" });

        CreateStore().Save(new HashSet<string> { "acme/collapsed" });

        Assert.That(new JsonHiddenRepositoryStore(new SessionsOptions(_root)).Load(), Is.EquivalentTo(Expect.Seq("acme/hidden")));
        Assert.That(CreateStore().Load(), Is.EquivalentTo(Expect.Seq("acme/collapsed")));
    }

    [Test]
    public void TreatsACorruptFileAsEmptyAndRecoversOnNextSave()
    {
        File.WriteAllText(Path.Combine(_root, "collapsed-repositories.json"), "[ not json");
        var store = CreateStore();

        Assert.That(store.Load(), Is.Empty);

        store.Save(new HashSet<string> { "acme/memento" });
        Assert.That(store.Load(), Is.EquivalentTo(Expect.Seq("acme/memento")));
    }
}
