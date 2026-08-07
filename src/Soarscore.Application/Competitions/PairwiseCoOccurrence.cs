// docs/plans/phase-drawn-steel-thread-plan.md WI-6a — so the organiser can
// see *why* a draw's pairings look the way they do, and judge whether to
// accept it (once the "Redrawing" thread the plan defers exists). A pure
// derivation over data the fold already has (Rounds -> TaskRound -> Group ->
// CompetitorRefs), not new domain state: no event shape change, nothing
// stored or denormalised. Lives here, not beside PhaseDraw.BuildGroups in
// Domain, despite the shared shape — this is a read-model derivation over
// already-folded state, not a decide function.

using System.Collections.Immutable;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Competitions;

/// <summary>One unordered competitor pair's meeting count across every group in every round of a phase's draw.</summary>
public sealed record PairwiseCoOccurrenceEntry(CompetitorId CompetitorA, CompetitorId CompetitorB, int Count);

public static class PairwiseCoOccurrence
{
    /// <summary>
    /// For every group in every round, increments a count for each unordered
    /// competitor pair co-located in it. Class-agnostic — counts pairs
    /// regardless of what class the competition runs — and cheap at
    /// CLAUDE.md's <=20 pilots / <=8 rounds ceiling.
    /// </summary>
    public static ImmutableDictionary<(CompetitorId, CompetitorId), int> Compute(ImmutableArray<Round> rounds)
    {
        var builder = ImmutableDictionary.CreateBuilder<(CompetitorId, CompetitorId), int>();

        foreach (var round in rounds)
        {
            foreach (var taskRound in round.TaskRounds)
            {
                foreach (var group in taskRound.Groups)
                {
                    var members = group.CompetitorRefs;
                    for (var i = 0; i < members.Length; i++)
                    {
                        for (var j = i + 1; j < members.Length; j++)
                        {
                            var key = PairKey(members[i], members[j]);
                            builder[key] = builder.TryGetValue(key, out var count) ? count + 1 : 1;
                        }
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// <see cref="Compute"/>'s result as a flat, deterministically ordered
    /// list — the shape a JSON response needs (a dictionary keyed on a tuple
    /// has no faithful JSON encoding), ordered by the pair's ids so the
    /// response is stable across repeated calls against the same draw.
    /// </summary>
    public static ImmutableArray<PairwiseCoOccurrenceEntry> ComputeEntries(ImmutableArray<Round> rounds) =>
        Compute(rounds)
            .Select(kvp => new PairwiseCoOccurrenceEntry(kvp.Key.Item1, kvp.Key.Item2, kvp.Value))
            .OrderBy(e => e.CompetitorA.Value)
            .ThenBy(e => e.CompetitorB.Value)
            .ToImmutableArray();

    /// <summary>Unordered pair key — canonicalised by Guid comparison, mirroring PhaseDraw's own PairKey.</summary>
    private static (CompetitorId, CompetitorId) PairKey(CompetitorId a, CompetitorId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a, b) : (b, a);
}
