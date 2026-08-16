using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for PhaseAggregator's drop-worst logic (LADR-0003:
/// CsCheck, for scoring-engine invariants). Every drop policy here has no
/// gates, so "first matching policy wins" always matches — the gate itself
/// is exercised separately in Drop_policy_with_unmet_gate_is_skipped.
/// </summary>
public class PhaseAggregatorPropertyTests
{
    private static readonly Gen<decimal> ScoreValue =
        Gen.Int[-10_000, 10_000].Select(i => i / 100m);

    // -------------------------------------------------------------- ByRound

    [Fact]
    public void ByRound_drop_partitions_conserves_and_drops_the_worst()
    {
        (from roundCount in Gen.Int[2, 6]
         from dropCount in Gen.Int[1, roundCount - 1]
         from scores in ScoreValue.Array[roundCount]
         select (roundCount, dropCount, scores))
        .Sample(t =>
        {
            var rounds = Enumerable.Range(1, t.roundCount)
                .Select(r => new RoundData(r,
                    ImmutableArray.Create(new TaskRoundData(1, "A", TaskRoundState.Complete))))
                .ToImmutableArray();

            var allScores = Enumerable.Range(1, t.roundCount)
                .Select(r => new TaskRoundScore("A", r, 1, t.scores[r - 1]))
                .ToDictionary(s => $"R{s.RoundOrdinal}", s => s);

            var phase = MakePhase(new DropPolicy
            {
                Dimension = DropDimension.ByRound,
                DropCount = t.dropCount,
            });

            var result = PhaseAggregator.Aggregate("Comp", phase, rounds, allScores);

            if (!TryPartition(result.AllScores, result.DroppedScores, out var kept))
                return false;

            if (kept.Sum(s => s.Score) != result.Aggregate)
                return false;

            var droppedScores = result.DroppedScores.Select(s => s.Score).ToList();
            var keptScores = kept.Select(s => s.Score).ToList();

            return droppedScores.Count == 0 || keptScores.Count == 0
                || droppedScores.Max() <= keptScores.Min();
        });
    }

    // --------------------------------------------------------------- ByTask

    [Fact]
    public void ByTask_drop_partitions_conserves_and_drops_the_worst_per_task_code()
    {
        var taskCodes = new[] { "A", "B" };

        (from roundsPerTask in Gen.Int[2, 6]
         from dropCount in Gen.Int[1, roundsPerTask - 1]
         from scoresA in ScoreValue.Array[roundsPerTask]
         from scoresB in ScoreValue.Array[roundsPerTask]
         select (roundsPerTask, dropCount, scoresA, scoresB))
        .Sample(t =>
        {
            var rounds = Enumerable.Range(1, t.roundsPerTask)
                .Select(r => new RoundData(r, ImmutableArray.Create(
                    new TaskRoundData(1, "A", TaskRoundState.Complete),
                    new TaskRoundData(2, "B", TaskRoundState.Complete))))
                .ToImmutableArray();

            var allScores = new Dictionary<string, TaskRoundScore>();
            for (int r = 1; r <= t.roundsPerTask; r++)
            {
                allScores[$"A{r}"] = new TaskRoundScore("A", r, 1, t.scoresA[r - 1]);
                allScores[$"B{r}"] = new TaskRoundScore("B", r, 2, t.scoresB[r - 1]);
            }

            var phase = MakePhase(new DropPolicy
            {
                Dimension = DropDimension.ByTask,
                DropCount = t.dropCount,
            });

            var result = PhaseAggregator.Aggregate("Comp", phase, rounds, allScores);

            if (!TryPartition(result.AllScores, result.DroppedScores, out var kept))
                return false;

            if (kept.Sum(s => s.Score) != result.Aggregate)
                return false;

            foreach (var code in taskCodes)
            {
                var droppedForTask = result.DroppedScores.Where(s => s.TaskCode == code).Select(s => s.Score).ToList();
                var keptForTask = kept.Where(s => s.TaskCode == code).Select(s => s.Score).ToList();

                if (droppedForTask.Count > 0 && keptForTask.Count > 0
                    && droppedForTask.Max() > keptForTask.Min())
                    return false;
            }

            return true;
        });
    }

    // ---------------------------------------------------- bounds (WI-5 #4)

    /// <summary>
    /// WI-5 invariant 4 (kanban/completed/scoring-steel-thread-plan.md): the phase
    /// aggregate never exceeds the sum of every task-round score, and never
    /// falls below the sum of the best (n - dropCount) of them. Stated over
    /// NON-NEGATIVE scores, unlike ScoreValue's -100..100 range above: with a
    /// negative score in the mix, dropping the WORST (most negative) score
    /// can raise the aggregate past the full sum, which is correct behaviour,
    /// not a violation — a raw score reaching this stage is never negative in
    /// practice (a task's own rounding/clamping floors it before
    /// aggregation), so the bound is checked over the domain it actually
    /// holds for. The tests above already cover conservation and
    /// worst-first-dropped for both dimensions; this is the literal ≤ / ≥
    /// bound the plan names, checked separately rather than folded in.
    /// </summary>
    private static readonly Gen<decimal> NonNegativeScoreValue = Gen.Int[0, 10_000].Select(i => i / 100m);

    [Fact]
    public void Aggregate_never_exceeds_the_full_sum_and_never_falls_below_the_best_n_minus_drops_ByRound()
    {
        (from roundCount in Gen.Int[2, 6]
         from dropCount in Gen.Int[1, roundCount - 1]
         from scores in NonNegativeScoreValue.Array[roundCount]
         select (roundCount, dropCount, scores))
        .Sample(t =>
        {
            var rounds = Enumerable.Range(1, t.roundCount)
                .Select(r => new RoundData(r,
                    ImmutableArray.Create(new TaskRoundData(1, "A", TaskRoundState.Complete))))
                .ToImmutableArray();

            var allScores = Enumerable.Range(1, t.roundCount)
                .Select(r => new TaskRoundScore("A", r, 1, t.scores[r - 1]))
                .ToDictionary(s => $"R{s.RoundOrdinal}", s => s);

            var phase = MakePhase(new DropPolicy
            {
                Dimension = DropDimension.ByRound,
                DropCount = t.dropCount,
            });

            var result = PhaseAggregator.Aggregate("Comp", phase, rounds, allScores);

            var fullSum = t.scores.Sum();
            var bestNMinusDrops = t.scores.OrderByDescending(s => s).Take(t.roundCount - t.dropCount).Sum();

            result.Aggregate.Should().BeLessThanOrEqualTo(fullSum);
            result.Aggregate.Should().BeGreaterThanOrEqualTo(bestNMinusDrops);
        });
    }

    [Fact]
    public void Aggregate_never_exceeds_the_full_sum_and_never_falls_below_the_best_n_minus_drops_ByTask()
    {
        (from roundsPerTask in Gen.Int[2, 6]
         from dropCount in Gen.Int[1, roundsPerTask - 1]
         from scoresA in NonNegativeScoreValue.Array[roundsPerTask]
         from scoresB in NonNegativeScoreValue.Array[roundsPerTask]
         select (roundsPerTask, dropCount, scoresA, scoresB))
        .Sample(t =>
        {
            var rounds = Enumerable.Range(1, t.roundsPerTask)
                .Select(r => new RoundData(r, ImmutableArray.Create(
                    new TaskRoundData(1, "A", TaskRoundState.Complete),
                    new TaskRoundData(2, "B", TaskRoundState.Complete))))
                .ToImmutableArray();

            var allScores = new Dictionary<string, TaskRoundScore>();
            for (int r = 1; r <= t.roundsPerTask; r++)
            {
                allScores[$"A{r}"] = new TaskRoundScore("A", r, 1, t.scoresA[r - 1]);
                allScores[$"B{r}"] = new TaskRoundScore("B", r, 2, t.scoresB[r - 1]);
            }

            var phase = MakePhase(new DropPolicy
            {
                Dimension = DropDimension.ByTask,
                DropCount = t.dropCount,
            });

            var result = PhaseAggregator.Aggregate("Comp", phase, rounds, allScores);

            var fullSum = t.scoresA.Sum() + t.scoresB.Sum();
            var bestNMinusDrops =
                t.scoresA.OrderByDescending(s => s).Take(t.roundsPerTask - t.dropCount).Sum()
                + t.scoresB.OrderByDescending(s => s).Take(t.roundsPerTask - t.dropCount).Sum();

            result.Aggregate.Should().BeLessThanOrEqualTo(fullSum);
            result.Aggregate.Should().BeGreaterThanOrEqualTo(bestNMinusDrops);
        });
    }

    // ----------------------------------------------------------------- gate

    [Fact]
    public void Drop_policy_with_unmet_rounds_gate_is_skipped()
    {
        (from roundCount in Gen.Int[2, 6]
         from dropCount in Gen.Int[1, roundCount - 1]
         from scores in ScoreValue.Array[roundCount]
         from requiredRounds in Gen.Int[roundCount + 1, roundCount + 10]
         select (roundCount, dropCount, scores, requiredRounds))
        .Sample(t =>
        {
            var rounds = Enumerable.Range(1, t.roundCount)
                .Select(r => new RoundData(r,
                    ImmutableArray.Create(new TaskRoundData(1, "A", TaskRoundState.Complete))))
                .ToImmutableArray();

            var allScores = Enumerable.Range(1, t.roundCount)
                .Select(r => new TaskRoundScore("A", r, 1, t.scores[r - 1]))
                .ToDictionary(s => $"R{s.RoundOrdinal}", s => s);

            var phase = MakePhase(new DropPolicy
            {
                Dimension = DropDimension.ByRound,
                DropCount = t.dropCount,
                ApplyWhenRoundsCompletedAtLeast = t.requiredRounds,
            });

            var result = PhaseAggregator.Aggregate("Comp", phase, rounds, allScores);

            // requiredRounds > roundCount (all rounds complete) → gate never
            // holds → the policy is skipped entirely, nothing dropped.
            return result.DroppedScores.IsEmpty && result.Aggregate == t.scores.Sum();
        });
    }

    // ------------------------------------------------------------- helpers

    /// <summary>
    /// True if every dropped score is present in all (as a multiset), with
    /// the remaining ("kept") scores returned.
    /// </summary>
    private static bool TryPartition(
        ImmutableArray<TaskRoundScore> all,
        ImmutableArray<TaskRoundScore> dropped,
        out List<TaskRoundScore> kept)
    {
        kept = all.ToList();
        foreach (var d in dropped)
        {
            if (!kept.Remove(d))
                return false;
        }
        return true;
    }

    private static PhaseDefinition MakePhase(DropPolicy policy) => new()
    {
        Ordinal = 1,
        Type = PhaseType.Preliminary,
        Validity = new ValidityRule { MinRounds = 1 },
        Drops = ImmutableArray.Create(policy),
        Tasks = ImmutableArray<TaskDefinition>.Empty,
    };
}
