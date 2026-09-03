// The draw's pairing algorithm — kanban/completed/phase-drawn-steel-thread-plan.md
// WI-1. Pure and deterministic given a stable field ordering: no unseeded
// randomness, so a replay and a CsCheck shrink (WI-2) both reproduce the same
// output. GroupId is not minted here — this returns bare CompetitorId
// partitions; Competition.cs's draw decide functions (DrawPhase, PrescribeDraw)
// mint GroupId when they build the Group records for the event, the only place
// a GroupId is actually needed.

using System.Collections.Immutable;

namespace Soarscore.Domain.Competitions;

public static class PhaseDraw
{
    /// <summary>
    /// Builds, for each of <paramref name="roundCount"/> rounds (outermost
    /// array), a partition of <paramref name="field"/> into groups — group
    /// count and sizes from <paramref name="minPerGroup"/> (fewest groups,
    /// F3K.9.1 / 5.5.11.8), and membership chosen round by round to minimise
    /// repeat pairings (00-general-rules.md#1 "as few times as possible") via
    /// a backtracking least-paired-first construction (see BuildOneRound).
    /// Not claimed optimal ACROSS rounds at full contest scale — each round
    /// is built to be the best available given prior rounds, not with
    /// lookahead onto rounds still to come — but WI-2's brute-force property
    /// test does hold this to the true joint-optimum at small field/round
    /// counts, which is what forced the backtracking: a single-pass,
    /// no-backtrack greedy can paint itself into a forced repeat in the last
    /// group of a round even at tiny N (field 6 / minPerGroup 2 / round 2 is
    /// WI-2's counterexample) — a known shape for this class of problem
    /// (resolvable designs / "social golfer"), not an implementation slip.
    /// </summary>
    public static ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> BuildGroups(
        ImmutableArray<CompetitorId> field, int minPerGroup, int roundCount) =>
        BuildGroups(field, Enumerable.Repeat(minPerGroup, roundCount).ToImmutableArray());

    /// <summary>
    /// Per-round <paramref name="minPerGroupByRound"/> — one entry per round,
    /// its length therefore also the round count (catalogue-choice-draws-plan.md
    /// WI-1: two values that must agree, when one already implies the other, is
    /// a defect waiting to be written). Group SHAPE may change from round to
    /// round; the cross-round pairing state (<c>pairCount</c>) does not reset
    /// between rounds regardless — that is the property this overload protects.
    /// </summary>
    public static ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> BuildGroups(
        ImmutableArray<CompetitorId> field, ImmutableArray<int> minPerGroupByRound) =>
        BuildGroups(field, minPerGroupByRound, []);

    /// <summary>
    /// The two-input overload plus <paramref name="protectedPairs"/> — unordered
    /// pairs of competitors the draw must try to keep apart (teams-mvp.md WI-4).
    /// The draw engine's entire view of protection is this flat pair set: it
    /// knows nothing about protection groups or scoring teams, and nothing here
    /// branches on a class or a team (draw-engine discipline).
    /// <para>
    /// The least-bad objective (owner decision 5): protection-budget deepening
    /// OUTER, repeat-ceiling escalation INNER, per round. The first per-round
    /// budget v = 0, 1, 2, … for which every round admits a partition with at
    /// most v protected co-occurrences wins, so the returned draw has the
    /// minimum achievable violation count per round — infeasible protection (a
    /// protection group larger than the group count) returns that
    /// minimum-violation partition rather than failing. Among partitions with
    /// the same violation count the repeat objective decides exactly as before,
    /// so determinism and the fairness-priority invariant hold by construction.
    /// </para>
    /// <para>
    /// A violation is a protected pair co-grouped in ONE round; the budget is
    /// per-round. The cross-round repeat state (<c>pairCount</c>) is untouched.
    /// Guaranteed to terminate: each protected pair can co-occur at most once
    /// per round, so at v = <paramref name="protectedPairs"/>.Length every
    /// candidate is admissible and the plain, always-feasible unconstrained
    /// build is found no later.
    /// </para>
    /// </summary>
    public static ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> BuildGroups(
        ImmutableArray<CompetitorId> field,
        ImmutableArray<int> minPerGroupByRound,
        ImmutableArray<ProtectedPair> protectedPairs)
    {
        var protectedSet = protectedPairs
            .Select(pair => PairKey(pair.A, pair.B))
            .ToHashSet();

        for (var violationBudget = 0; ; violationBudget++)
        {
            var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();
            var rounds = ImmutableArray.CreateBuilder<ImmutableArray<ImmutableArray<CompetitorId>>>(minPerGroupByRound.Length);
            var feasible = true;

            foreach (var minPerGroup in minPerGroupByRound)
            {
                var groupCount = Math.Max(1, field.Length / minPerGroup);
                var sizes = GroupSizes(field.Length, groupCount);

                var groups = BuildOneRound(field, sizes, pairCount, protectedSet, violationBudget);
                if (groups is null)
                {
                    feasible = false;
                    break;
                }

                rounds.Add(groups.Value);
                RecordPairings(groups.Value, pairCount);
            }

            if (feasible)
            {
                return rounds.MoveToImmutable();
            }
        }
    }

    /// <summary>
    /// <paramref name="groupCount"/> sizes summing to <paramref name="fieldSize"/>:
    /// <c>fieldSize / groupCount</c>, remainder spread one-per-group across the
    /// first groups. No rule states groups must be exactly equal, only that the
    /// split should favour fewer, fuller groups — see the plan's rules note.
    /// </summary>
    private static ImmutableArray<int> GroupSizes(int fieldSize, int groupCount)
    {
        var baseSize = fieldSize / groupCount;
        var remainder = fieldSize % groupCount;

        var builder = ImmutableArray.CreateBuilder<int>(groupCount);
        for (var g = 0; g < groupCount; g++)
        {
            builder.Add(g < remainder ? baseSize + 1 : baseSize);
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Finds the lowest ceiling C such that the field can be split into
    /// <paramref name="sizes"/>-shaped groups with no pair's resultant count
    /// (existing + 1, for any pair newly co-grouped this round) exceeding C and
    /// no more than <paramref name="violationBudget"/> protected pairs
    /// co-grouped — iterative deepening over C, backtracking
    /// (<see cref="TryBuildRound"/>) within each attempt. A single-pass fill can
    /// commit an early group to a choice that forces the last group into an
    /// avoidable repeat; escalating C and backtracking is what finds the
    /// partition a plain greedy misses.
    /// <para>
    /// Returns null when no ceiling can help: every candidate's worst resultant
    /// pairing is at most the largest count already recorded plus one (each
    /// competitor is placed once per round, so no pair is newly co-grouped
    /// twice), which makes the repeat constraint VACUOUS at that ceiling — a
    /// failure there is the violation budget's, and no higher C can succeed.
    /// Bounding the escalation is what lets a budget-infeasible round report
    /// failure to the deepening loop above instead of searching forever.
    /// </para>
    /// </summary>
    private static ImmutableArray<ImmutableArray<CompetitorId>>? BuildOneRound(
        ImmutableArray<CompetitorId> field,
        ImmutableArray<int> sizes,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs,
        int violationBudget)
    {
        var currentMax = pairCount.Count == 0 ? 0 : pairCount.Values.Max();

        for (var ceiling = Math.Max(1, currentMax); ceiling <= currentMax + 1; ceiling++)
        {
            var attempt = TryBuildRound(sizes, 0, field, pairCount, ceiling, protectedPairs, violationBudget);
            if (attempt is not null)
            {
                return attempt;
            }
        }

        return null;
    }

    private static ImmutableArray<ImmutableArray<CompetitorId>>? TryBuildRound(
        ImmutableArray<int> sizes,
        int groupIndex,
        ImmutableArray<CompetitorId> unplaced,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int ceiling,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs,
        int remainingBudget)
    {
        if (groupIndex == sizes.Length)
        {
            return ImmutableArray<ImmutableArray<CompetitorId>>.Empty;
        }

        foreach (var (group, rest, violations) in CandidateGroups(
            unplaced, sizes[groupIndex], pairCount, ceiling, protectedPairs, remainingBudget))
        {
            var tail = TryBuildRound(
                sizes, groupIndex + 1, rest, pairCount, ceiling, protectedPairs, remainingBudget - violations);
            if (tail is not null)
            {
                return tail.Value.Insert(0, group);
            }
        }

        return null;
    }

    /// <summary>
    /// Every <paramref name="size"/>-subset of <paramref name="unplaced"/> whose
    /// internal pairs all resolve to <paramref name="ceiling"/> or below and
    /// whose internal protected-pair count is at most
    /// <paramref name="remainingBudget"/>, best-first: ranked by the worst
    /// single resultant pairing, then the sum of resultant pairings, then field
    /// order — the same ranking the original single-pass greedy used, so the
    /// common case (no backtracking needed) still picks exactly what it used to
    /// pick, on the first candidate tried. The violation count rides along so
    /// <see cref="TryBuildRound"/> can hand the remainder of the budget to the
    /// groups still to be placed.
    /// </summary>
    private static IEnumerable<(ImmutableArray<CompetitorId> Group, ImmutableArray<CompetitorId> Remaining, int Violations)> CandidateGroups(
        ImmutableArray<CompetitorId> unplaced,
        int size,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int ceiling,
        HashSet<(CompetitorId, CompetitorId)> protectedPairs,
        int remainingBudget)
    {
        var candidates = new List<(ImmutableArray<CompetitorId> Group, int WorstPairing, int Sum, int Violations)>();

        foreach (var combo in Combinations(unplaced, size))
        {
            var worst = 0;
            var sum = 0;
            var violations = 0;
            var admissible = true;

            for (var i = 0; i < combo.Length && admissible; i++)
            {
                for (var j = i + 1; j < combo.Length; j++)
                {
                    var key = PairKey(combo[i], combo[j]);
                    var resultant = pairCount.GetValueOrDefault(key) + 1;
                    if (resultant > ceiling)
                    {
                        admissible = false;
                        break;
                    }

                    sum += resultant;
                    if (resultant > worst)
                    {
                        worst = resultant;
                    }

                    if (protectedPairs.Contains(key) && ++violations > remainingBudget)
                    {
                        admissible = false;
                        break;
                    }
                }
            }

            if (admissible)
            {
                candidates.Add((combo, worst, sum, violations));
            }
        }

        // Stable sort: List.Sort is not, and Combinations already enumerates
        // in field order, so an unstable sort would silently break the
        // "ties broken by field order" determinism this algorithm promises.
        var ordered = candidates.OrderBy(c => c.WorstPairing).ThenBy(c => c.Sum);

        foreach (var (group, _, _, violations) in ordered)
        {
            var rest = unplaced.Where(c => !group.Contains(c)).ToImmutableArray();
            yield return (group, rest, violations);
        }
    }

    /// <summary>Every k-subset of items, preserving relative field order within each subset.</summary>
    private static IEnumerable<ImmutableArray<CompetitorId>> Combinations(ImmutableArray<CompetitorId> items, int k)
    {
        if (k == 0)
        {
            yield return ImmutableArray<CompetitorId>.Empty;
            yield break;
        }

        if (items.Length < k)
        {
            yield break;
        }

        var first = items[0];
        var rest = items.RemoveAt(0);

        foreach (var combo in Combinations(rest, k - 1))
        {
            yield return combo.Insert(0, first);
        }

        foreach (var combo in Combinations(rest, k))
        {
            yield return combo;
        }
    }

    private static void RecordPairings(
        ImmutableArray<ImmutableArray<CompetitorId>> groups,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount)
    {
        foreach (var group in groups)
        {
            for (var i = 0; i < group.Length; i++)
            {
                for (var j = i + 1; j < group.Length; j++)
                {
                    var key = PairKey(group[i], group[j]);
                    pairCount[key] = pairCount.GetValueOrDefault(key) + 1;
                }
            }
        }
    }

    /// <summary>Unordered pair key — canonicalised by Guid comparison, not field order.</summary>
    private static (CompetitorId, CompetitorId) PairKey(CompetitorId a, CompetitorId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a, b) : (b, a);
}
