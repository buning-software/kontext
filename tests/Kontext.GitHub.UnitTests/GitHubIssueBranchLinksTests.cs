using System.Net;
using System.Text.Json.Nodes;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitHub.UnitTests;

[TestFixture]
public class GitHubIssueBranchLinksTests
{
    private const string NotLinked = """{"data":{"repository":{"id":"R_1","issue":{"id":"I_29","linkedBranches":{"nodes":[{"ref":{"name":"other"}},{"ref":null}]}}}}}""";
    private const string Linked = """{"data":{"repository":{"id":"R_1","issue":{"id":"I_29","linkedBranches":{"nodes":[{"ref":{"name":"features/29-upgrade"}}]}}}}}""";
    private const string Created = """{"data":{"createLinkedBranch":{"linkedBranch":{"id":"LB_1"}}}}""";

    private StubHandler _handler = null!;
    private HttpClient _http = null!;
    private List<string> _tokenHosts = null!;
    private GitHubIssueBranchLinks _links = null!;

    [SetUp]
    public void CreateLinks()
    {
        _handler = new StubHandler();
        _http = new HttpClient(_handler);
        _tokenHosts = [];
        _links = new GitHubIssueBranchLinks(_http, host =>
        {
            _tokenHosts.Add(host);
            return "secret-token";
        });
    }

    [TearDown]
    public void DisposeClient()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    private static GitHubIssue Issue(string host = GitHubHosts.Default)
        => new(new RepositoryRef("acme", "memento", Host: host), 29, "Upgrade", null, "open", [], new Uri("https://github.com/acme/memento/issues/29"), "octocat", DateTimeOffset.UnixEpoch);

    private static JsonNode Variables(RecordedRequest request) => JsonNode.Parse(request.Body)!["variables"]!;

    [Test]
    public async Task SeesABranchThatIsLinkedToTheIssue()
    {
        _handler.Enqueue(HttpStatusCode.OK, Linked);

        Assert.That(await _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Is.True);
    }

    [Test]
    public async Task SeesThatABranchIsNotLinkedYet()
    {
        _handler.Enqueue(HttpStatusCode.OK, NotLinked);

        Assert.That(await _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Is.False);
    }

    [Test]
    public async Task AsksForTheIssueOfTheRightRepositoryWithTheSignedInToken()
    {
        _handler.Enqueue(HttpStatusCode.OK, NotLinked);

        await _links.IsLinkedAsync(Issue(), "features/29-upgrade");

        var request = _handler.Requests.Single();
        Assert.That(request.Url, Is.EqualTo("https://api.github.com/graphql"));
        Assert.That(request.Authorization, Is.EqualTo("Bearer secret-token"));
        Assert.That(_tokenHosts, Is.EqualTo(new[] { GitHubHosts.Default }));
        Assert.That(Variables(request)["owner"]!.GetValue<string>(), Is.EqualTo("acme"));
        Assert.That(Variables(request)["name"]!.GetValue<string>(), Is.EqualTo("memento"));
        Assert.That(Variables(request)["number"]!.GetValue<int>(), Is.EqualTo(29));
    }

    [TestCase("github.corp.com", "https://github.corp.com/api/graphql")]
    [TestCase("acme.ghe.com", "https://api.acme.ghe.com/graphql")]
    public async Task TalksToTheGraphQlEndpointOfAnEnterpriseServer(string host, string url)
    {
        _handler.Enqueue(HttpStatusCode.OK, NotLinked);

        await _links.IsLinkedAsync(Issue(host), "features/29-upgrade");

        Assert.That(_handler.Requests.Single().Url, Is.EqualTo(url));
    }

    [Test]
    public async Task CreatesTheBranchLinkedToTheIssueAtTheGivenCommit()
    {
        _handler.Enqueue(HttpStatusCode.OK, NotLinked);
        _handler.Enqueue(HttpStatusCode.OK, Created);

        await _links.LinkAsync(Issue(), "features/29-upgrade", "abc123");

        var create = Variables(_handler.Requests[1]);
        Assert.That(JsonNode.Parse(_handler.Requests[1].Body)!["query"]!.GetValue<string>(), Does.Contain("createLinkedBranch"));
        Assert.That(create["issueId"]!.GetValue<string>(), Is.EqualTo("I_29"));
        Assert.That(create["repositoryId"]!.GetValue<string>(), Is.EqualTo("R_1"));
        Assert.That(create["oid"]!.GetValue<string>(), Is.EqualTo("abc123"));
        Assert.That(create["name"]!.GetValue<string>(), Is.EqualTo("features/29-upgrade"));
    }

    [Test]
    public async Task DoesNotLinkABranchTwice()
    {
        _handler.Enqueue(HttpStatusCode.OK, Linked);

        await _links.LinkAsync(Issue(), "features/29-upgrade", "abc123");

        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReportsTheErrorGitHubGives()
    {
        _handler.Enqueue(HttpStatusCode.OK, NotLinked);
        _handler.Enqueue(HttpStatusCode.OK, """{"errors":[{"message":"Resource not accessible by integration"}]}""");

        Assert.That(
            () => _links.LinkAsync(Issue(), "features/29-upgrade", "abc123"),
            Throws.InstanceOf<GitHubException>().With.Message.Contains("Resource not accessible by integration"));
    }

    [Test]
    public void ReportsAMissingIssue()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{"data":{"repository":{"id":"R_1","issue":null}}}""");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("acme/memento#29"));
    }

    [Test]
    public void ReportsAMissingRepository()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{"data":{"repository":null}}""");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("repository acme/memento"));
    }

    [Test]
    public void ReportsTheErrorGitHubGivesWhileLookingUpTheIssue()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{"errors":[{"message":"Could not resolve to a Repository"}]}""");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("Could not resolve to a Repository"));
    }

    [Test]
    public void ReportsAFailedRequestWithItsStatusCode()
    {
        _handler.Enqueue(HttpStatusCode.Forbidden, """{"message":"Resource protected by organization SAML enforcement"}""");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("HTTP 403"));
    }

    [Test]
    public void ReportsAnAnswerItCannotRead()
    {
        _handler.Enqueue(HttpStatusCode.OK, "<html>maintenance</html>");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("could not read"));
    }

    [Test]
    public void ReportsAnAnswerWithoutData()
    {
        _handler.Enqueue(HttpStatusCode.OK, "{}");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("empty answer"));
    }

    [Test]
    public void DoesNotCallGitHubWithoutASignIn()
    {
        var links = new GitHubIssueBranchLinks(_http, host => throw new GitHubException($"You are not signed in to {host}."));

        Assert.That(() => links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("not signed in"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public void ReportsAFailureToCreateTheLink()
    {
        _handler.Enqueue(HttpStatusCode.OK, NotLinked);
        _handler.Enqueue(HttpStatusCode.BadGateway, string.Empty);

        Assert.That(() => _links.LinkAsync(Issue(), "features/29-upgrade", "abc123"), Throws.InstanceOf<GitHubException>().With.Message.Contains("HTTP 502"));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void RefusesABlankBranchOrCommitWithoutCallingGitHub(string blank)
    {
        Assert.That(() => _links.IsLinkedAsync(Issue(), blank), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => _links.LinkAsync(Issue(), blank, "abc123"), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => _links.LinkAsync(Issue(), "features/29-upgrade", blank), Throws.InstanceOf<ArgumentException>());
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public void ReportsARejectedSignIn()
    {
        _handler.Enqueue(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""");

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<AuthenticationFailedException>());
    }

    [Test]
    public void ReportsAnUnreachableServer()
    {
        _handler.EnqueueFailure(new HttpRequestException("no route"));

        Assert.That(() => _links.IsLinkedAsync(Issue(), "features/29-upgrade"), Throws.InstanceOf<GitHubException>().With.Message.Contains("Could not reach GitHub"));
    }
}
