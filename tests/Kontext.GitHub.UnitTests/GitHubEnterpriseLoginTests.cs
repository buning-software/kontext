using System.Net;
using Kontext.Domain;

namespace Kontext.GitHub.UnitTests;

[TestFixture]
public class GitHubEnterpriseLoginTests : GitHubDeviceLoginTestBase
{
    private static readonly GitHubServer Corporate = new("github.corp.com", "Ov23li.corporate");

    private static readonly string CorporateCodeJson = DeviceCodeJson.Replace("https://github.com/login/device", "https://github.corp.com/login/device", StringComparison.Ordinal);

    [Test]
    public async Task AsksTheEnterpriseServerForTheCodeUsingTheEnterpriseClientId()
    {
        Handler.Enqueue(HttpStatusCode.OK, CorporateCodeJson);

        await Create(clientId: null).StartAsync(Corporate);

        var request = Handler.Requests.Single();
        Assert.That(request.Url, Is.EqualTo("https://github.corp.com/login/device/code"));
        Assert.That(request.Form["client_id"], Is.EqualTo("Ov23li.corporate"), "the regular GitHub client id must not be sent to another server");
    }

    [Test]
    public async Task RemembersTheServerAndClientIdInTheChallengeSoWaitingNeedsNothingElse()
    {
        Handler.Enqueue(HttpStatusCode.OK, CorporateCodeJson);

        var challenge = await Create().StartAsync(Corporate);

        Assert.That(challenge.Host, Is.EqualTo("github.corp.com"));
        Assert.That(challenge.ClientId, Is.EqualTo("Ov23li.corporate"));
        Assert.That(challenge.VerificationUri, Is.EqualTo(new Uri("https://github.corp.com/login/device")));
    }

    [Test]
    public async Task RejectsAVerificationAddressOnAnotherServer()
    {
        Handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync(Corporate));

        Assert.That(ex.Message, Does.Contain("github.corp.com").And.Contain("unexpected verification address"));
    }

    [Test]
    public async Task PollsTheEnterpriseServerForTheTokenWithTheEnterpriseClientId()
    {
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);
        var challenge = Challenge(Time.GetUtcNow().AddMinutes(15)) with { Host = "github.corp.com", ClientId = "Ov23li.corporate" };

        var wait = Create(clientId: null).WaitForAccessTokenAsync(challenge);
        await AdvanceUntilSettled(wait, Interval);

        Assert.That(await wait, Is.EqualTo(Secret));
        var request = Handler.Requests.Single();
        Assert.That(request.Url, Is.EqualTo("https://github.corp.com/login/oauth/access_token"));
        Assert.That(request.Form["client_id"], Is.EqualTo("Ov23li.corporate"));
    }

    [Test]
    public async Task NamesTheEnterpriseServerWhenSignInIsDenied()
    {
        Handler.Enqueue(HttpStatusCode.OK, """{ "error": "access_denied" }""");
        var challenge = Challenge(Time.GetUtcNow().AddMinutes(15)) with { Host = "github.corp.com", ClientId = "Ov23li.corporate" };

        var wait = Create().WaitForAccessTokenAsync(challenge);
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(ex.Message, Is.EqualTo("Sign-in was cancelled on github.corp.com."));
    }
}
