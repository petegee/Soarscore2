// Group-isolation of normalisation — kanban/completed/multi-group-normalisation-coverage.md.
//
// The invariant these tests name: a competitor's normalised score depends only
// on their OWN group's raw scores. Perturbing any other group in the same
// round must leave it untouched, and each group must crown its own winner on
// the normalisation target.
//
// Stated at ScoringService.ScoreCompetition, deliberately, not at
// NormalisationEngine: the engine is handed one group's results and cannot
// see another group even in principle, so a property there would be true by
// construction and would prove nothing. ScoreCompetition is where the
// partition is actually made (its `foreach (var group in taskRound.Groups)`),
// so it is the only level at which "per group, not per round" is a claim that
// can fail.
//
// A synthetic class, in ScoringServicePropertyTests' style: MinPerGroup 3 so
// small fields split into several groups, one metric scoring at rate 1 so the
// raw score IS the captured value and every expected number is computable by
// hand.

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

public class NormalisationGroupIsolationPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private const string Metric = "flightTime";
    private const decimal WinnerScore = 1000m;
    private const int MinPerGroup = 3;

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = Metric, Kind = MeasuredKind.Number }];

    // =========================================================== invariants

    /// <summary>
    /// Perturbing one group's flight times leaves every OTHER group's scores
    /// bit-identical. The generator moves only the scores of the competitors
    /// drawn into one chosen group; if normalisation reached across the round
    /// — taking the round's best time as the divisor — every other group's
    /// scores would move with it.
    /// </summary>
    [Fact]
    public void A_competitors_score_depends_only_on_their_own_groups_scores()
    {
        (from fieldSize in Gen.Int[6, 12]
         from baseTimes in Gen.Int[100, 900].Array[12]
         from perturbations in Gen.Int[100, 900].Array[12]
         from groupToPerturb in Gen.Int[0, 3]
         select (fieldSize, baseTimes, perturbations, groupToPerturb))
        .Sample(t =>
        {
            var world = BuildDrawnCompetition(t.fieldSize);
            var groups = world.Groups;
            var perturbedIndex = t.groupToPerturb % groups.Length;
            var perturbedMembers = groups[perturbedIndex].CompetitorRefs.ToHashSet();

            decimal TimeFor(CompetitorId c, bool perturbed)
            {
                var seat = world.FieldOrder.IndexOf(c);
                return perturbed && perturbedMembers.Contains(c)
                    ? t.perturbations[seat]
                    : t.baseTimes[seat];
            }

            var before = ScoreWith(world, c => TimeFor(c, perturbed: false));
            var after = ScoreWith(world, c => TimeFor(c, perturbed: true));

            foreach (var group in groups)
            {
                if (group.Ordinal == groups[perturbedIndex].Ordinal)
                    continue;

                foreach (var competitor in group.CompetitorRefs)
                {
                    var key = competitor.ToString();
                    after.Scores[key].Score.Should().Be(before.Scores[key].Score);
                }
            }
        });
    }

    /// <summary>
    /// Every group crowns its own winner on the normalisation target: in a
    /// one-round competition, the count of competitors scoring exactly 1000
    /// equals the number of groups drawn — not one for the whole round. Ties
    /// are excluded by generating distinct times, so "the group's best" is
    /// unambiguous.
    /// </summary>
    [Fact]
    public void Every_group_has_its_own_winner_on_the_normalisation_target()
    {
        (from fieldSize in Gen.Int[6, 12]
         from times in Gen.Int[100, 900].Array[12].Where(a => a.Distinct().Count() == a.Length)
         select (fieldSize, times))
        .Sample(t =>
        {
            var world = BuildDrawnCompetition(t.fieldSize);

            var result = ScoreWith(world, c => t.times[world.FieldOrder.IndexOf(c)]);

            result.Scores.Values.Count(s => s.Score == WinnerScore)
                .Should().Be(world.Groups.Length);

            // And it is the right competitor in each group: the one with that
            // group's longest flight time, computed from the group's own
            // members alone.
            foreach (var group in world.Groups)
            {
                var groupWinner = group.CompetitorRefs
                    .MaxBy(c => t.times[world.FieldOrder.IndexOf(c)])!;

                result.Scores[groupWinner.ToString()].Score.Should().Be(WinnerScore);
            }
        });
    }

    // =============================================================== fixture

    /// <summary>A drawn one-round competition plus the field order its scores are indexed by.</summary>
    private sealed record World(
        Competition Competition,
        ImmutableArray<CompetitorId> FieldOrder,
        ImmutableArray<Group> Groups);

    private static World BuildDrawnCompetition(int fieldSize)
    {
        var classDefinition = MakeClassDefinition(MakeTask());

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Group Isolation", "Nowhere",
            new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 18),
            "1.0.0",
            new AdoptedRules
            {
                Definition = classDefinition,
                SourceClassId = "content-hash-synthetic",
                SourceVersion = classDefinition.Version,
                AdoptedAt = Now,
            },
            Now));

        var field = ImmutableArray.CreateBuilder<CompetitorId>(fieldSize);

        for (var i = 0; i < fieldSize; i++)
        {
            var competitorRef = CompetitorId.New();
            var registered = competition.RegisterCompetitor(competitorRef, PersonId.New(), Now);
            registered.IsSuccess.Should().BeTrue();
            competition = competition.Apply(registered.Value);
            field.Add(competitorRef);
        }

        var drawn = competition.DrawPhase(1, [], Now);
        drawn.IsSuccess.Should().BeTrue();
        competition = competition.Apply(drawn.Value);

        var groups = competition.Phases[0].Rounds[0].TaskRounds[0].Groups;

        // The premise of both properties: a field of 6..12 against MinPerGroup 3
        // really does split. If the draw ever stopped splitting, these tests
        // would silently degenerate into single-group tests that prove nothing.
        groups.Length.Should().BeGreaterThan(1);

        return new World(competition, field.ToImmutable(), groups);
    }

    /// <summary>
    /// Scores the whole competition with one flight per competitor, the flight
    /// time chosen by <paramref name="timeFor"/> — raw score == that time
    /// (single rate-1 term), so the normalised expectation is arithmetic.
    /// </summary>
    private static CompetitionResult ScoreWith(World world, Func<CompetitorId, decimal> timeFor)
    {
        var entries = new Dictionary<EntryId, Entry>();

        var round = world.Competition.Phases[0].Rounds[0];
        var taskRound = round.TaskRounds[0];

        foreach (var group in taskRound.Groups)
        {
            foreach (var competitorRef in group.CompetitorRefs)
            {
                var opened = world.Competition.OpenEntry(
                    EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, Now);
                opened.IsSuccess.Should().BeTrue();

                var entry = Entry.Create(opened.Value).Apply(new FlightOpened(1, Now, Now));

                var captured = entry.CaptureMeasurement(
                    1, Metric, MeasuredValue.Of(timeFor(competitorRef)), Now, MetricDefs);
                captured.IsSuccess.Should().BeTrue();
                entry = entry.Apply(captured.Value);

                entries[entry.Id] = entry;
            }
        }

        var scored = ScoringService.ScoreCompetition(world.Competition, entries);
        scored.IsSuccess.Should().BeTrue();
        return scored.Value;
    }

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Group isolation task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Group = new GroupConstraint { MinPerGroup = MinPerGroup },
        Normalise = new Normalisation
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = (int)WinnerScore,
        },
        Score = [new RateTerm { MetricRef = Metric, Rate = 1 }],
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
}
