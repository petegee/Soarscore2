using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Domain.Competitions;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// JSON round-trips for the seven team events — kanban/in-progress/teams-mvp.md
/// WI-3, following the existing CompetitionEventJsonTests pattern byte for
/// byte: serialize as the union, assert the <c>$kind</c> discriminator,
/// deserialize, re-emit, and require byte-for-byte stability
/// (SoarscoreEventJson.Options — the single source both stores' conventions
/// copy).
/// </summary>
public class TeamsEventJsonTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private static void AssertRoundTrip(CompetitionEvent @event, string expectedKind)
    {
        var json = JsonSerializer.Serialize(@event, SoarscoreEventJson.Options);
        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);

        json.Should().Contain($"\"$kind\":\"{expectedKind}\"");
        reemitted.Should().Be(json);
        reread.Should().BeOfType(@event.GetType());
        reread.Should().Be(@event);
    }

    [Fact]
    public void ScoringTeamDefined_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new ScoringTeamDefined(
            new ScoringTeam { Id = ScoringTeamId.New(), Name = "Eagles" }, At);

        AssertRoundTrip(@event, "scoringTeamDefined");
    }

    [Fact]
    public void ScoringTeamMembershipAssigned_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new ScoringTeamMembershipAssigned(
            new ScoringTeamMembership
            {
                CompetitorRef = CompetitorId.New(),
                TeamRef = ScoringTeamId.New(),
                Contributes = false,
            },
            At);

        AssertRoundTrip(@event, "scoringTeamMembershipAssigned");
    }

    [Fact]
    public void ScoringTeamMembershipCleared_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new ScoringTeamMembershipCleared(CompetitorId.New(), At);

        AssertRoundTrip(@event, "scoringTeamMembershipCleared");
    }

    [Fact]
    public void ProtectionGroupDefined_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new ProtectionGroupDefined(
            new ProtectionGroup { Id = ProtectionGroupId.New(), Name = "Helpers" }, At);

        AssertRoundTrip(@event, "protectionGroupDefined");
    }

    [Fact]
    public void ProtectionGroupMemberAdded_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new ProtectionGroupMemberAdded(
            new ProtectionGroupMembership { CompetitorRef = CompetitorId.New(), GroupRef = ProtectionGroupId.New() },
            At);

        AssertRoundTrip(@event, "protectionGroupMemberAdded");
    }

    [Fact]
    public void ProtectionGroupMemberRemoved_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new ProtectionGroupMemberRemoved(
            CompetitorId.New(), ProtectionGroupId.New(), At);

        AssertRoundTrip(@event, "protectionGroupMemberRemoved");
    }

    [Fact]
    public void TeamClassificationConfigured_round_trips_through_SoarscoreEventJson_byte_for_byte()
    {
        CompetitionEvent @event = new TeamClassificationConfigured(
            new TeamClassificationConfiguration { Enabled = true, Method = "bestThreeScoreSum" }, At);

        AssertRoundTrip(@event, "teamClassificationConfigured");
    }

    // kanban/in-progress/teams-mvp.md WI-7 — the Finalisation shape change. The
    // declared team results ride inside the existing Finalised event, so the
    // round-trip proof is about the payload: the decimal-as-string convention
    // (LADR-0001 §4.6) reaches Total/Score through the generic converter, and
    // the typed ids survive as Guids.

    [Fact]
    public void Finalised_event_round_trips_declared_team_results_through_SoarscoreEventJson_byte_for_byte()
    {
        var contributorA = CompetitorId.New();
        var contributorB = CompetitorId.New();
        var finalisation = new Finalisation
        {
            Scope = FinalisationScope.Competition,
            Revision = 1,
            By = "CD",
            At = At,
            DeclaredResults =
            [
                new DeclaredResult
                {
                    CompetitorRef = contributorA,
                    Aggregate = 599.9999999m,
                    Placing = 1,
                    Promoted = false,
                },
            ],
            DeclaredTeamResults =
            [
                new DeclaredTeamResult
                {
                    TeamRef = ScoringTeamId.New(),
                    Name = "Eagles",
                    Total = 1099.9999999m,
                    Placing = 1,
                    Contributors =
                    [
                        new DeclaredTeamContributor { CompetitorRef = contributorA, Score = 600.0000001m, Placing = 1 },
                        new DeclaredTeamContributor { CompetitorRef = contributorB, Score = 499.9999998m, Placing = 2 },
                    ],
                    PlacingSum = 3,
                    BestIndividualPlacing = 1,
                },
            ],
        };
        CompetitionEvent finalised = new Finalised(finalisation);

        var json = JsonSerializer.Serialize(finalised, SoarscoreEventJson.Options);

        // Decimal-as-string, reached through the generic converter just like
        // DeclaredResult.Aggregate is in CompetitionEventJsonTests.
        json.Should().Contain("\"total\":\"1099.9999999\"");
        json.Should().Contain("\"score\":\"600.0000001\"");

        var reread = JsonSerializer.Deserialize<CompetitionEvent>(json, SoarscoreEventJson.Options);
        var reemitted = JsonSerializer.Serialize(reread, SoarscoreEventJson.Options);
        reemitted.Should().Be(json);

        var stored = reread.Should().BeOfType<Finalised>().Subject.Finalisation;
        stored.DeclaredTeamResults.Should().HaveCount(1);
        var declaredTeam = stored.DeclaredTeamResults[0];
        declaredTeam.TeamRef.Should().Be(finalisation.DeclaredTeamResults[0].TeamRef);
        declaredTeam.Name.Should().Be("Eagles");
        declaredTeam.Total.Should().Be(1099.9999999m);
        declaredTeam.Placing.Should().Be(1);
        declaredTeam.PlacingSum.Should().Be(3);
        declaredTeam.BestIndividualPlacing.Should().Be(1);
        declaredTeam.Contributors.Should().HaveCount(2);
        declaredTeam.Contributors[0].CompetitorRef.Should().Be(contributorA);
        declaredTeam.Contributors[0].Score.Should().Be(600.0000001m);
        declaredTeam.Contributors[0].Placing.Should().Be(1);
        declaredTeam.Contributors[1].CompetitorRef.Should().Be(contributorB);
        declaredTeam.Contributors[1].Score.Should().Be(499.9999998m);
    }
}
