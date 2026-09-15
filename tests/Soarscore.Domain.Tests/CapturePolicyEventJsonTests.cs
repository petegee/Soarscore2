using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// JSON round-trips for the capture-policy event — authentication-and-
/// authorisation.md WI-4, following the TeamsEventJsonTests pattern (itself
/// the CompetitionEventJsonTests pattern byte for byte): serialize as the
/// union, assert the <c>$kind</c> discriminator, deserialize, re-emit, and
/// require byte-for-byte stability (SoarscoreEventJson.Options — the single
/// source both stores' conventions copy). Payload equality is asserted field
/// by field, the Finalised declared-team-results precedent: record equality
/// compares the Capturers list by reference, which a deserialized array
/// cannot share.
/// </summary>
public class CapturePolicyEventJsonTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private static void AssertRoundTrip(CompetitionEvent @event, string expectedKind, Action<CompetitionEvent> assertPayload)
    {
        var json = JsonSerializer.Serialize(@event, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        json.Should().Contain($"\"$kind\":\"{expectedKind}\"");
        reemitted.Should().Be(json);
        reread.Should().BeOfType(@event.GetType());
        assertPayload(reread!);
    }

    [Fact]
    public void CapturePolicyConfigured_allow_list_round_trips_through_SoarscoreEventJson()
    {
        var capturers = new[] { PersonId.New(), PersonId.New() };
        CompetitionEvent @event = new CapturePolicyConfigured(
            new CapturePolicy(CapturePolicyMode.AllowList, capturers), At);

        AssertRoundTrip(@event, "capturePolicyConfigured", reread =>
        {
            var configured = reread.Should().BeOfType<CapturePolicyConfigured>().Subject;
            configured.Policy.Mode.Should().Be(CapturePolicyMode.AllowList);
            configured.Policy.Capturers.Should().Equal(capturers);
            configured.At.Should().Be(At);
        });
    }

    [Fact]
    public void CapturePolicyConfigured_organisers_only_round_trips_through_SoarscoreEventJson()
    {
        CompetitionEvent @event = new CapturePolicyConfigured(
            new CapturePolicy(CapturePolicyMode.OrganisersOnly, []), At);

        AssertRoundTrip(@event, "capturePolicyConfigured", reread =>
        {
            var configured = reread.Should().BeOfType<CapturePolicyConfigured>().Subject;
            configured.Policy.Mode.Should().Be(CapturePolicyMode.OrganisersOnly);
            configured.Policy.Capturers.Should().BeEmpty();
            configured.At.Should().Be(At);
        });
    }
}
