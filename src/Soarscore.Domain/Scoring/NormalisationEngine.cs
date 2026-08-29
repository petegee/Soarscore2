// NormalisationEngine — kanban/completed/scoring-service-plan.md WI-5.
//
// Normalises task results within a group, applies post-normalisation score
// terms (ScoreNormalised), rounds, floors the result at zero (lower clamp —
// kanban/completed/normalisation-lower-clamp.md), and checks minValidResults
// for group annulment (Issue #5). If the task has no Normalisation, raw
// scores pass through unchanged. Each row's incoming raw score is captured
// into GroupResult.PreNormalisationScores before the overwrite
// (kanban/in-progress/pre-normalisation-score-view-field.md#WI-1).

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Normalises a group's task results.
/// </summary>
public static class NormalisationEngine
{
    /// <summary>
    /// Normalise a group's task results. If the task has no Normalisation,
    /// raw scores pass through unchanged. NoResult entries are excluded
    /// from winner finding. Populates PreNormalisationScores with each
    /// row's incoming raw score
    /// (kanban/in-progress/pre-normalisation-score-view-field.md#WI-1).
    /// </summary>
    /// <param name="groupRef">Which group (informational — not used in computation).</param>
    /// <param name="taskResults">CompetitorRef → TaskResult.</param>
    /// <param name="task">The resolved task definition.</param>
    /// <param name="parameterBindings">Parameter bindings (for ScoreNormalised term resolution).</param>
    public static GroupResult Normalise(
        string groupRef,
        ImmutableDictionary<string, TaskResult> taskResults,
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings)
    {
        // 1. Count valid results.
        var validEntries = taskResults
            .Where(kv => kv.Value.State == TaskResultState.Valid)
            .ToList();

        int validCount = validEntries.Count;

        // 2. Check group annulment (Issue #5).
        bool isAnnulled = false;
        if (task.Group is not null && task.Group.MinValidResults.HasValue)
        {
            if (validCount < task.Group.MinValidResults.Value)
                isAnnulled = true;
        }

        // 3. No normalisation? Raw scores pass through.
        if (task.Normalise is null)
        {
            var passThrough = taskResults.ToImmutableDictionary(
                kv => kv.Key,
                kv => kv.Value);

            // Pass-through is the identity, so the pre-normalisation values
            // are the same numbers as the final results
            // (kanban/in-progress/pre-normalisation-score-view-field.md#WI-1).
            var preScores = taskResults.ToImmutableDictionary(
                kv => kv.Key,
                kv => kv.Value.RawScore);

            return new GroupResult(
                Results: passThrough,
                WinnerRef: null,
                ValidCount: validCount,
                PreNormalisationScores: preScores,
                IsAnnulled: isAnnulled
            );
        }

        // 4. Has normalisation — compute normalised scores.
        var norm = task.Normalise;

        // Find winner among valid entries.
        string? winnerRef = null;
        decimal winnerRaw = 0m;

        if (validEntries.Count > 0)
        {
            if (norm.Direction == NormalisationDirection.HigherIsBetter)
            {
                var best = validEntries.MaxBy(kv => kv.Value.RawScore);
                winnerRef = best.Key;
                winnerRaw = best.Value.RawScore;
            }
            else // LowerIsBetter
            {
                var best = validEntries.MinBy(kv => kv.Value.RawScore);
                winnerRef = best.Key;
                winnerRaw = best.Value.RawScore;
            }
        }

        // Capture each row's incoming raw score from the incoming
        // taskResults — never from post-modification copies — so every
        // overwrite below (zero-guard continues, LowerIsBetter backstop,
        // the normalised rewrite) leaves the original value in the map
        // (kanban/in-progress/pre-normalisation-score-view-field.md#WI-1).
        var preNormalisationScores = taskResults.ToImmutableDictionary(
            kv => kv.Key,
            kv => kv.Value.RawScore);

        // 5. Compute normalised scores and add ScoreNormalised terms.
        var resultBuilder = ImmutableDictionary.CreateBuilder<string, TaskResult>();

        foreach (var (competitorRef, taskResult) in taskResults)
        {
            if (taskResult.State != TaskResultState.Valid || winnerRaw == 0m)
            {
                // NoResult or no valid winner → score 0.
                resultBuilder[competitorRef] = taskResult with { RawScore = 0m };
                continue;
            }

            decimal raw = taskResult.RawScore;

            // A LowerIsBetter raw score of exactly zero has no finite
            // normalised value — the division below is undefined, not just
            // large. Not reachable today for a non-negative metric: a zero
            // would already be the smallest value among validEntries and so
            // would already have won the MinBy above, tripping the
            // `winnerRaw == 0m` branch first (and, for courseTime, a
            // captured zero is excluded even earlier — see
            // SeedF3F.cs/SeedF3B.cs's ValidWhen). Kept as a backstop for any
            // future LowerIsBetter metric whose raw score can go negative,
            // where a real (negative) winner could coexist with another
            // competitor sitting at exactly zero.
            if (norm.Direction == NormalisationDirection.LowerIsBetter && raw == 0m)
            {
                resultBuilder[competitorRef] = taskResult with { RawScore = 0m };
                continue;
            }

            decimal normalised;

            if (norm.Direction == NormalisationDirection.HigherIsBetter)
            {
                normalised = (norm.WinnerScore * raw) / winnerRaw;
            }
            else
            {
                normalised = (norm.WinnerScore * winnerRaw) / raw;
            }

            // 6. Round normalised if set.
            if (norm.Round is not null)
                normalised = RoundingSupport.ApplyRounding(normalised, norm.Round);

            // 7. Add normalised terms (evaluated per selected flight).
            if (!task.ScoreNormalised.IsDefaultOrEmpty
                && taskResult.Selection is not null)
            {
                foreach (var flight in taskResult.Selection.Flights)
                {
                    foreach (var term in task.ScoreNormalised)
                    {
                        var contrib = FlightInterpreter.EvaluateTerm(
                            term, flight.Metrics);
                        normalised += contrib.Points;
                    }
                }
            }

            // 8. Round again after adding normalised terms (if Round is set).
            if (norm.Round is not null)
                normalised = RoundingSupport.ApplyRounding(normalised, norm.Round);

            // 9. Lower clamp — F5J 5.5.11.12 f ("where the score is negative, a
            // zero score will be recorded"), implemented at the normalised grain
            // per GS's floors (Scoring_MOD.vb:310 option-1, :367 option-2 sum);
            // nz-fixture-replay-scenarios.md D2/N1,
            // kanban/completed/normalisation-lower-clamp.md D1-D3. `<=` so a
            // rounded decimal -0.0 cannot escape.
            if (normalised <= 0m)
                normalised = 0m;

            resultBuilder[competitorRef] = taskResult with { RawScore = normalised };
        }

        return new GroupResult(
            Results: resultBuilder.ToImmutable(),
            WinnerRef: winnerRef,
            ValidCount: validCount,
            PreNormalisationScores: preNormalisationScores,
            IsAnnulled: isAnnulled
        );
    }

}
