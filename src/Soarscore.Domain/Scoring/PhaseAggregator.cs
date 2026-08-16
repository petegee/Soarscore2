// PhaseAggregator — kanban/completed/scoring-service-plan.md WI-7.
//
// Aggregates task-round results into round scores, then phase scores, applying
// drop policies in order. Handles both ByRound and ByTask drop dimensions.
// The Drops list is ordered — first matching policy wins (F22).

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Round-level data passed by the orchestrator.
/// </summary>
/// <param name="RoundOrdinal">1-based round number.</param>
/// <param name="TaskRounds">The task-rounds within this round.</param>
public sealed record RoundData(
    int RoundOrdinal,
    ImmutableArray<TaskRoundData> TaskRounds
);

/// <summary>
/// One task-round within a round.
/// </summary>
/// <param name="TaskOrdinal">1-based ordinal of this task within the round.</param>
/// <param name="TaskCode">The task code (e.g. "A", "B", "C").</param>
/// <param name="State">Whether this task-round completed or was annulled.</param>
public sealed record TaskRoundData(
    int TaskOrdinal,
    string TaskCode,
    TaskRoundState State
);

public enum TaskRoundState { Complete, Annulled }

/// <summary>
/// Aggregates task-round scores into phase scores, applying drop policies.
/// </summary>
public static class PhaseAggregator
{
    /// <summary>
    /// Aggregate all task-round results for one competitor across a phase,
    /// applying drops.
    /// </summary>
    /// <param name="competitorRef">The competitor's identifier.</param>
    /// <param name="phase">The phase definition.</param>
    /// <param name="rounds">Ordered by round ordinal.</param>
    /// <param name="allScores">
    /// All task-round scores for this competitor in this phase.
    /// Key: "TaskCode|RoundOrdinal|TaskOrdinal" or just iterate to find matches.
    /// </param>
    public static PhaseScores Aggregate(
        string competitorRef,
        PhaseDefinition phase,
        ImmutableArray<RoundData> rounds,
        IReadOnlyDictionary<string, TaskRoundScore> allScores)
    {
        // 1. Build ordered list of task-round scores, matching by round/task-ordinal.
        var scores = new List<TaskRoundScore>();
        var roundScores = new Dictionary<int, decimal>();  // roundOrdinal → sum of task-round scores

        foreach (var round in rounds)
        {
            decimal roundTotal = 0m;
            foreach (var taskRound in round.TaskRounds)
            {
                // Build a lookup key. We'll match by iterating allScores for ones
                // belonging to this competitor + round + task.
                var matching = allScores.Values
                    .FirstOrDefault(s => s.RoundOrdinal == round.RoundOrdinal
                                      && s.TaskOrdinal == taskRound.TaskOrdinal
                                      && s.TaskCode == taskRound.TaskCode);

                if (matching is not null)
                {
                    decimal contribution = taskRound.State == TaskRoundState.Annulled
                        ? 0m
                        : matching.Score;

                    scores.Add(matching with { Score = contribution });
                    roundTotal += contribution;
                }
                else
                {
                    // No score recorded — treat as 0.
                    scores.Add(new TaskRoundScore(
                        taskRound.TaskCode,
                        round.RoundOrdinal,
                        taskRound.TaskOrdinal,
                        Score: 0m
                    ));
                }
            }
            roundScores[round.RoundOrdinal] = roundTotal;
        }

        // 2. Determine completed (non-annulled) rounds for gate evaluation.
        int completedRounds = rounds.Count(r =>
            r.TaskRounds.Any(tr => tr.State == TaskRoundState.Complete));

        // 3. Apply drops — first matching policy wins.
        var allScoresSnap = scores.ToImmutableArray();
        var droppedScores = ImmutableArray<TaskRoundScore>.Empty;
        decimal aggregate;

        if (phase.Drops.IsDefaultOrEmpty)
        {
            aggregate = scores.Sum(s => s.Score);
        }
        else
        {
            (aggregate, droppedScores) = ApplyDrops(
                scores, phase.Drops, completedRounds);
        }

        return new PhaseScores(
            CompetitorRef: competitorRef,
            Aggregate: aggregate,
            AllScores: allScoresSnap,
            DroppedScores: droppedScores
        );
    }

    // --------------------------------------------------------- private

    private static (decimal aggregate, ImmutableArray<TaskRoundScore> dropped)
        ApplyDrops(
            List<TaskRoundScore> scores,
            ImmutableArray<DropPolicy> drops,
            int completedRounds)
    {
        // Evaluate drops in order. First policy whose gates all hold wins.
        foreach (var policy in drops)
        {
            // Check rounds gate
            bool roundsOk = !policy.ApplyWhenRoundsCompletedAtLeast.HasValue
                         || completedRounds >= policy.ApplyWhenRoundsCompletedAtLeast.Value;

            if (!roundsOk)
                continue;

            // Check results gate
            if (policy.Dimension == DropDimension.ByRound)
            {
                // For ByRound: score each round, count how many have results.
                int resultsCount = scores
                    .GroupBy(s => s.RoundOrdinal)
                    .Count(g => g.Sum(s => s.Score) != 0 || g.Any());  // rounds with any score

                bool resultsOk = !policy.ApplyWhenResultsAtLeast.HasValue
                              || resultsCount >= policy.ApplyWhenResultsAtLeast.Value;

                if (!resultsOk)
                    continue;

                // Both gates hold → apply ByRound drop.
                return ApplyByRoundDrop(scores, policy.DropCount);
            }
            else // ByTask
            {
                // Group scores by task code, check results gate per task code.
                var byTask = scores.GroupBy(s => s.TaskCode).ToList();

                // Each task code must meet the results gate independently.
                // The rounds gate is phase-level (already checked).
                bool resultsOk = !policy.ApplyWhenResultsAtLeast.HasValue
                    || byTask.All(g => g.Count() >= policy.ApplyWhenResultsAtLeast.Value);

                // Actually, re-reading Issue #8: the results gate is checked
                // PER TASK CODE. But the drop applies per task code (some may
                // apply, some may not). So we check per task code and drop
                // only from those that meet the gate.
                if (policy.ApplyWhenResultsAtLeast.HasValue)
                {
                    return ApplyByTaskDrop(scores, policy.DropCount,
                        policy.ApplyWhenResultsAtLeast.Value);
                }
                else
                {
                    return ApplyByTaskDrop(scores, policy.DropCount,
                        minResults: 0);
                }
            }
        }

        // No policy matched — no drops.
        decimal total = scores.Sum(s => s.Score);
        return (total, ImmutableArray<TaskRoundScore>.Empty);
    }

    private static (decimal aggregate, ImmutableArray<TaskRoundScore> dropped)
        ApplyByRoundDrop(List<TaskRoundScore> scores, int dropCount)
    {
        // Compute per-round totals, sort ascending, drop lowest N.
        var roundTotals = scores
            .GroupBy(s => s.RoundOrdinal)
            .Select(g => (Round: g.Key, Total: g.Sum(s => s.Score)))
            .OrderBy(x => x.Total)
            .ToList();

        var droppedRounds = roundTotals.Take(dropCount)
            .Select(x => x.Round)
            .ToHashSet();

        var dropped = scores
            .Where(s => droppedRounds.Contains(s.RoundOrdinal))
            .ToImmutableArray();

        var remaining = scores
            .Where(s => !droppedRounds.Contains(s.RoundOrdinal))
            .Sum(s => s.Score);

        return (remaining, dropped);
    }

    private static (decimal aggregate, ImmutableArray<TaskRoundScore> dropped)
        ApplyByTaskDrop(
            List<TaskRoundScore> scores,
            int dropCount,
            int minResults)
    {
        // Group scores by task code.
        var byTask = scores.GroupBy(s => s.TaskCode).ToList();

        var droppedList = ImmutableArray.CreateBuilder<TaskRoundScore>();
        decimal remaining = 0m;

        foreach (var group in byTask)
        {
            var taskScores = group.OrderBy(s => s.Score).ToList();

            // Only drop if this task code meets the results gate.
            if (taskScores.Count >= minResults)
            {
                var toDrop = taskScores.Take(dropCount).ToList();
                droppedList.AddRange(toDrop);

                var kept = taskScores.Skip(dropCount);
                remaining += kept.Sum(s => s.Score);
            }
            else
            {
                // Gate fails — no drop for this task code.
                remaining += taskScores.Sum(s => s.Score);
            }
        }

        return (remaining, droppedList.ToImmutable());
    }
}
