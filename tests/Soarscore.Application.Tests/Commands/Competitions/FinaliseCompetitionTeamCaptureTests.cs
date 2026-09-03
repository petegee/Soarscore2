// kanban/in-progress/teams-mvp.md WI-7. The FinaliseCompetitionHandler's team
// half: after scoring the individuals the handler runs
// TeamClassificationEngine.Classify and maps the standings into
// Finalisation.DeclaredTeamResults, so the declaration freezes the full team
// classification at the moment it was declared (owner decision 4). The method
// is FinaliseCompetitionPropertyTests's — the handler is driven for real
// against the hand-written fakes, the emitted Finalised is read off the
// stream, and the declaration is compared against an independent
// re-derivation — extended with the team seeding of
// ScoreTeamStandingsHandlerTests (real decide functions throughout, the
// Minimal pass-through task so an aggregate score is the flight time itself).

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Tests.Shared.CompetitionClasses;
using Soarscore.Application.Tests.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

using FakeClock = Soarscore.Application.Tests.Shared.Competitions.FakeClock;
using FakeEventStore = Soarscore.Application.Tests.Shared.Competitions.FakeEventStore;
using FakeEntryQuery = Soarscore.Application.Tests.Shared.Entries.FakeEntryQuery;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class FinaliseCompetitionTeamCaptureTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private const int PhaseOrdinal = 0;
    private const int RoundOrdinal = 1;
    private const int TaskRoundOrdinal = 1;

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

    /// <summary>
    /// Builds a one-team scored, finalisable competition through its decide
    /// functions and appends the emitted events: every competitor joins the one
    /// scoring team with the matching contribution flag, the classification is
    /// configured enabled iff <paramref name="classificationEnabled"/>, the
    /// whole-field phase is drawn and accepted, each competitor flies one
    /// flight, and the CD closes the single task-round.
    /// </summary>
    private static (
        FakeEventStore Store,
        FakeEntryQuery EntryQuery,
        CompetitionId CompetitionId,
        ScoringTeamId TeamId,
        ImmutableArray<CompetitorId> Competitors)
        SeedFinalisableTeamCompetition(decimal[] flightTimes, bool[] contributes, bool classificationEnabled)
    {
        flightTimes.Length.Should().Be(contributes.Length);

        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Teams Finalise Comp 2026", "Auckland",
            new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), "1", AdoptedRulesFor(), Now);

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

        if (classificationEnabled)
        {
            var configured = competition.ConfigureTeamClassification(enabled: true, by: "CD", Now);
            configured.IsSuccess.Should().BeTrue();
            competitionEvents.Add(configured.Value);
            competition = competition.Apply(configured.Value);
        }

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

        // The CD's assertion that the scores are in — finalisation's validity
        // gate (Minimal's MinRounds 1) needs the round fully flown.
        var completed = competition.CompleteTaskRound(PhaseOrdinal, RoundOrdinal, TaskRoundOrdinal, Now);
        completed.IsSuccess.Should().BeTrue();
        competitionEvents.Add(completed.Value);
        competition = competition.Apply(completed.Value);

        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, competitionEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        return (store, entryQuery, competitionId, teamId, competitors.MoveToImmutable());
    }

    [Fact]
    public async Task Finalising_a_scored_team_competition_declares_exactly_what_the_engine_derived()
    {
        // Three contributing members and one ineligible (the defending-champion
        // case): the declaration freezes the full standing — total, place,
        // contributors with scores and placings, tie-break evidence.
        var (store, entryQuery, competitionId, teamId, competitors) =
            SeedFinalisableTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false], classificationEnabled: true);

        var handler = new FinaliseCompetitionHandler(store, entryQuery, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new FinaliseCompetition(competitionId, "CD Jane"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");

        var finalised = store.Streams[competitionId.Value][^1].Should().BeOfType<Finalised>().Subject;
        var finalisation = finalised.Finalisation;

        finalisation.DeclaredTeamResults.Should().HaveCount(1);
        var declared = finalisation.DeclaredTeamResults[0];
        declared.TeamRef.Should().Be(teamId);
        declared.Name.Should().Be("Hawks");
        declared.Total.Should().Be(1500m);
        declared.Placing.Should().Be(1);
        declared.PlacingSum.Should().Be(6);
        declared.BestIndividualPlacing.Should().Be(1);
        declared.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            (competitors[0], 600m, 1), (competitors[1], 500m, 2), (competitors[2], 400m, 3));

        // The independent re-derivation, FinaliseCompetitionPropertyTests's
        // method: same Competition, same Entries, straight through the engine —
        // the two paths compared, not one path with itself.
        var loaded = await CompetitionLoader.LoadAsync(store, competitionId, TestContext.Current.CancellationToken);
        loaded.IsSuccess.Should().BeTrue();
        var entries = await EntryCollector.CollectAsync(store, entryQuery, competitionId, TestContext.Current.CancellationToken);
        entries.IsSuccess.Should().BeTrue();
        var scored = ScoringService.ScoreCompetition(loaded.Value.Competition, entries.Value);
        scored.IsSuccess.Should().BeTrue();
        var classified = TeamClassificationEngine.Classify(
            scored.Value,
            loaded.Value.Competition.ScoringTeams,
            loaded.Value.Competition.ScoringTeamMemberships,
            loaded.Value.Competition.TeamClassification);
        classified.IsSuccess.Should().BeTrue();

        var rederived = classified.Value.Standings.Should().ContainSingle().Subject;
        rederived.TeamRef.Should().Be(declared.TeamRef);
        rederived.Total.Should().Be(declared.Total);
        rederived.Placing.Should().Be(declared.Placing);
        rederived.PlacingSum.Should().Be(declared.PlacingSum);
        rederived.BestIndividualPlacing.Should().Be(declared.BestIndividualPlacing);
        rederived.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            declared.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)));
    }

    [Fact]
    public async Task Finalising_a_competition_whose_classification_is_never_configured_declares_no_team_results()
    {
        // Teams defined but the classification never switched on: the handler
        // runs no team derivation, the decide permits an empty declaration, and
        // the finalisation carries none — a state, not an error.
        var (store, entryQuery, competitionId, _, _) =
            SeedFinalisableTeamCompetition([600m, 500m, 400m, 300m], [true, true, true, false], classificationEnabled: false);

        var handler = new FinaliseCompetitionHandler(store, entryQuery, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new FinaliseCompetition(competitionId, "CD Jane"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");

        var finalised = store.Streams[competitionId.Value][^1].Should().BeOfType<Finalised>().Subject;
        finalised.Finalisation.DeclaredTeamResults.Should().BeEmpty();

        // The individual half is untouched by any of this.
        finalised.Finalisation.DeclaredResults.Should().HaveCount(4);
    }
}
