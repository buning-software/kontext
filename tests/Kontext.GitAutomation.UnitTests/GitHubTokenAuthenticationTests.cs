using System.Text;
using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class GitHubTokenAuthenticationTests
{
    private sealed class FakeTokens(string? token, string? enterpriseHost = null, string? enterpriseToken = null) : IGitHubTokenStore
    {
        public string? Load(string host) =>
            GitHubHosts.IsDefault(host) ? token : string.Equals(host, enterpriseHost, StringComparison.OrdinalIgnoreCase) ? enterpriseToken : null;

        public void Save(string host, string value) => throw new NotSupportedException();

        public void Clear(string host) => throw new NotSupportedException();

        public string? FindEnterpriseHost() => enterpriseHost;
    }

    private static string Decode(string header) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(header["AUTHORIZATION: basic ".Length..]));

    [Test]
    public void SendsTheTokenOnlyToGitHubAndKeepsItOutOfTheCommandLine()
    {
        var environment = new GitHubTokenAuthentication(new FakeTokens("ghp_secret")).EnvironmentVariables();

        Assert.That(environment["GIT_CONFIG_KEY_0"], Is.EqualTo("http.https://github.com/.extraheader"));

        var header = environment["GIT_CONFIG_VALUE_0"];
        Assert.That(header, Does.StartWith("AUTHORIZATION: basic "));
        Assert.That(Decode(header), Is.EqualTo("x-access-token:ghp_secret"));
    }

    [Test]
    public void SilencesCredentialHelpersSoNoSignInWindowCanAppear()
    {
        var environment = new GitHubTokenAuthentication(new FakeTokens("ghp_secret")).EnvironmentVariables();

        Assert.That(environment["GIT_CONFIG_COUNT"], Is.EqualTo("2"));
        Assert.That(environment["GIT_CONFIG_KEY_1"], Is.EqualTo("credential.helper"));
        Assert.That(environment["GIT_CONFIG_VALUE_1"], Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ChangesNothingWhenThereIsNoToken(string? token)
    {
        Assert.That(new GitHubTokenAuthentication(new FakeTokens(token)).EnvironmentVariables(), Is.Empty);
    }

    [Test]
    public void SendsEachAccountsTokenOnlyToItsOwnServer()
    {
        var environment = new GitHubTokenAuthentication(new FakeTokens("ghp_public", "github.corp.com", "ghe_corporate")).EnvironmentVariables();

        Assert.That(environment["GIT_CONFIG_COUNT"], Is.EqualTo("3"));
        Assert.That(environment["GIT_CONFIG_KEY_0"], Is.EqualTo("http.https://github.com/.extraheader"));
        Assert.That(Decode(environment["GIT_CONFIG_VALUE_0"]), Is.EqualTo("x-access-token:ghp_public"));
        Assert.That(environment["GIT_CONFIG_KEY_1"], Is.EqualTo("http.https://github.corp.com/.extraheader"));
        Assert.That(Decode(environment["GIT_CONFIG_VALUE_1"]), Is.EqualTo("x-access-token:ghe_corporate"));
        Assert.That(environment["GIT_CONFIG_KEY_2"], Is.EqualTo("credential.helper"));
    }

    [Test]
    public void SendsOnlyTheEnterpriseTokenWhenThereIsNoGitHubAccount()
    {
        var environment = new GitHubTokenAuthentication(new FakeTokens(null, "github.corp.com", "ghe_corporate")).EnvironmentVariables();

        Assert.That(environment["GIT_CONFIG_COUNT"], Is.EqualTo("2"));
        Assert.That(environment["GIT_CONFIG_KEY_0"], Is.EqualTo("http.https://github.corp.com/.extraheader"));
        Assert.That(environment, Does.Not.ContainKey("GIT_CONFIG_KEY_2"));
    }
}
