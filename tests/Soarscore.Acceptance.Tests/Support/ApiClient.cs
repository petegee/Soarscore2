// docs/plans/capture-a-score-steel-thread-plan.md WI-13. A thin wrapper over
// HttpClient/System.Net.Http.Json so every step definition serialises and
// deserialises with the SAME JsonSerializerOptions the real Api uses for
// every request and response — Composition.cs's ConfigureHttpJsonOptions
// installs ClassDefinitionIngestion.Options's enum/NumberOrParam/FlagOrParam
// converters onto the app's global JSON options, not onto one endpoint, so a
// client using anything else (plain camelCase STJ defaults, say) would
// disagree with the server on how a MeasuredKind or a ClassDefinition's
// polymorphic ScoreTerm hierarchy round-trips.

using System.Net.Http.Json;
using Soarscore.Application.CompetitionClasses;

namespace Soarscore.Acceptance.Tests.Support;

public static class ApiClient
{
    /// <summary>What every request body is written with and every response body is read with — see this file's header.</summary>
    public static System.Text.Json.JsonSerializerOptions Options => ClassDefinitionIngestion.Options;

    public static async Task<TResult> PostCommandAsync<TResult>(HttpClient client, string path, object command)
    {
        var response = await client.PostAsJsonAsync(path, command, Options);
        await EnsureSuccessAsync(response, path);
        return (await response.Content.ReadFromJsonAsync<TResult>(Options))!;
    }

    public static async Task<HttpResponseMessage> PostCommandRawAsync(HttpClient client, string path, object command) =>
        await client.PostAsJsonAsync(path, command, Options);

    public static async Task<TResult> GetAsync<TResult>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await EnsureSuccessAsync(response, url);
        return (await response.Content.ReadFromJsonAsync<TResult>(Options))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string path)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"{path} returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }
}
