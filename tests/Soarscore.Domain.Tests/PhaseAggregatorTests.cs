using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for PhaseAggregator (WI-7).
/// Tests ByRound drops, ByTask drops, gate evaluation, mixed task codes,
/// empty drops, and drop ordering.
/// </summary>
public class PhaseAggregatorTests
{
    // ------------------------------------------------------ No drops

    [Fact]
    public void No_drops_all_rounds_summed()
    {
        var phase = MakePhase(drops: []);
        var rounds = MakeRounds(5, taskCode: "A");
        var allScores = MakeScores(rounds, new[] { 100m, 200m, 300m, 400m, 500m });

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        result.Aggregate.Should().Be(1500m);
        result.DroppedScores.Should().BeEmpty();
        result.AllScores.Length.Should().Be(5);
    }

    // ------------------------------------------------------ ByRound drop

    [Fact]
    public void ByRound_drop_when_gates_hold()
    {
        var phase = MakePhase(drops: [ByRoundDrop(count: 1, completedAtLeast: 4)]);
        var rounds = MakeRounds(5, taskCode: "A");
        var allScores = MakeScores(rounds, new[] { 100m, 200m, 300m, 400m, 500m });

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        // Drop lowest round: 100 → remaining: 200+300+400+500 = 1400
        result.Aggregate.Should().Be(1400m);
        result.DroppedScores.Should().ContainSingle();
    }

    [Fact]
    public void ByRound_drop_gate_fails_no_drop()
    {
        var phase = MakePhase(drops: [ByRoundDrop(count: 1, completedAtLeast: 6)]);
        var rounds = MakeRounds(3, taskCode: "A");
        var allScores = MakeScores(rounds, new[] { 100m, 200m, 300m });

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        // Gate fails: 3 rounds < 6 → no drop. Sum = 600
        result.Aggregate.Should().Be(600m);
        result.DroppedScores.Should().BeEmpty();
    }

    // ------------------------------------------------------ Two-tier drops (F3F)

    [Fact]
    public void Two_tier_drops_first_matching_wins()
    {
        // F3F: ByRound 2 when ≥ 15, ByRound 1 when ≥ 4
        var phase = MakePhase(drops: [
            ByRoundDrop(count: 2, completedAtLeast: 15),
            ByRoundDrop(count: 1, completedAtLeast: 4),
        ]);

        // 15 rounds → first policy matches
        var rounds15 = MakeRounds(15, taskCode: "A");
        var scores15 = Enumerable.Range(1, 15).Select(i => (decimal)(i * 100)).ToArray();
        var allScores15 = MakeScores(rounds15, scores15);

        var result15 = PhaseAggregator.Aggregate("C1", phase, rounds15, allScores15);

        // Sum all = 12000, drop 2 lowest = 100+200=300 → 11700
        result15.Aggregate.Should().Be(11700m);
        result15.DroppedScores.Length.Should().Be(2);

        // 5 rounds → second policy matches (first gate fails)
        var rounds5 = MakeRounds(5, taskCode: "A");
        var scores5 = new[] { 100m, 200m, 300m, 400m, 500m };
        var allScores5 = MakeScores(rounds5, scores5);

        var result5 = PhaseAggregator.Aggregate("C1", phase, rounds5, allScores5);

        // Sum = 1500, drop 1 lowest = 100 → 1400
        result5.Aggregate.Should().Be(1400m);
        result5.DroppedScores.Should().ContainSingle();
    }

    // ------------------------------------------------------ ByTask drop

    [Fact]
    public void ByTask_drop_per_task_code()
    {
        var phase = MakePhase(drops: [ByTaskDrop(count: 1, completedAtLeast: 0, resultsAtLeast: 3)]);
        // Round 1: Task A, Round 2: Task B, Round 3: Task A, Round 4: Task B, Round 5: Task A, Round 6: Task B
        var rounds = new[]
        {
            MakeRound(1, [new TaskRoundData(1, "A", TaskRoundState.Complete)]),
            MakeRound(2, [new TaskRoundData(1, "B", TaskRoundState.Complete)]),
            MakeRound(3, [new TaskRoundData(1, "A", TaskRoundState.Complete)]),
            MakeRound(4, [new TaskRoundData(1, "B", TaskRoundState.Complete)]),
            MakeRound(5, [new TaskRoundData(1, "A", TaskRoundState.Complete)]),
            MakeRound(6, [new TaskRoundData(1, "B", TaskRoundState.Complete)]),
        }.ToImmutableArray();

        // Task A scores: 800, 700, 600. Task B scores: 900, 850, 750.
        var allScores = new Dictionary<string, TaskRoundScore>
        {
            ["r1"] = new("A", 1, 1, 800m),
            ["r2"] = new("B", 2, 1, 900m),
            ["r3"] = new("A", 3, 1, 700m),
            ["r4"] = new("B", 4, 1, 850m),
            ["r5"] = new("A", 5, 1, 600m),
            ["r6"] = new("B", 6, 1, 750m),
        };

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        // Drop lowest A (600) and lowest B (750). Remaining: 800+700+900+850 = 3250
        result.Aggregate.Should().Be(3250m);
        result.DroppedScores.Length.Should().Be(2);
    }

    // ------------------------------------------------------ Empty drops

    [Fact]
    public void Empty_drops_no_discard()
    {
        var phase = MakePhase(drops: []);
        var rounds = MakeRounds(5, taskCode: "A");
        var allScores = MakeScores(rounds, new[] { 100m, 200m, 300m, 400m, 500m });

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        result.Aggregate.Should().Be(1500m);
        result.DroppedScores.Should().BeEmpty();
    }

    // ------------------------------------------------------ Annulled task-rounds

    [Fact]
    public void Annulled_task_rounds_contribute_zero()
    {
        var phase = MakePhase(drops: []);
        var rounds = new[]
        {
            MakeRound(1, [new TaskRoundData(1, "A", TaskRoundState.Complete)]),
            MakeRound(2, [new TaskRoundData(1, "A", TaskRoundState.Annulled)]),
            MakeRound(3, [new TaskRoundData(1, "A", TaskRoundState.Complete)]),
        }.ToImmutableArray();

        var allScores = new Dictionary<string, TaskRoundScore>
        {
            ["r1"] = new("A", 1, 1, 500m),
            ["r2"] = new("A", 2, 1, 300m),  // annulled → contributes 0
            ["r3"] = new("A", 3, 1, 400m),
        };

        var result = PhaseAggregator.Aggregate("C1", phase, rounds, allScores);

        result.Aggregate.Should().Be(900m); // 500 + 0 + 400
    }

    // ------------------------------------------------------ helpers

    private static PhaseDefinition MakePhase(ImmutableArray<DropPolicy> drops) => new()
    {
        Ordinal = 1,
        Type = PhaseType.Preliminary,
        Rounds = new RoundComposition
        {
            Kind = CompositionKind.ChooseFromCatalogue,
            TasksPerRound = 1,
        },
        Validity = new ValidityRule { MinRounds = 1 },
        Drops = drops,
        Tasks = ImmutableArray<TaskDefinition>.Empty,
    };

    private static DropPolicy ByRoundDrop(int count, int? completedAtLeast) => new()
    {
        Dimension = DropDimension.ByRound,
        DropCount = count,
        ApplyWhenRoundsCompletedAtLeast = completedAtLeast,
    };

    private static DropPolicy ByTaskDrop(int count, int? completedAtLeast, int? resultsAtLeast) => new()
    {
        Dimension = DropDimension.ByTask,
        DropCount = count,
        ApplyWhenRoundsCompletedAtLeast = completedAtLeast,
        ApplyWhenResultsAtLeast = resultsAtLeast,
    };

    private static ImmutableArray<RoundData> MakeRounds(int count, string taskCode)
    {
        return Enumerable.Range(1, count).Select(i =>
            MakeRound(i, [new TaskRoundData(1, taskCode, TaskRoundState.Complete)])
        ).ToImmutableArray();
    }

    private static RoundData MakeRound(int ordinal, ImmutableArray<TaskRoundData> taskRounds) => new(ordinal, taskRounds);

    private static IReadOnlyDictionary<string, TaskRoundScore> MakeScores(
        ImmutableArray<RoundData> rounds, decimal[] scores)
    {
        var dict = new Dictionary<string, TaskRoundScore>();
        for (int i = 0; i < Math.Min(rounds.Length, scores.Length); i++)
        {
            var r = rounds[i].TaskRounds[0];
            dict[$"r{rounds[i].RoundOrdinal}"] = new TaskRoundScore(
                r.TaskCode, rounds[i].RoundOrdinal, r.TaskOrdinal, scores[i]);
        }
        return dict;
    }
}
