// kanban/completed/task-round-lifecycle.md WI-10, invariant B, stated there
// verbatim as:
//
//   **Invariant B — a declared result is always re-derivable.** For any
//   competition and entry set that finalises successfully, the `DeclaredResult`
//   set in the emitted `Finalised` equals, competitor for competitor, what
//   `ScoringService.ScoreCompetition` returns for the same inputs — score,
//   placing and disqualification. This is `DeclaredResult`'s own documented
//   contract (`Competition.cs`: "Answers 'what was declared', never 'what is
//   the score' … can always be re-derived and compared against what was
//   published"), turned into an executable claim.
//
// This lives in Application.Tests, not Domain.Tests, because the mapping the
// invariant is about — CompetitionResult -> DeclaredResult — is
// FinaliseCompetitionHandler's, not the aggregate's: Competition.Finalise
// receives DeclaredResults already computed. Domain.Tests can therefore not
// express it at all. The handler is driven for real against the hand-written
// fakes (LADR-0003 "Doubles"), and the re-derivation is then done
// independently by calling ScoringService.ScoreCompetition on the same folded
// inputs — so the two paths are compared, not one path with itself.
//
// A small synthetic class, following Domain.Tests's ScoringServicePropertyTests:
// the invariant is about the mapping, not about any class's rules, and a
// literal MinRounds of 1 keeps every generated shape finalisable.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Tests.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class FinaliseCompetitionPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] MetricNames = ["alpha", "bravo"];

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [.. MetricNames.Select(n => new MetricDefinition { Name = n, Kind = MeasuredKind.Number })];

    [Fact]
    public void Every_DeclaredResult_in_the_emitted_Finalised_re_derives_from_ScoreCompetition()
    {
        (from fieldSize in Gen.Int[1, 8]
         from rounds in Gen.Int[1, 3]
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var store = new FakeEventStore();
            var entryQuery = new FakeEntryQuery();
            var (competitionId, competition, entries) = Seed(store, entryQuery, t.fieldSize, t.rounds);

            var handler = new FinaliseCompetitionHandler(store, entryQuery, new FakeClock(Now));

            var result = handler
                .HandleAsync(new FinaliseCompetition(competitionId, "CD"), TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();

            result.IsSuccess.Should().BeTrue();

            var finalised = store.Streams[competitionId.Value][^1].Should().BeOfType<Finalised>().Subject;
            var declared = finalised.Finalisation.DeclaredResults;

            // Non-vacuity: every generated shape has at least one competitor,
            // so an empty declared set would mean the comparison below checked
            // nothing at all.
            declared.Should().NotBeEmpty();

            // The independent re-derivation: same Competition, same Entries,
            // straight through the engine.
            var rederived = ScoringService.ScoreCompetition(competition, entries);
            rederived.IsSuccess.Should().BeTrue();

            declared.Select(d => d.CompetitorRef.ToString())
                .Should().BeEquivalentTo(rederived.Value.Scores.Keys);

            foreach (var declaredResult in declared)
            {
                var key = declaredResult.CompetitorRef.ToString();
                var score = rederived.Value.Scores[key];

                declaredResult.Aggregate.Should().Be(score.Score);

                // Disqualification: RankingEngine excludes a disqualified
                // competitor from Placings altogether, and DeclaredResult.Placing
                // is not nullable, so a declared placing of 0 means exactly
                // "no placing was derived for this competitor".
                if (rederived.Value.Placings.TryGetValue(key, out var placing))
                {
                    declaredResult.Placing.Should().Be(placing);
                    score.Disqualified.Should().BeFalse();
                }
                else
                {
                    declaredResult.Placing.Should().Be(0);
                    score.Disqualified.Should().BeTrue();
                }
            }
        });
    }

    // ------------------------------------------------------------- helpers

    private static decimal ValueFor(string metric) => (Array.IndexOf(MetricNames, metric) + 1) * 10m;

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [.. MetricNames.Select(n => (ScoreTerm)new RateTerm { MetricRef = n, Rate = 1 })],
    };

    private static ClassDefinition MakeClassDefinition() => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks = [MakeTask()],
            },
        ],
    };

    /// <summary>
    /// Builds the whole competition and its entries through the real decide
    /// functions, appends every resulting event into the fake store (so the
    /// handler folds exactly what a real run would), and hands back the folded
    /// aggregates for the independent re-derivation.
    /// </summary>
    private static (CompetitionId Id, Competition Competition, Dictionary<EntryId, Entry> Entries) Seed(
        FakeEventStore store, FakeEntryQuery entryQuery, int fieldSize, int rounds)
    {
        var classDefinition = MakeClassDefinition();
        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };

        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Finalise Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);
        var competitionEvents = new List<IDomainEvent> { created };

        for (var i = 0; i < fieldSize; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            registered.IsSuccess.Should().BeTrue();
            competitionEvents.Add(registered.Value);
            competition = competition.Apply(registered.Value);
        }

        // task.Group is null (whole-field, one group), so DrawPhase needs no
        // parameter binding — see Competition.DrawPhase's minPerGroup default.
        var drawn = competition.DrawPhase(rounds, ImmutableArray<string>.Empty, Now);
        drawn.IsSuccess.Should().BeTrue();
        competitionEvents.Add(drawn.Value);
        competition = competition.Apply(drawn.Value);

        var entries = new Dictionary<EntryId, Entry>();

        foreach (var round in competition.Phases[0].Rounds)
        {
            var taskRound = round.TaskRounds[0];
            foreach (var group in taskRound.Groups)
            {
                foreach (var competitorRef in group.CompetitorRefs)
                {
                    var opened = competition.OpenEntry(
                        EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, Now);
                    opened.IsSuccess.Should().BeTrue();

                    var flightOpened = new FlightOpened(1, Now, Now);
                    var entryEvents = new List<IDomainEvent> { opened.Value, flightOpened };
                    var entry = Entry.Create(opened.Value).Apply(flightOpened);

                    foreach (var metric in MetricNames)
                    {
                        var captured = entry.CaptureMeasurement(
                            1, metric, MeasuredValue.Of(ValueFor(metric)), Now, MetricDefs);
                        captured.IsSuccess.Should().BeTrue();
                        entryEvents.Add(captured.Value);
                        entry = entry.Apply(captured.Value);
                    }

                    store.AppendAsync(entry.Id.Value, ExpectedVersion.NoStream, entryEvents)
                        .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

                    entryQuery.Seed(new EntrySummary(
                        entry.Id, competitionId, 0, round.Ordinal, taskRound.Ordinal,
                        group.Id, competitorRef, ReflightRole.Original));

                    entries[entry.Id] = entry;
                }
            }
        }

        // Every round completed — the CD's assertion that the scores are in,
        // which is what lets the validity gate (MinRounds 1) pass.
        foreach (var round in competition.Phases[0].Rounds)
        {
            var completed = competition.CompleteTaskRound(0, round.Ordinal, 1, Now);
            completed.IsSuccess.Should().BeTrue();
            competitionEvents.Add(completed.Value);
            competition = competition.Apply(completed.Value);
        }

        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, competitionEvents)
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();

        return (competitionId, competition, entries);
    }
}
