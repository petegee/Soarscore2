// RankingEngine — docs/plans/scoring-service-plan.md WI-8.
//
// Produces final placings from per-competitor scores, handling finalRanking
// kinds and ties. Disqualified competitors are excluded from placings.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Ranks competitors by final scores.
/// </summary>
public static class RankingEngine
{
    /// <summary>
    /// Rank competitors by final scores.
    /// </summary>
    /// <param name="scores">All competitors' final scores.</param>
    /// <param name="finalRanking">
    /// Determines how phases are combined for ranking.
    /// The orchestrator pre-computes scores according to this policy
    /// (LastPhaseReplaces swaps scores, SplitByPromotion splits lists).
    /// This method ranks what it receives.
    /// </param>
    /// <param name="promotion">Promotion rule, for SplitByPromotion context.</param>
    public static CompetitionResult Rank(
        ImmutableArray<FinalCompetitorScore> scores,
        FinalRankingKind? finalRanking,
        PromotionRule? promotion)
    {
        // 1. Remove disqualified competitors.
        var active = scores
            .Where(s => !s.Disqualified)
            .ToList();

        if (active.Count == 0)
        {
            return new CompetitionResult(
                Scores: scores.ToImmutableDictionary(s => s.CompetitorRef),
                Placings: ImmutableDictionary<string, int>.Empty
            );
        }

        // 2. Sort by Score descending (higher is better).
        var ranked = active
            .OrderByDescending(s => s.Score)
            .ToList();

        // 3. Assign placings with ties.
        var placings = new Dictionary<string, int>();
        int currentPlace = 1;
        int i = 0;

        while (i < ranked.Count)
        {
            decimal currentScore = ranked[i].Score;

            // Find the end of the tie group.
            int j = i + 1;
            while (j < ranked.Count && ranked[j].Score == currentScore)
                j++;

            // All competitors from i to j-1 share place `currentPlace`.
            for (int k = i; k < j; k++)
            {
                placings[ranked[k].CompetitorRef] = currentPlace;
            }

            // Next place skips the tie group size.
            currentPlace += (j - i);
            i = j;
        }

        // Disqualified competitors get no placing (excluded from dictionary).

        return new CompetitionResult(
            Scores: scores.ToImmutableDictionary(s => s.CompetitorRef),
            Placings: placings.ToImmutableDictionary()
        );
    }
}
