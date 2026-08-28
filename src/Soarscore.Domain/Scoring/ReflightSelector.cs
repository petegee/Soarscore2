// ReflightSelector — kanban/in-progress/reflight-groups.md WI-6a; the
// destination-aware shape law is reflight-aggregate-destination.md WI-1.
//
// The pure rule that collapses a competitor's one or two live Entries for a
// task-round to the ONE score that counts for that competitor — per
// destination, since a make-up entry counts for an earlier round while its
// task-mates count for their own (D6). Holds no Entry dependency and owns the
// shape law, so both ScoreCompetition and the shape guard share it (WI-6b,
// finding 9's collapse-to-one invariant, R1 — restated per destination as R1′
// by reflight-aggregate-destination.md).
//
// The applicable selection is read from the class's ReflightRule resolved as
// data — never a branch on class (CLAUDE.md's core architectural law). The
// two-role rule (soaring-domain-class-diagram.md, "Reflight scoring is per
// role, not per class"): the entitled competitor's re-flight is official even
// if worse (`Entitled` → EntitledScores), every other pilot takes the better
// of their two attempts (`Filler` → OthersScore).

using Soarscore.Domain;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// The selection law and the shape law for reflight scoring. Static, not
/// instantiable — it owns no state, like every other stage in this namespace.
/// </summary>
public static class ReflightSelector
{
    /// <summary>
    /// The entry-based key scoring uses once a competitor can hold two live
    /// entries in one group (finding 7) — the old competitor-string key would
    /// collide on the now-legal Original + reflight-role pairing. The format is
    /// defined here so both call sites (ScoreCompetition, ScoreTaskRound) use
    /// the identical format (WI-6b, WI-7).
    /// </summary>
    public static string EntryKey(Entry entry) =>
        $"{entry.CompetitorRef}|{entry.Id}";

    /// <summary>
    /// Is this shape of live roles for one competitor legal? Either a single
    /// entry of any role, or exactly one Original plus exactly one
    /// reflight-role entry — the two shapes the reflight rules describe.
    /// Anything else (two same-role entries, three or more) is a corruption.
    /// </summary>
    public static bool ShapePermits(IReadOnlyList<ReflightRole> roles) =>
        roles.Count == 1
        || (roles.Count == 2
            && roles.Count(r => r == ReflightRole.Original) == 1
            && roles.Count(r => r != ReflightRole.Original) == 1);

    /// <summary>
    /// The destination-aware shape law (reflight-aggregate-destination.md WI-1,
    /// D6's three bullets). Each live entry's destination is its counts-for
    /// round, resolved to the entry's own <paramref name="roundOrdinal"/> when
    /// the datum is null; then:
    /// <list type="bullet">
    /// <item>an Original counts for its own round, always — an Original naming
    /// another round is corruption (bullet 1);</item>
    /// <item>an explicit counts-for on a reflight-role entry must name an
    /// earlier round of the phase — a non-earlier one is a shape violation
    /// here, refused with the same <c>score.reflightShapeUnsupported</c> as
    /// any other corruption, so a bad destination is never scored silently
    /// (bullet 2; the write side's openEntry.* codes are WI-2);</item>
    /// <item>per (competitor, task-round, destination) the live entries must
    /// be exactly one entry of any role, or exactly one Original plus exactly
    /// one reflight-role entry — the two-role law applied per destination
    /// (bullet 3). This implies at most one Original per (competitor,
    /// task-round), and it accepts the comp-135 shape: an Original plus two
    /// make-ups with distinct destinations in one task-round.</item>
    /// </list>
    /// When no entry carries a counts-for, the law reduces exactly to
    /// <see cref="ShapePermits(IReadOnlyList{ReflightRole})"/> over the whole
    /// list — the R6 regression guarantee.
    /// </summary>
    public static bool ShapePermits(int roundOrdinal, IReadOnlyList<(ReflightRole Role, int? CountsFor)> liveEntries)
    {
        foreach (var (role, countsFor) in liveEntries)
        {
            if (countsFor is not { } destination)
                continue; // null resolves to the entry's own round — always legal

            if (role == ReflightRole.Original)
            {
                if (destination != roundOrdinal)
                    return false;
            }
            else if (destination < 1 || destination >= roundOrdinal)
            {
                return false;
            }
        }

        return liveEntries
            .GroupBy(e => e.CountsFor ?? roundOrdinal)
            .All(destinationGroup => ShapePermits(destinationGroup.Select(e => e.Role).ToList()));
    }

    /// <summary>
    /// Collapse one competitor's candidates to their score for this task-round.
    /// A single candidate is returned unchanged (the ordinary, non-reflight
    /// case, and the lone-entry-after-annulment shape). Two candidates with one
    /// Original and one reflight-role entry select per the class's
    /// <see cref="ReflightRule"/>: Replacement takes the re-flight, BetterOf
    /// takes the better of the two normalised scores, NotPermitted and
    /// UndefinedRequiresRuling fail honestly rather than assume.
    /// <para>
    /// <paramref name="ruledSelection"/> (reflight-scoring-rulings.md WI-3) is a
    /// recorded CD ruling — it fills a silence only: where the role-applicable
    /// class slot is NOT UndefinedRequiresRuling it is ignored entirely (RR1,
    /// the rulebook always beats the CD). It reaches here only from a validated
    /// <see cref="ReflightRuling"/> (Replacement/BetterOf), but the switch's
    /// exhaustiveness keeps the method total regardless of what is passed.
    /// </para>
    /// </summary>
    public static Result<decimal> Select(
        IReadOnlyList<(ReflightRole Role, decimal Score)> candidates,
        ReflightRule rule,
        ReflightSelection? ruledSelection = null)
    {
        if (candidates.Count == 1)
        {
            return Result<decimal>.Success(candidates[0].Score);
        }

        // Shape corruption is the caller's guard's job (ShapePermits); this is
        // the belt-and-braces that keeps the 2-candidate law below total.
        if (candidates.Count != 2)
        {
            return Result<decimal>.Failure(
                "score.reflightShapeUnsupported",
                $"A competitor holds {candidates.Count} live entries for one task-round. "
                + $"Expected one, or an Original paired with one reflight role (roles seen: "
                + $"{string.Join(", ", candidates.Select(c => c.Role))}).");
        }

        var isEntitled = candidates.Any(c => c.Role == ReflightRole.Entitled);
        var reflight = candidates.First(c => c.Role != ReflightRole.Original);
        var original = candidates.First(c => c.Role == ReflightRole.Original);
        var selection = isEntitled ? rule.EntitledScores : rule.OthersScore;

        // RR1: a ruling fills silences only — where the class speaks, it governs.
        if (selection == ReflightSelection.UndefinedRequiresRuling && ruledSelection is { } ruled)
        {
            selection = ruled;
        }

        return selection switch
        {
            ReflightSelection.Replacement => Result<decimal>.Success(reflight.Score),
            ReflightSelection.BetterOf => Result<decimal>.Success(Math.Max(original.Score, reflight.Score)),
            ReflightSelection.NotPermitted => Result<decimal>.Failure(
                "score.reflightNotPermitted",
                "This class permits no re-flights; a reflight-role entry was captured anyway."),
            ReflightSelection.UndefinedRequiresRuling => Result<decimal>.Failure(
                "score.reflightRequiresRuling",
                "This class is silent on which of a competitor's attempts counts when a re-flight "
                + "is flown; a CD ruling is required, and none has been recorded."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(selection), selection, "Unknown ReflightSelection."),
        };
    }
}