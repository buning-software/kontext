
namespace Kontext.Claude.UnitTests;

[TestFixture]
public class ClaudeHookPayloadTests
{
    [Test]
    public void ReadsTheEventName()
    {
        var parsed = ClaudeHookPayload.Parse("""{ "session_id": "abc", "hook_event_name": "Stop", "last_assistant_message": "done" }""");

        Assert.That(parsed, Is.EqualTo(("Stop", (string?)null, (string?)"abc")));
    }

    [Test]
    public void ReadsTheNotificationType()
    {
        var parsed = ClaudeHookPayload.Parse("""{ "hook_event_name": "Notification", "notification_type": "permission_prompt", "message": "Claude needs your permission" }""");

        Assert.That(parsed, Is.EqualTo(("Notification", (string?)"permission_prompt", (string?)null)));
    }

    [Test]
    public void ReadsTheSessionIdClaudeIsUsing()
    {
        var parsed = ClaudeHookPayload.Parse("""{ "session_id": "0a1e4b01-845d-4b16-9dad-8f5539363baa", "hook_event_name": "UserPromptSubmit" }""");

        Assert.That(parsed?.SessionId, Is.EqualTo("0a1e4b01-845d-4b16-9dad-8f5539363baa"));
    }

    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[1, 2]")]
    [TestCase("\"just a string\"")]
    [TestCase("{}")]
    [TestCase("""{ "hook_event_name": 7 }""")]
    [TestCase("""{ "hook_event_name": "  " }""")]
    public void RejectsAnythingThatIsNotAHookEvent(string body)
    {
        Assert.That(ClaudeHookPayload.Parse(body), Is.Null);
    }
}
