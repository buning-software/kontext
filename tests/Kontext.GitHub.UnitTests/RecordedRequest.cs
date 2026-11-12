using System.Net;
using System.Text;

namespace Kontext.GitHub.UnitTests;

internal sealed record RecordedRequest(HttpMethod Method, string Url, string Accept, Dictionary<string, string> Form, string Body = "", string? Authorization = null);
