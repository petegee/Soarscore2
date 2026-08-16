using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property tests for <see cref="ScoringService"/>'s two orchestration-level
/// invariants — WI-5 invariants 2 and 3 (kanban/completed/scoring-steel-thread-plan.md).
/// A small synthetic class/task, not a corpus one: these invariants are about
/// ScoringService's own orchestration (fold-order independence, purity), not
/// about any particular class's rules, so a minimal fixture is what isolates
/// them — ScoringCorpusPropertyTests (invariant 7) is where the real corpus
/// classes get exercised.
/// </summary>
public class ScoringServicePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] MetricNames = ["alpha", "bravo", "charlie", "delta"];

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [.. MetricNames.Select(n => new MetricDefinition { Name = n, Kind = MeasuredKind.Number })];

    // One RateTerm per metric, rate 1 — the raw score is just the sum of the
    // captured values, so a fold-order bug would show up directly as a wrong
    // total rather than needing a more elaborate oracle.
    private static readonly ImmutableArray<ScoreTerm> ScoreTerms =
        [.. MetricNames.Select(n => (ScoreTerm)new RateTerm { MetricRef = n, Rate = 1 })];

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = ScoreTerms,
    };

    private static ClassDefinition MakeClassDefinition(TaskDefinition task) => new()
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
                Tasks = [task],
            },
        ],
    };

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();

    private static Entry OpenSampleEntry(CompetitorId competitorRef, GroupId groupRef) =>
        Entry.Create(new EntryOpened(
            EntryId.New(),
            new TimeWindow { Start = Now, End = Now.AddSeconds(600) },
            CompetitionId.New(), 0, 1, 1,
            groupRef, competitorRef, ReflightRole.Original, Now));

    private static decimal ValueFor(string metric) => (Array.IndexOf(MetricNames, metric) + 1) * 10m;

    // ============================================================ invariant 2

    /// <summary>
    /// Scoring is order-invariant over distinct metrics: folding the same set
    /// of MeasurementCaptured events for an Entry, in any permutation, yields
    /// an identical GroupResult through ScoreGroup — the property that says
    /// scoring reads the fold (the final Entry state), not the event order.
    /// </summary>
    [Fact]
    public void Scoring_is_order_invariant_over_distinct_metrics()
    {
        (from permutationA in Gen.Shuffle(MetricNames)
         from permutationB in Gen.Shuffle(MetricNames)
         select (permutationA, permutationB))
        .Sample(t =>
        {
            var resultA = ScoreWithCaptureOrder(t.permutationA);
            var resultB = ScoreWithCaptureOrder(t.permutationB);

            resultA.State.Should().Be(resultB.State);
            resultA.RawScore.Should().Be(resultB.RawScore);
        });
    }

    private static TaskResult ScoreWithCaptureOrder(string[] captureOrder)
    {
        var groupRef = GroupId.New();
        var competitorRef = CompetitorId.New();

        var entry = OpenSampleEntry(competitorRef, groupRef)
            .Apply(new FlightOpened(1, Now, Now));

        foreach (var metric in captureOrder)
        {
            var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(ValueFor(metric)), Now, MetricDefs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }

        var task = MakeTask();
        var classDef = MakeClassDefinition(task);
        var entries = ImmutableDictionary<string, Entry>.Empty.Add(competitorRef.ToString(), entry);

        var group = ScoringService.ScoreGroup(groupRef.ToString(), task, classDef, entries, EmptyBindings);
        return group.Results[competitorRef.ToString()];
    }

    // ============================================================ invariant 3

    /// <summary>
    /// Scoring is a pure function: scoring the same Competition + entries
    /// twice through ScoreCompetition yields equal results. This is what
    /// makes LADR-0001 §3's "results are derived, never stored" safe — if
    /// scoring were not pure, re-deriving a leaderboard on demand could
    /// silently drift between reads of the same event log.
    /// </summary>
    [Fact]
    public void Scoring_is_a_pure_function()
    {
        (from fieldSize in Gen.Int[1, 8]
         from rounds in Gen.Int[1, 2]
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var (competition, entries) = BuildScoredCompetition(t.fieldSize, t.rounds);

            var first = ScoringService.ScoreCompetition(competition, entries);
            var second = ScoringService.ScoreCompetition(competition, entries);

            first.IsSuccess.Should().BeTrue();
            second.IsSuccess.Should().BeTrue();
            second.Value.Scores.Should().BeEquivalentTo(first.Value.Scores);
            second.Value.Placings.Should().BeEquivalentTo(first.Value.Placings);
        });
    }

    private static (Competition Competition, Dictionary<EntryId, Entry> Entries) BuildScoredCompetition(
        int fieldSize, int rounds)
    {
        var task = MakeTask();
        var classDefinition = MakeClassDefinition(task);

        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Purity Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < fieldSize; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        // task.Group is null (whole-field, one group), so DrawPhase needs no
        // parameter binding — see Competition.DrawPhase's minPerGroup default.
        var drawn = competition.DrawPhase(rounds, [], Now);
        drawn.IsSuccess.Should().BeTrue();
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

                    var entry = Entry.Create(opened.Value).Apply(new FlightOpened(1, Now, Now));

                    foreach (var metric in MetricNames)
                    {
                        var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(ValueFor(metric)), Now, MetricDefs);
                        entry = entry.Apply(captured.Value);
                    }

                    entries[entry.Id] = entry;
                }
            }
        }

        return (competition, entries);
    }
}
