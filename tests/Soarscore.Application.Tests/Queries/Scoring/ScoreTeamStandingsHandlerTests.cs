// kanban/in-progress/teams-mvp.md WI-6/WI-7. Covers ScoreTeamStandingsHandler
// directly against a FakeEventStore + FakeEntryQuery. The handler runs the
// REAL pipeline — CompetitionLoader -> EntryCollector -> ScoringService
// .ScoreCompetition -> TeamClassificationEngine.Classify — never a stubbed
// scorer: the competition is built through its decide functions (the
// ScoreTaskRoundHandlerTests seeding precedent) and the emitted events
// appended, so the fold sees exactly what a real run would. The state
// assertions the story names: derived = null when classification is disabled
// or never configured, derived = populated (with the engine's standings) when
// enabled.
//
// WI-7 adds the declared half: the latest competition-scope finalisation's
// DeclaredTeamResults, read straight off the fold. Finalisation here is driven
// through the real FinaliseCompetitionHandler (which derives the declaration
// itself), and the divergence scenario — a scoring-team correction AFTER
// finalisation leaves the declared section frozen while derived recomputes —
// is the team-shaped twin of the TaskRoundReopened stance for individual
// results (Competition.ReopenTaskRound's doc comment).

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Application.Tests.Shared.CompetitionClasses;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;
using Soarscore.Application.Tests.Shared.Entries;
using FakeClock = Soarscore.Application.Tests.Shared.Competitions.FakeClock;
using FakeEventStore = Soarscore.Application.Tests.Shared.Competitions.FakeEventStore;
using FakeEntryQuery = Soarscore.Application.Tests.Shared.Entries.FakeEntryQuery;

namespace Soarscore.Application.Tests.Queries.Scoring;

public class ScoreTeamStandingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private const int PhaseOrdinal = 0;
    private const int RoundOrdinal = 1;
    private const int TaskRoundOrdinal = 1;

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Teams Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", AdoptedRulesFor(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static AdoptedRules AdoptedRulesFor()
    {
        var definition = ClassDefinitionFixtures.Minimal();
        return new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version!,
            AdoptedAt = Now,
        };
    }

    /// <summary>Builds a one-team scored competition through its decide
    /// functions and appends the emitted events: every competitor joins the one
    /// scoring team with the matching contribution flag, classification is
    /// configured enabled, the whole-field phase is drawn and accepted, and each
    /// competitor flies one flight (the Minimal pass-through task, so the
    /// aggregate score is the flight time itself). Returns what the query
    /// needs.</summary>
    private static (
        FakeEventStore Store,
        FakeEntryQuery EntryQuery,
        CompetitionId CompetitionId,
        ScoringTeamId TeamId,
        ImmutableArray<CompetitorId> Competitors)
        SeedScoredTeamCompetition(decimal[] flightTimes, bool[] contributes)
    {
        flightTimes.Length.Should().Be(contributes.Length);

        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Teams Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", AdoptedRulesFor(), Now);

        var competition = Competition.Create(created);
        var competitionEvents = new List<IDomainEvent> { created };

        var teamId = ScoringTeamId.New();
        var defined = competition.DefineScoringTeam(teamId, "Hawks", Now);
        defined.IsSuccess.Should().BeTrue();
        competitionEvents.Add(defined.Value);
        competition = competition.Apply(defined.Value);

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>(flightTimes.Length);
        for (var i = 0; i < flightTimes.Length; i++)
        {
            var competitorId = CompetitorId.New();
            var registered = competition.RegisterCompetitor(competitorId, PersonId.New(), Now);
            registered.IsSuccess.Should().BeTrue();
            competitionEvents.Add(registered.Value);
            competition = competition.Apply(registered.Value);

            var assigned = competition.AssignScoringTeamMembership(competitorId, teamId, contributes[i], Now);
            assigned.IsSuccess.Should().BeTrue();
            competitionEvents.Add(assigned.Value);
            competition = competition.Apply(assigned.Value);

            competitors.Add(competitorId);
        }

        var configured = competition.ConfigureTeamClassification(enabled: true, by: "CD", Now);
        configured.IsSuccess.Should().BeTrue();
        competitionEvents.Add(configured.Value);
        competition = competition.Apply(configured.Value);

        // task.Group is null (whole-field, one group), so DrawPhase needs no
        // parameter binding — see Competition.DrawPhase's minPerGroup default.
        var drawn = competition.DrawPhase(1, ImmutableArray<string>.Empty, Now);
        drawn.IsSuccess.Should().BeTrue();
        competitionEvents.Add(drawn.Value);
        competition = competition.Apply(drawn.Value);

        // Entries open only against an accepted draw (D4).
        var accepted = competition.AcceptDraw(Now);
        accepted.IsSuccess.Should().BeTrue();
        competitionEvents.Add(accepted.Value);
        competition = competition.Apply(accepted.Value);

        var group = competition.Phases[PhaseOrdinal].Rounds[0].TaskRounds[0].Groups.Single();
        var taskDefinition = competition.AdoptedRules.Definition.Phases[0].Tasks[0];

        for (var i = 0; i < flightTimes.Length; i++)
        {
            // Indexed by the competitors array, not the drawn group order: the
            // whole-field group holds everyone, and this pairing is what fixes
            // which competitor flew which time.
            var opened = competition.OpenEntry(
                EntryId.New(), PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
                group.Id, competitors[i], ReflightRole.Original, Now);
            opened.IsSuccess.Should().BeTrue();

            var entry = Entry.Create(opened.Value);

            var flightOpened = entry.OpenFlight(1, null, Now);
            flightOpened.IsSuccess.Should().BeTrue();
            var entryEvents = new List<IDomainEvent> { opened.Value, flightOpened.Value };
            entry = entry.Apply(flightOpened.Value);

            var captured = entry.CaptureMeasurement(
                1, "flightTime", MeasuredValue.Of(flightTimes[i]), Now, taskDefinition.Metrics);
            captured.IsSuccess.Should().BeTrue();
            entryEvents.Add(captured.Value);
            entry = entry.Apply(captured.Value);

            store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents)
                .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

            entryQuery.Seed(new EntrySummary(
                entry.Id, competitionId, PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal,
                group.Id, competitors[i], ReflightRole.Original));
        }

        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, competitionEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        return (store, entryQuery, competitionId, teamId, competitors.MoveToImmutable());
    }

    private static async Task<Result<TeamStandingsView>> QueryStandings(
        FakeEventStore store, FakeEntryQuery entryQuery, CompetitionId competitionId)
    {
        var handler = new ScoreTeamStandingsHandler(store, entryQuery);
        return await handler.HandleAsync(
            new ScoreTeamStandings(competitionId), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Closes the one task-round the seeding drew — the CD's assertion that the
    /// scores are in, which is what lets the validity gate (Minimal's MinRounds
    /// 1) pass and finalisation become possible.
    /// </summary>
    private static void CompleteTheSingleRound(FakeEventStore store, CompetitionId competitionId)
    {
        var version = store.Streams[competitionId.Value].Count;
        store.AppendAsync(
                competitionId.Value, ExpectedVersion.Exact(version),
                [new TaskRoundCompleted(PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, Now)])
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();
    }

    private static async Task FinaliseAsync(FakeEventStore store, FakeEntryQuery entryQuery, CompetitionId competitionId)
    {
        var handler = new FinaliseCompetitionHandler(store, entryQuery, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new FinaliseCompetition(competitionId, "CD Jane"), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
    }

    /// <summary>
    /// The declared team result the seeding's finalisation must have frozen:
    /// Hawks' three contributors (600 + 500 + 400), the fourth member
    /// ineligible — the exact shape the engine derives from the captured
    /// flights. Field-by-field, not record equality: DeclaredTeamResult nests
    /// an ImmutableArray, whose equality is reference-based.
    /// </summary>
    private static void AssertIsTheSeededHawksStanding(DeclaredTeamResult declaredTeam, ScoringTeamId teamId, ImmutableArray<CompetitorId> competitors)
    {
        declaredTeam.TeamRef.Should().Be(teamId);
        declaredTeam.Name.Should().Be("Hawks");
        declaredTeam.Total.Should().Be(1500m);
        declaredTeam.Placing.Should().Be(1);
        declaredTeam.PlacingSum.Should().Be(6);
        declaredTeam.BestIndividualPlacing.Should().Be(1);
        declaredTeam.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            (competitors[0], 600m, 1), (competitors[1], 500m, 2), (competitors[2], 400m, 3));
    }

    [Fact]
    public async Task A_never_configured_classification_returns_derived_null()
    {
        var (store, competitionId) = SeedCompetition();

        var result = await QueryStandings(store, new FakeEntryQuery(), competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().BeNull();
    }

    [Fact]
    public async Task A_disabled_classification_returns_derived_null()
    {
        var (store, competitionId) = SeedCompetition();
        var configure = new ConfigureTeamClassificationHandler(store, new FakeClock(Now));
        (await configure.HandleAsync(
            new ConfigureTeamClassification(competitionId, Enabled: false, By: "CD"),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        var result = await QueryStandings(store, new FakeEntryQuery(), competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().BeNull();
    }

    [Fact]
    public async Task An_enabled_classification_with_no_teams_defined_returns_derived_with_empty_standings()
    {
        var (store, competitionId) = SeedCompetition();
        var configure = new ConfigureTeamClassificationHandler(store, new FakeClock(Now));
        (await configure.HandleAsync(
            new ConfigureTeamClassification(competitionId, Enabled: true, By: "CD"),
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        var result = await QueryStandings(store, new FakeEntryQuery(), competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().NotBeNull();
        var derived = result.Value.Derived!;
        derived.Standings.Should().BeEmpty();
        derived.Method.Should().Be(TeamClassificationEngine.MethodBestThreeScoreSum);
        derived.SourceClassification.Should().Be(TeamClassificationEngine.SourceCompetitionFinalAggregate);
    }

    [Fact]
    public async Task An_enabled_classification_with_a_scored_team_derives_its_standings()
    {
        // Three contributing members and one ineligible (the defending-champion
        // case): the total counts only the three, the fourth reads Ineligible.
        var (store, entryQuery, competitionId, _, competitors) =
            SeedScoredTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false]);

        var result = await QueryStandings(store, entryQuery, competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().NotBeNull();
        var derived = result.Value.Derived!;

        var standing = derived.Standings.Should().ContainSingle().Subject;
        standing.Name.Should().Be("Hawks");
        standing.Placing.Should().Be(1);
        standing.Total.Should().Be(1500m);
        standing.PlacingSum.Should().Be(6);
        standing.BestIndividualPlacing.Should().Be(1);

        standing.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            (competitors[0], 600m, 1), (competitors[1], 500m, 2), (competitors[2], 400m, 3));

        standing.Members.Should().HaveCount(4);
        for (var i = 0; i < 3; i++)
        {
            var member = standing.Members.Single(m => m.CompetitorRef == competitors[i]);
            member.State.Should().Be(TeamContributionState.Contributor);
        }

        standing.Members.Single(m => m.CompetitorRef == competitors[3]).State
            .Should().Be(TeamContributionState.Ineligible);
    }

    [Fact]
    public async Task Standings_against_an_unknown_competition_fail_with_competition_notFound()
    {
        var handler = new ScoreTeamStandingsHandler(new FakeEventStore(), new FakeEntryQuery());

        var result = await handler.HandleAsync(
            new ScoreTeamStandings(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    // ----------------------------------------------------- declared (teams-mvp.md WI-7)

    [Fact]
    public async Task A_competition_with_no_competition_scope_finalisation_returns_declared_null()
    {
        var (store, entryQuery, competitionId, _, _) =
            SeedScoredTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false]);

        var result = await QueryStandings(store, entryQuery, competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().NotBeNull();
        result.Value.Declared.Should().BeNull();
    }

    [Fact]
    public async Task The_declared_section_equals_the_derived_standings_at_finalisation()
    {
        var (store, entryQuery, competitionId, teamId, competitors) =
            SeedScoredTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false]);
        CompleteTheSingleRound(store, competitionId);
        await FinaliseAsync(store, entryQuery, competitionId);

        var result = await QueryStandings(store, entryQuery, competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().NotBeNull();
        result.Value.Declared.Should().NotBeNull();

        // Nothing has changed since finalisation, so the frozen declaration is
        // exactly what the engine re-derives now — the comparison the paper
        // asks the declared-vs-derived read to make.
        var declared = result.Value.Declared!.Value.Should().ContainSingle().Subject;
        AssertIsTheSeededHawksStanding(declared, teamId, competitors);

        var derived = result.Value.Derived!.Standings.Should().ContainSingle().Subject;
        derived.TeamRef.Should().Be(declared.TeamRef);
        derived.Total.Should().Be(declared.Total);
        derived.Placing.Should().Be(declared.Placing);
        derived.PlacingSum.Should().Be(declared.PlacingSum);
        derived.BestIndividualPlacing.Should().Be(declared.BestIndividualPlacing);
        derived.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            declared.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)));
    }

    [Fact]
    public async Task Declared_stays_frozen_while_derived_recomputes_after_a_post_finalisation_scoring_team_correction()
    {
        // The teams-mvp.md consequence note: scoring-team corrections after
        // finalisation are allowed (explicit, auditable events, no gate), and
        // the declaration is NOT retroactively mutated — the divergence becomes
        // visible right here. Clearing the top contributor's membership drops
        // the derived total from 1500 (600+500+400) to 900 (500+400: the fourth
        // member is ineligible), while the declared section still shows what
        // was declared — the team-shaped twin of the TaskRoundReopened stance.
        var (store, entryQuery, competitionId, teamId, competitors) =
            SeedScoredTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false]);
        CompleteTheSingleRound(store, competitionId);
        await FinaliseAsync(store, entryQuery, competitionId);

        var cleared = await new ClearScoringTeamMembershipHandler(store, new FakeClock(Now))
            .HandleAsync(new ClearScoringTeamMembership(competitionId, competitors[0]), TestContext.Current.CancellationToken);
        cleared.IsSuccess.Should().BeTrue($"{cleared.Code}: {cleared.Message}");

        var result = await QueryStandings(store, entryQuery, competitionId);

        result.IsSuccess.Should().BeTrue();

        // Derived: recomputed from the corrected memberships.
        var derived = result.Value.Derived!.Standings.Should().ContainSingle().Subject;
        derived.TeamRef.Should().Be(teamId);
        derived.Total.Should().Be(900m);
        derived.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            (competitors[1], 500m, 2), (competitors[2], 400m, 3));

        // Declared: frozen at finalisation, untouched by the correction.
        result.Value.Declared.Should().NotBeNull();
        var declared = result.Value.Declared!.Value.Should().ContainSingle().Subject;
        AssertIsTheSeededHawksStanding(declared, teamId, competitors);
    }

    [Fact]
    public async Task A_finalisation_is_surfaced_even_when_the_classification_has_since_been_disabled()
    {
        // The declared section is read off the fold before the enabled check,
        // so reconfiguring the classification after finalisation hides the
        // derived standings (a state, not an error) without ever hiding what
        // was declared — frozen data does not depend on what derived work is
        // possible today.
        var (store, entryQuery, competitionId, teamId, competitors) =
            SeedScoredTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false]);
        CompleteTheSingleRound(store, competitionId);
        await FinaliseAsync(store, entryQuery, competitionId);

        var disabled = await new ConfigureTeamClassificationHandler(store, new FakeClock(Now))
            .HandleAsync(new ConfigureTeamClassification(competitionId, Enabled: false, By: "CD"), TestContext.Current.CancellationToken);
        disabled.IsSuccess.Should().BeTrue($"{disabled.Code}: {disabled.Message}");

        var result = await QueryStandings(store, entryQuery, competitionId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Derived.Should().BeNull();
        result.Value.Declared.Should().NotBeNull();
        AssertIsTheSeededHawksStanding(result.Value.Declared!.Value[0], teamId, competitors);
    }
}
