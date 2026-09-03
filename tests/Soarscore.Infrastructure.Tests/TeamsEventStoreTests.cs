// kanban/in-progress/teams-mvp.md WI-3 — the store-backed proof of the seven
// team events: registration (the alias table line is what makes each event
// readable on EITHER backend — a missing line fails at runtime on both per
// LADR-0001 §4.8) plus replay round-trip through the Competition fold.
//
// Written ONCE against IStoreFixture so the file runs unchanged against every
// backend Soarscore supports — Marten/PostgreSQL and Fisher/SQLite — one
// concrete subclass per backend at the foot of the file; only the Postgres
// subclass keeps Trait("Category", "Storage") (EventStoreTests.cs's header
// says why).
//
// Appends go straight through IEventStore — teams-mvp.md WI-6 owns the
// command handlers, so no dispatcher is wired here yet; the events are
// exactly the seven the decide functions emit, and the replay half folds the
// read stream through the aggregate's generic Apply, asserting the fold
// contract (replacement, clear, remove-filter, last-wins) survives the real
// store's serialisation.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class TeamsEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly DateTimeOffset At = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CompetitionCreated SampleCreatedEvent() =>
        new(
            CompetitionId.New(),
            "Teams Store Test Comp",
            "Taupo",
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 3),
            "1.0.0",
            new AdoptedRules
            {
                Definition = F5JDefinition,
                SourceClassId = "content-hash-abc123",
                SourceVersion = F5JDefinition.Version,
                AdoptedAt = At,
            },
            At);

    private async Task AppendAsync(Guid streamId, ExpectedVersion expected, params IDomainEvent[] events)
    {
        var appended = await fixture.EventStore.AppendAsync(streamId, expected, events, Ct);
        appended.IsSuccess.Should().BeTrue($"{appended.Code}: {appended.Message}");
    }

    private async Task<IReadOnlyList<IDomainEvent>> ReadAsync(Guid streamId)
    {
        var read = await fixture.EventStore.ReadStreamAsync(streamId, 0, Ct);
        read.IsSuccess.Should().BeTrue();
        return read.Value;
    }

    private static Competition Fold(IReadOnlyList<IDomainEvent> stream) =>
        stream.Cast<CompetitionEvent>().Aggregate((Competition?)null, Competition.Apply)
        ?? throw new InvalidOperationException("The stream did not fold to a Competition.");

    [Fact]
    public async Task All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state()
    {
        var team = new ScoringTeam { Id = ScoringTeamId.New(), Name = "Eagles" };
        var group = new ProtectionGroup { Id = ProtectionGroupId.New(), Name = "Helpers" };
        var competitorA = CompetitorId.New();
        var competitorB = CompetitorId.New();
        var configuration = new TeamClassificationConfiguration { Enabled = true, Method = "bestThreeScoreSum" };

        CompetitionEvent[] teamEvents =
        [
            new ScoringTeamDefined(team, At),
            new ScoringTeamMembershipAssigned(
                new ScoringTeamMembership { CompetitorRef = competitorA, TeamRef = team.Id, Contributes = true }, At),
            new ScoringTeamMembershipAssigned(
                new ScoringTeamMembership { CompetitorRef = competitorB, TeamRef = team.Id, Contributes = false }, At),
            new ScoringTeamMembershipCleared(competitorB, At.AddMinutes(1)),
            new ProtectionGroupDefined(group, At),
            new ProtectionGroupMemberAdded(
                new ProtectionGroupMembership { CompetitorRef = competitorA, GroupRef = group.Id }, At),
            new ProtectionGroupMemberRemoved(competitorA, group.Id, At.AddMinutes(2)),
            new TeamClassificationConfigured(configuration, At),
        ];

        var streamId = CompetitionId.New().Value;
        IDomainEvent[] stream = [SampleCreatedEvent(), .. teamEvents];
        await AppendAsync(streamId, ExpectedVersion.NoStream, stream);

        var stored = await ReadAsync(streamId);

        stored.Count.Should().Be(9); // created + the seven team kinds
        stored.OfType<ScoringTeamDefined>().Single().Team.Should().Be(team);
        stored.OfType<ScoringTeamMembershipCleared>().Single().CompetitorRef.Should().Be(competitorB);
        stored.OfType<ProtectionGroupMemberRemoved>().Single().GroupRef.Should().Be(group.Id);
        stored.OfType<TeamClassificationConfigured>().Single().Configuration.Should().Be(configuration);

        // Replay through the aggregate fold: the state that comes back from
        // the store is the state the events describe — membership cleared,
        // protection membership removed, configuration last-wins.
        var replayed = Fold(stored);
        replayed.ScoringTeams.Should().ContainSingle().Which.Id.Should().Be(team.Id);
        replayed.ProtectionGroups.Should().ContainSingle().Which.Id.Should().Be(group.Id);
        replayed.ScoringTeamMemberships.Should().ContainSingle().Which.CompetitorRef.Should().Be(competitorA);
        replayed.ProtectionGroupMemberships.Should().BeEmpty();
        replayed.TeamClassification.Should().Be(configuration);
    }

    [Fact]
    public async Task A_reassigned_membership_replays_as_a_single_replaced_record()
    {
        var team = new ScoringTeam { Id = ScoringTeamId.New(), Name = "Eagles" };
        var competitor = CompetitorId.New();

        var streamId = CompetitionId.New().Value;
        await AppendAsync(
            streamId, ExpectedVersion.NoStream,
            SampleCreatedEvent(),
            new ScoringTeamDefined(team, At),
            new ScoringTeamMembershipAssigned(
                new ScoringTeamMembership { CompetitorRef = competitor, TeamRef = team.Id, Contributes = true }, At));
        await AppendAsync(
            streamId, ExpectedVersion.Exact(3),
            new ScoringTeamMembershipAssigned(
                new ScoringTeamMembership { CompetitorRef = competitor, TeamRef = team.Id, Contributes = false }, At));

        var stored = await ReadAsync(streamId);

        stored.Count.Should().Be(4);
        stored.OfType<ScoringTeamMembershipAssigned>().Should().HaveCount(2);

        var replayed = Fold(stored);
        replayed.ScoringTeamMemberships.Should().ContainSingle();
        replayed.ScoringTeamMemberships[0].Contributes.Should().BeFalse();
    }

    // --------------------------------------------------- finalisation (teams-mvp.md WI-7)
    //
    // Finalisation.DeclaredTeamResults rides inside the existing Finalised
    // event (already registered in SoarscoreEventTypes on both backends), so
    // what a real store can break is the payload's serialisation — nested
    // ImmutableArrays, the decimal-as-string convention (LADR-0001 §4.6),
    // Guid-typed ids — and the fold that reads it back. Comparisons are
    // field-by-field: DeclaredTeamResult nests an ImmutableArray, whose
    // equality is reference-based, so record equality would be false even for
    // identical content.

    private static Finalisation SampleFinalisationWithTeamResults()
    {
        var contributorA = CompetitorId.New();
        var contributorB = CompetitorId.New();

        return new Finalisation
        {
            Scope = FinalisationScope.Competition,
            Revision = 1,
            By = "CD Jane",
            At = At,
            DeclaredResults =
            [
                new DeclaredResult { CompetitorRef = contributorA, Aggregate = 600.0000001m, Placing = 1, Promoted = false },
                new DeclaredResult { CompetitorRef = contributorB, Aggregate = 499.9999999m, Placing = 2, Promoted = false },
            ],
            DeclaredTeamResults =
            [
                new DeclaredTeamResult
                {
                    TeamRef = ScoringTeamId.New(),
                    Name = "Eagles",
                    Total = 1100m,
                    Placing = 1,
                    Contributors =
                    [
                        new DeclaredTeamContributor { CompetitorRef = contributorA, Score = 600.0000001m, Placing = 1 },
                        new DeclaredTeamContributor { CompetitorRef = contributorB, Score = 499.9999999m, Placing = 2 },
                    ],
                    PlacingSum = 3,
                    BestIndividualPlacing = 1,
                },
            ],
        };
    }

    private static void AssertDeclaredTeamResultSurvived(DeclaredTeamResult stored, DeclaredTeamResult expected)
    {
        stored.TeamRef.Should().Be(expected.TeamRef);
        stored.Name.Should().Be(expected.Name);
        stored.Total.Should().Be(expected.Total);
        stored.Placing.Should().Be(expected.Placing);
        stored.PlacingSum.Should().Be(expected.PlacingSum);
        stored.BestIndividualPlacing.Should().Be(expected.BestIndividualPlacing);
        stored.Contributors.Should().HaveCount(expected.Contributors.Length);
        for (var i = 0; i < expected.Contributors.Length; i++)
        {
            stored.Contributors[i].CompetitorRef.Should().Be(expected.Contributors[i].CompetitorRef);
            stored.Contributors[i].Score.Should().Be(expected.Contributors[i].Score);
            stored.Contributors[i].Placing.Should().Be(expected.Contributors[i].Placing);
        }
    }

    [Fact]
    public async Task A_finalisation_carrying_declared_team_results_round_trips_through_the_real_store_and_replays()
    {
        var finalisation = SampleFinalisationWithTeamResults();

        var streamId = CompetitionId.New().Value;
        await AppendAsync(streamId, ExpectedVersion.NoStream, SampleCreatedEvent(), new Finalised(finalisation));

        var stored = await ReadAsync(streamId);

        stored.Count.Should().Be(2);
        var storedFinalisation = stored.OfType<Finalised>().Single().Finalisation;
        AssertDeclaredTeamResultSurvived(storedFinalisation.DeclaredTeamResults.Single(), finalisation.DeclaredTeamResults[0]);

        // Replay through the aggregate fold: the state that comes back from
        // the store is the state the events describe.
        var replayed = Fold(stored);
        replayed.Finalisations.Should().ContainSingle();
        AssertDeclaredTeamResultSurvived(replayed.Finalisations[0].DeclaredTeamResults.Single(), finalisation.DeclaredTeamResults[0]);
    }

    [Fact]
    public async Task The_standings_query_surfaces_the_round_tripped_declared_section_from_the_real_store()
    {
        // The declared section is read straight off the fold, so a store-level
        // proof needs no scored competition: with the classification never
        // configured the query answers Derived = null (a state) while still
        // surfacing the frozen declaration — the exact shape WI-7's divergence
        // read depends on after any correction.
        var finalisation = SampleFinalisationWithTeamResults();

        var competitionId = CompetitionId.New();
        await AppendAsync(competitionId.Value, ExpectedVersion.NoStream, SampleCreatedEvent(), new Finalised(finalisation));

        var result = await new ScoreTeamStandingsHandler(fixture.EventStore, fixture.EntryQuery)
            .HandleAsync(new ScoreTeamStandings(competitionId), Ct);

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
        result.Value.Derived.Should().BeNull();
        result.Value.Declared.Should().NotBeNull();
        AssertDeclaredTeamResultSurvived(result.Value.Declared!.Value.Single(), finalisation.DeclaredTeamResults[0]);
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresTeamsEventStoreTests(PostgresFixture fixture)
    : TeamsEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteTeamsEventStoreTests(SqliteFixture fixture)
    : TeamsEventStoreTests<SqliteFixture>(fixture);
