// ui_link-sign-in-empty-body.md acceptance: behaviour and docs agree that
// POST /link-sign-in takes no body. The guide says "No body" while an empty
// body 400s ("Implicit body inferred … no body was provided"); /link-sign-in
// is now mapped with MapBodilessCommand, so both a missing body and {}
// reach the handler. These facts pin that over real HTTP against the
// mock-mode factory, plus the backstop: a missing body on a command that
// requires one still 400s.

using AwesomeAssertions;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Acceptance.Tests;

public sealed class LinkSignInEmptyBodyTests
{
    private sealed record LinkSignInView(PersonId PersonId);

    private static async Task<HttpResponseMessage> PostLinkSignInAsync(string token, HttpContent? content)
    {
        await AuthAcceptanceFixture.EnsureInitializedAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/link-sign-in")
        {
            Content = content,
        };
        request.Headers.Authorization = new("Bearer", token);
        return await AuthAcceptanceFixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Empty_body_signs_in_exactly_like_an_empty_object()
    {
        var id = Guid.NewGuid().ToString("N");
        var token = TestJwt.ForIdentity($"mock|bodiless-{id}", $"bodiless-{id}@example.org", "Bodiless");

        using var empty = await PostLinkSignInAsync(token, content: null);
        empty.EnsureSuccessStatusCode();
        var created = (await AuthApi.ReadAsync<LinkSignInView>(empty)).PersonId;

        using var emptyObject = await AuthApi.PostAsync(token, "/link-sign-in", new { });
        emptyObject.EnsureSuccessStatusCode();
        var again = (await AuthApi.ReadAsync<LinkSignInView>(emptyObject)).PersonId;

        // Same person both ways — the empty body created via arm 3, {} resolved via arm 1.
        again.Should().Be(created);
    }

    [Fact]
    public async Task Empty_body_on_a_command_requiring_one_still_400s()
    {
        await AuthAcceptanceFixture.EnsureInitializedAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/register-person");
        request.Headers.Authorization = new("Bearer", TestJwt.ForPerson(AuthActors.Organiser));
        using var response = await AuthAcceptanceFixture.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
