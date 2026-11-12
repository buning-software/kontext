using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.GitHub;

/// <summary>
/// Reads and creates an issue's linked branches through GitHub's GraphQL API; the REST API has no way to link a branch.
/// GitHub only links a branch it creates itself, so linking also creates the branch on GitHub.
/// </summary>
internal sealed class GitHubIssueBranchLinks(HttpClient http, Func<string, string> tokenFor) : IIssueBranchLinks
{
    public GitHubIssueBranchLinks(HttpClient http, GitHubClients clients)
        : this(http, (clients ?? throw new ArgumentNullException(nameof(clients))).TokenFor)
    {
    }

    private const string FindQuery =
        "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){id issue(number:$number){id linkedBranches(first:100){nodes{ref{name}}}}}}";

    private const string CreateMutation =
        "mutation($issueId:ID!,$repositoryId:ID!,$oid:GitObjectID!,$name:String!){createLinkedBranch(input:{issueId:$issueId,repositoryId:$repositoryId,oid:$oid,name:$name}){linkedBranch{id}}}";

    public async Task<bool> IsLinkedAsync(GitHubIssue issue, string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        return (await FindAsync(issue, cancellationToken)).LinkedBranches.Contains(branchName);
    }

    public async Task LinkAsync(GitHubIssue issue, string branchName, string commitOid, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitOid);

        var found = await FindAsync(issue, cancellationToken);
        if (found.LinkedBranches.Contains(branchName))
        {
            return;
        }

        var variables = new JsonObject
        {
            ["issueId"] = found.IssueId,
            ["repositoryId"] = found.RepositoryId,
            ["oid"] = commitOid,
            ["name"] = branchName,
        };
        await PostAsync(issue.Repository.Host, CreateMutation, variables, cancellationToken);
    }

    private async Task<LinkTarget> FindAsync(GitHubIssue issue, CancellationToken cancellationToken)
    {
        var variables = new JsonObject
        {
            ["owner"] = issue.Repository.Owner,
            ["name"] = issue.Repository.Name,
            ["number"] = issue.Number,
        };
        var data = await PostAsync(issue.Repository.Host, FindQuery, variables, cancellationToken);

        var repository = data["repository"] as JsonObject
            ?? throw new GitHubException($"GitHub could not find the repository {issue.Repository.FullName}.");
        var found = repository["issue"] as JsonObject
            ?? throw new GitHubException($"GitHub could not find the issue {issue.Repository.FullName}#{issue.Number}.");

        var linked = (found["linkedBranches"]?["nodes"] as JsonArray ?? [])
            .Select(node => node?["ref"]?["name"]?.GetValue<string>())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        return new LinkTarget(repository["id"]!.GetValue<string>(), found["id"]!.GetValue<string>(), linked);
    }

    private async Task<JsonObject> PostAsync(string host, string query, JsonObject variables, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["query"] = query, ["variables"] = variables }.ToJsonString();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new GitHubServer(host).GraphQlUri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenFor(host));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Kontext", "1.0"));

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new AuthenticationFailedException("GitHub rejected this sign-in. It may have expired or been revoked.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubException($"GitHub returned an error (HTTP {(int)response.StatusCode}) while linking the branch to the issue.");
            }

            var parsed = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;
            if (parsed?["errors"] is JsonArray { Count: > 0 } errors)
            {
                throw new GitHubException($"GitHub returned an error: {errors[0]?["message"]?.GetValue<string>()}");
            }

            return parsed?["data"] as JsonObject ?? throw new GitHubException("GitHub returned an empty answer while linking the branch to the issue.");
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubException("Could not reach GitHub. Check your internet connection.", ex);
        }
        catch (JsonException ex)
        {
            throw new GitHubException("GitHub returned an answer Kontext could not read.", ex);
        }
    }
}
