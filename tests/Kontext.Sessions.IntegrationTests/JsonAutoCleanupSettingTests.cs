namespace Kontext.Sessions.IntegrationTests;

[TestFixture]
public class JsonAutoCleanupSettingTests
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

    private JsonAutoCleanupSetting CreateSetting(string? directory = null) => new(new SessionsOptions(directory ?? _root));

    [Test]
    public void IsOnBeforeAnythingIsSaved()
    {
        Assert.That(CreateSetting().IsEnabled, Is.True);
    }

    [Test]
    public void RemembersThatItWasTurnedOff()
    {
        CreateSetting().IsEnabled = false;

        Assert.That(CreateSetting().IsEnabled, Is.False);
    }

    [Test]
    public void RemembersThatItWasTurnedBackOn()
    {
        var setting = CreateSetting();
        setting.IsEnabled = false;

        setting.IsEnabled = true;

        Assert.That(CreateSetting().IsEnabled, Is.True);
    }

    [Test]
    public void ReadsACorruptFileAsOn()
    {
        File.WriteAllText(Path.Combine(_root, "auto-cleanup.json"), "{ not json");

        Assert.That(CreateSetting().IsEnabled, Is.True);
    }

    [Test]
    public void CreatesTheStateDirectoryWhenSaving()
    {
        var nested = Path.Combine(_root, "nested");

        CreateSetting(nested).IsEnabled = false;

        Assert.That(CreateSetting(nested).IsEnabled, Is.False);
    }
}
