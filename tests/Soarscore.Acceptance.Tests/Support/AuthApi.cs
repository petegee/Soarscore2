// authentication-and-authorisation.md WI-10 — the HTTP surface the four auth
// features drive: the auth-mode factory's client with a bearer token per
// request (persona tokens never go on DefaultRequestHeaders — the client is
// shared run-wide). Every call lazily ensures the mock-mode factory exists.
// Bodies and reads use ApiClient.Options — the same serialiser options the
// server uses, so ids/enums round-trip identically.

using System.Net.Http.Json;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;

namespace Soarscore.Acceptance.Tests.Support;

public static class AuthApi
{
    public static async Task<HttpResponseMessage> PostAsync(string bearer, string path, object command)
    {
        await AuthAcceptanceFixture.EnsureInitializedAsync();
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(command, command.GetType(), ApiClient.Options);
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(json) { Headers = { ContentType = new("application/json") } },
        };
        request.Headers.Authorization = new("Bearer", bearer);
        return await AuthAcceptanceFixture.Client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PostAnonymousAsync(string path, object command)
    {
        await AuthAcceptanceFixture.EnsureInitializedAsync();
        return await AuthAcceptanceFixture.Client.PostAsJsonAsync(path, command, ApiClient.Options);
    }


    public static async Task<HttpResponseMessage> GetAsync(string bearer, string url)
    {
        await AuthAcceptanceFixture.EnsureInitializedAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", bearer);
        return await AuthAcceptanceFixture.Client.SendAsync(request);
    }

    /// <summary>
    /// The bearer twin of ApiClient.PostCommandAsync: POST a command, unwrap
    /// the WI-2 {value, warnings} envelope when one is present, and throw with
    /// the body when the endpoint refuses (use the Raw/ProblemTitle pair for
    /// refusals the feature must assert on).
    /// </summary>
    public static async Task<T> PostCommandAsync<T>(string bearer, string path, object command)
    {
        var response = await PostAsync(bearer, path, command);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{path} returned {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var body = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(body);
        return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
            && document.RootElement.TryGetProperty("warnings", out var warningsElement)
                ? System.Text.Json.JsonSerializer.Deserialize<T>(
                    document.RootElement.GetProperty("value").GetRawText(), ApiClient.Options)!
                : System.Text.Json.JsonSerializer.Deserialize<T>(body, ApiClient.Options)!;
    }

    /// <summary>
    /// The typed read of a 200 body, through the server's own options. Reads
    /// the body as a string first — ReadAsStringAsync caches, so a response a
    /// step already inspected can be read again by its Then.
    /// </summary>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), ApiClient.Options)!;

    /// <summary>The ProblemDetails title — where the refusal code surfaces (the house pattern).</summary>
    public static async Task<string> ProblemTitleAsync(HttpResponseMessage response)
    {
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("title").GetString()!;
    }

    /// <summary>
    /// The group id for (round, taskRound 1, group) of the scenario's
    /// competition — the same resolution CapturingAScoreSteps performs, minus
    /// the caching this feature's short scenarios do not need.
    /// </summary>
    public static async Task<GroupId> ResolveGroupIdAsync(string bearer, Guid competitionId, int roundOrdinal, int groupOrdinal)
    {
        var view = await ReadAsync<CompetitionView>(await GetAsync(bearer, $"/competition?id={competitionId}"));
        var phase = view.Competition.Phases.Single();
        var round = phase.Rounds.Single(r => r.Ordinal == roundOrdinal);
        return round.TaskRounds.Single().Groups.Single(g => g.Ordinal == groupOrdinal).Id;
    }
}
