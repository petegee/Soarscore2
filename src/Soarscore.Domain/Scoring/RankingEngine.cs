// RankingEngine — kanban/completed/scoring-service-plan.md WI-8; the tie-break
// ladder per kanban/in-progress/tie-break-policy-in-class-definition.md.
//
// Produces final placings from per-competitor scores, handling finalRanking
// kinds and ties. Disqualified competitors are excluded from placings.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// The tie-break context the rank stage reads: one phase's ordered directives
/// plus the figures the comparator rungs compare against. The only reachable
/// ranking today is Phases[0]'s (ScoringService's phase loop), so the
/// qualifying-positions map is always empty in production and every
/// <see cref="PublishedClassDefinition.QualifyingPosition"/> rung is
/// unreachable end-to-end (story D9) — the engine still implements it
/// generically, proven by tests that construct the context directly.
/// </summary>
/// <param name="Directives">The ranking phase's stated ladder, in order. Empty = the display ladder.</param>
/// <param name="QualifyingPositions">
/// CompetitorRef → the competitor's placing in the source phase's ranking, for
/// <see cref="PublishedClassDefinition.QualifyingPosition"/> rungs. Absent
/// entries cannot be separated on such a rung (the pair stays tied).
/// </param>
public sealed record TieBreakContext(
    ImmutableArray<TieBreakDirective> Directives,
    ImmutableDictionary<string, int> QualifyingPositions)
{
    /// <summary>
    /// No stated policy: the class-agnostic display ladder (Score DESC,
    /// PreDropScore DESC) with nothing ever surfaced as pending.
    /// </summary>
    public static TieBreakContext Display { get; } =
        new(ImmutableArray<TieBreakDirective>.Empty, ImmutableDictionary<string, int>.Empty);
}

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
    /// <param name="tieBreaks">
    /// The ranking phase's tie-break policy and its comparator figures. The
    /// empty policy (<see cref="TieBreakContext.Display"/>) is byte-identical
    /// to the pre-policy two-rung display ladder (Invariant T, clause 2).
    /// </param>
    public static CompetitionResult Rank(
        ImmutableArray<FinalCompetitorScore> scores,
        FinalRankingKind? finalRanking,
        PromotionRule? promotion,
        TieBreakContext tieBreaks)
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

        // 2. The ladder. Rung 1 is always Score DESC — that is what "rank"
        // means, and it is core-owned rather than class data. A phase stating
        // NO tie-breaks (TieBreaks empty, the default) keeps the display rung
        // 2, the PreDropScore countback. A phase stating a list SUPERSEDES
        // rung 2: comparator rungs narrow tie groups in order, and the FIRST
        // operational or UndefinedRequiresRuling rung halts evaluation — rungs
        // after it stay dormant (F3F.1.13's bestDroppedScore fallback is stated
        // for exactly that future), so only the comparators before the halt
        // are sort keys (story D3). The engine evaluates rungs generically; it
        // never branches on which class is being run.
        var directives = tieBreaks.Directives;
        var halt = directives.FirstOrDefault(
            d => d is not BestDroppedScore and not QualifyingPosition);
        var keyRungs = directives
            .TakeWhile(d => d is BestDroppedScore or QualifyingPosition)
            .ToArray();

        int Compare(FinalCompetitorScore a, FinalCompetitorScore b)
        {
            // Rung 1, higher is better.
            var c = b.Score.CompareTo(a.Score);
            if (c != 0) return c;

            foreach (var rung in keyRungs)
            {
                switch (rung)
                {
                    case BestDroppedScore:
                        // Higher is better ("the best dropped score defines
                        // the ranking").
                        c = b.BestDroppedScore.CompareTo(a.BestDroppedScore);
                        if (c != 0) return c;
                        break;
                    case QualifyingPosition:
                        // A better prior placing is the LOWER number —
                        // ascending ("their respective position in the
                        // qualifying rounds"). A competitor with no figure
                        // cannot be separated on this rung: the pair stays
                        // tied (unreachable in the single-phase world — D9).
                        var hasA = tieBreaks.QualifyingPositions.TryGetValue(a.CompetitorRef, out var posA);
                        var hasB = tieBreaks.QualifyingPositions.TryGetValue(b.CompetitorRef, out var posB);
                        if (hasA && hasB)
                        {
                            c = posA.CompareTo(posB);
                            if (c != 0) return c;
                        }
                        break;
                }
            }

            // The display ladder's rung 2 — the PreDropScore countback —
            // belongs to the ABSENT policy only: a stated list supersedes it
            // (D3), so an empty policy is exactly the pre-policy two-rung
            // ladder (Invariant T, clause 2) and any stated list ends here —
            // including one whose first rung is operational, where countback
            // is precisely what the class refuses (F3B.2.8's answer to a tie
            // is fly, not count back).
            if (directives.IsEmpty)
            {
                c = b.PreDropScore.CompareTo(a.PreDropScore);
                if (c != 0) return c;
            }

            return 0;
        }

        var ranked = active
            .OrderBy(s => s, Comparer<FinalCompetitorScore>.Create(Compare))
            .ToList();

        // 3. Assign placings with ties — the same skip-ahead loop as always.
        var placings = new Dictionary<string, int>();
        var pending = ImmutableArray.CreateBuilder<PendingTieBreak>();
        int currentPlace = 1;
        int i = 0;

        while (i < ranked.Count)
        {
            // Find the end of the tie group: equal on every evaluated rung.
            int j = i + 1;
            while (j < ranked.Count && Compare(ranked[i], ranked[j]) == 0)
                j++;

            // All competitors from i to j-1 share place `currentPlace`.
            for (int k = i; k < j; k++)
            {
                placings[ranked[k].CompetitorRef] = currentPlace;
            }

            // Halt (story D5): a group the comparators could not separate
            // whose next unevaluated rung is operational or
            // UndefinedRequiresRuling keeps its shared places exactly as
            // assigned above and surfaces the requirement as data — a
            // read-side annotation, never a write gate (NFR-4). An EMPTY
            // policy never halts (its display ladder is the established
            // practice; PendingTieBreaks stays empty — Invariant T, clause 2).
            // An exhausted comparator ladder lands here with `halt` null:
            // shared places, settled, NO pending entry (F3J's fly-off: equal
            // aggregate and equal qualifying place share the final place — the
            // rulebook states nothing further).
            if (j - i > 1 && halt is not null)
            {
                pending.Add(new PendingTieBreak(
                    ranked.Skip(i).Take(j - i).Select(s => s.CompetitorRef).ToImmutableArray(),
                    halt));
            }

            // Next place skips the tie group size.
            currentPlace += (j - i);
            i = j;
        }

        // Disqualified competitors get no placing (excluded from dictionary).

        return new CompetitionResult(
            Scores: scores.ToImmutableDictionary(s => s.CompetitorRef),
            Placings: placings.ToImmutableDictionary()
        )
        {
            PendingTieBreaks = pending.ToImmutable()
        };
    }
}
