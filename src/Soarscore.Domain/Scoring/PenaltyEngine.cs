// PenaltyEngine — kanban/completed/scoring-service-plan.md WI-6; stage routing
// amended per D1: kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-1;
// raw-stage Disqualify flag per
// kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-2.
//
// Applies penalties at both pipeline stages, with exclusion-group semantics and
// accrual. The stage follows the RECORDED SCOPE of each penalty record (D1):
// Flight/Entry-scoped records act entirely at the task-round stage (raw,
// pre-normalisation) — all their declared effects, DeductPoints included, land
// there; TaskRound/Competition-scoped records act at the final aggregate as
// before. Every declared effect now acts at its owning stage: Zero* → NoResult,
// DeductPoints → subtract, Disqualify → the RawPenaltyApplication flag carried
// to final assembly (D-B2). Exclusion-group suppression is single-pass:
// compute all accrued contributions first, suppress, then apply survivors. The
// result does not depend on evaluation order.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Applies penalties at both pipeline stages. There is no appliedAt attribute
/// (design rule #6); instead the pipeline stage follows the RECORDED penalty's
/// SCOPE (D1): Flight/Entry-scoped records are owned by the task-round stage
/// (raw/pre-normalisation), TaskRound/Competition-scoped records by the final
/// aggregate — and every declared effect acts within its owning stage
/// (kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-2).
/// </summary>
public static class PenaltyEngine
{
    /// <summary>
    /// Apply ALL effects of Flight/Entry-scoped records at the task-round stage —
    /// ZeroFlight/ZeroRound/ZeroTask, DeductPoints AND Disqualify (D1, D-B1).
    /// Called BEFORE normalisation. Returns the possibly-penalised TaskResult
    /// plus the Disqualify flag, which is flag-only (D-B2): no arithmetic
    /// change, OR-accumulated at final assembly into
    /// FinalCompetitorScore.Disqualified.
    ///
    /// A matched DeductPoints effect subtracts pre-normalisation, so the
    /// deducted raw feeds winner-finding directly: the group's 1000 anchors on
    /// the best deducted raw — GliderScore's late-landing placement
    /// (varFltDednIdx=1, FltPenalty inside RawScore).
    ///
    /// Floor (D4): a deducted HigherIsBetter raw never goes below zero — the
    /// task-round analogue of FAI General §6 / C.19 ("a score that would go
    /// negative is recorded as zero"). For LowerIsBetter tasks (F3B speed /
    /// F3F) a declared-points pre-normalisation deduction has no rule-grounded
    /// meaning in either rulebook, and no rulebook or fixture exercises it
    /// today — surface it if one ever does. TaskResult carries no direction, so
    /// the arithmetic below is direction-blind: the same subtract-and-floor
    /// applies either way.
    ///
    /// A Zero* effect zeroes to NoResult so the penalized competitor is
    /// excluded from winner-finding (design rule #4); the Disqualify flag
    /// survives that early-out (D-B3), so a Zero* + Disqualify definition
    /// yields NoResult AND the flag — both declared effects acted.
    ///
    /// The Disqualify flag does not zero: aggregate-stage Disqualify sets
    /// FinalCompetitorScore.Disqualified without arithmetic change, so
    /// entry-scoped Disqualify does not either (D-B2). Unmatched infraction
    /// types remain skipped (events-already-in-log safety net).
    /// </summary>
    /// <param name="result">The task result to potentially penalise or zero.</param>
    /// <param name="penalties">Recorded penalties scoped to this Entry/TaskRound.</param>
    /// <param name="definitions">Penalty definitions from the adopted rules.</param>
    public static RawPenaltyApplication ApplyRawPenalties(
        TaskResult result,
        ImmutableArray<RecordedPenalty> penalties,
        ImmutableArray<PenaltyDefinition> definitions)
    {
        // NoResult input stays untouched: penalties have nothing valid left to act on.
        if (result.State is not TaskResultState.Valid)
            return new RawPenaltyApplication(result, Disqualified: false);

        if (penalties.IsDefaultOrEmpty)
            return new RawPenaltyApplication(result, Disqualified: false);

        var defLookup = BuildDefinitionLookup(definitions);

        // D2: same accrual path as the aggregate stage.
        var contributions = Accrue(penalties, defLookup);

        if (contributions.Count == 0)
            return new RawPenaltyApplication(result, Disqualified: false);

        var surviving = ResolveExclusion(contributions);

        // D-B3: the Disqualify accrual must be known BEFORE the Zero* scan so
        // the early-out below can carry the flag — a Zero* + Disqualify
        // definition then yields NoResult AND the flag, both declared effects
        // acted (kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-2).
        var anyDisqualify = contributions.Values.Any(i => i.HasDisqualify);

        // Zero-dominance (D3), checked across ALL contributed definitions,
        // suppressed or not: Zero*-carrying definitions cannot join exclusion
        // groups (adoption check), so suppression cannot touch them.
        foreach (var def in contributions.Keys)
        {
            foreach (var effect in def.Effects)
            {
                if (effect.Effect is PenaltyEffect.ZeroFlight
                                 or PenaltyEffect.ZeroRound
                                 or PenaltyEffect.ZeroTask)
                {
                    // Zeroed by a raw penalty → NoResult so the competitor
                    // is excluded from normalisation's winner finding. The
                    // Disqualify flag rides along (D-B3) — the flag does not
                    // zero (D-B2).
                    return new RawPenaltyApplication(result with
                    {
                        State = TaskResultState.NoResult,
                        Selection = null,
                        RawScore = 0m
                    }, anyDisqualify);
                }
            }
        }

        // Disqualify (D-B1/D-B2): the accrued flag (HasDisqualify, possibly
        // computed above) is now actioned as the returned flag — flag-only,
        // no score change, carried to final assembly via ScoreGroup's walk.
        // A Disqualify-carrying definition can never be suppressed out of
        // flagging: exclusion groups admit only all-DeductPoints definitions
        // (adoption check 16, D-B4).

        decimal totalDeduction = 0m;
        foreach (var (def, info) in contributions)
        {
            if (!surviving.Contains(def))
                continue;

            totalDeduction += info.TotalDeduction;
        }

        // Subtracted pre-normalisation (feeds winner finding); floored per D4 —
        // HigherIsBetter analogue of FAI General §6 / C.19. State stays Valid,
        // Selection untouched: normalisation and reflight collapse read them.
        return new RawPenaltyApplication(
            result with { RawScore = Math.Max(0m, result.RawScore - totalDeduction) },
            anyDisqualify);
    }

    /// <summary>
    /// Apply aggregate penalties (DeductPoints, Disqualify).
    /// Called AFTER drops, BEFORE ranking.
    /// TaskRound/Competition-scoped records land here (D1); entry-scoped
    /// records are handled by ApplyRawPenalties instead — the two functions
    /// receive disjoint inputs, so there is no double-count.
    /// </summary>
    /// <param name="score">The competitor's aggregate score after drops.</param>
    /// <param name="penalties">Recorded penalties scoped to Competition level.</param>
    /// <param name="definitions">Penalty definitions from the adopted rules.</param>
    public static PenaltyApplication ApplyAggregatePenalties(
        decimal score,
        ImmutableArray<RecordedPenalty> penalties,
        ImmutableArray<PenaltyDefinition> definitions)
    {
        if (penalties.IsDefaultOrEmpty)
            return new PenaltyApplication(Deduction: 0m, Disqualified: false);

        var defLookup = BuildDefinitionLookup(definitions);

        var contributions = Accrue(penalties, defLookup);

        if (contributions.Count == 0)
            return new PenaltyApplication(Deduction: 0m, Disqualified: false);

        // Exclusion-group suppression (single-pass).
        var surviving = ResolveExclusion(contributions);

        // Sum deductions from surviving DeductPoints effects.
        decimal totalDeduction = 0m;
        bool disqualified = false;

        foreach (var (def, info) in contributions)
        {
            if (!surviving.Contains(def))
                continue;

            totalDeduction += info.TotalDeduction;
            if (info.HasDisqualify)
                disqualified = true;
        }

        return new PenaltyApplication(
            Deduction: totalDeduction,
            Disqualified: disqualified
        );
    }

    /// <summary>
    /// Shared accrual for both pipeline stages (D2 — identical accrual and
    /// exclusion semantics at raw and aggregate):
    /// kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-1.
    /// Matches penalties to definitions via the lookup and computes the total
    /// accrued contribution per definition, also tracking which definitions
    /// have a Disqualify effect.
    /// </summary>
    private static Dictionary<PenaltyDefinition, AccruedInfo> Accrue(
        ImmutableArray<RecordedPenalty> penalties,
        Dictionary<string, PenaltyDefinition> defLookup)
    {
        // Match penalties to definitions, compute total accrued contribution per definition.
        // Also track which definitions have a Disqualify effect.
        var contributions = new Dictionary<PenaltyDefinition, AccruedInfo>();

        foreach (var penalty in penalties)
        {
            if (!defLookup.TryGetValue(penalty.InfractionType, out var def))
                continue;

            if (!contributions.TryGetValue(def, out var info))
            {
                info = new AccruedInfo();
                contributions[def] = info;
            }

            foreach (var effect in def.Effects)
            {
                if (effect.Effect == PenaltyEffect.DeductPoints && effect.Points.HasValue)
                {
                    decimal contribution = def.Accrual == PenaltyAccrual.PerOccurrence
                        ? effect.Points.Value * penalty.OccurrenceCount
                        : effect.Points.Value;  // OncePerAttempt

                    info.TotalDeduction += contribution;
                }
                else if (effect.Effect == PenaltyEffect.Disqualify)
                {
                    info.HasDisqualify = true;
                }
            }
        }

        return contributions;
    }

    /// <summary>
    /// Compute which penalty definitions survive exclusion-group suppression.
    /// Single-pass: compute per-group winners from original contributions,
    /// then a definition survives only if it is the max winner in EVERY
    /// exclusion group it belongs to.
    /// </summary>
    internal static HashSet<PenaltyDefinition> ResolveExclusion(
        Dictionary<PenaltyDefinition, AccruedInfo> contributions)
    {
        // Only consider definitions that are in at least one exclusion group
        // and have a non-zero contribution.
        var active = contributions
            .Where(kv => kv.Key.ExclusionGroups.Length > 0 && kv.Value.TotalDeduction > 0)
            .ToList();

        if (active.Count == 0)
            return new HashSet<PenaltyDefinition>(contributions.Keys);  // all survive

        // Build per-group view: groupName → list of (definition, contribution)
        var groups = new Dictionary<string, List<(PenaltyDefinition Def, decimal Contribution)>>();

        foreach (var (def, info) in active)
        {
            foreach (var groupName in def.ExclusionGroups)
            {
                if (!groups.TryGetValue(groupName, out var list))
                {
                    list = new List<(PenaltyDefinition, decimal)>();
                    groups[groupName] = list;
                }
                list.Add((def, info.TotalDeduction));
            }
        }

        // For each group, find the max contribution.
        var groupMax = new Dictionary<string, decimal>();
        foreach (var (name, list) in groups)
        {
            groupMax[name] = list.Max(x => x.Contribution);
        }

        // A definition survives only if, in EVERY group it belongs to,
        // its contribution is the max for that group (and max > 0).
        var suppressed = new HashSet<PenaltyDefinition>();

        foreach (var (def, info) in active)
        {
            foreach (var groupName in def.ExclusionGroups)
            {
                decimal max = groupMax[groupName];
                if (info.TotalDeduction < max)
                {
                    // This definition is NOT the max in this group → suppressed.
                    suppressed.Add(def);
                    break;
                }
            }
        }

        // All non-excluded definitions survive, plus any definitions not in
        // any exclusion group survive unconditionally.
        var surviving = new HashSet<PenaltyDefinition>();
        foreach (var (def, _) in contributions)
        {
            if (!suppressed.Contains(def))
                surviving.Add(def);
        }

        return surviving;
    }

    // --------------------------------------------------------- private

    private static Dictionary<string, PenaltyDefinition> BuildDefinitionLookup(
        ImmutableArray<PenaltyDefinition> definitions)
    {
        var lookup = new Dictionary<string, PenaltyDefinition>();
        foreach (var def in definitions)
        {
            if (!lookup.ContainsKey(def.InfractionType))
                lookup[def.InfractionType] = def;
        }
        return lookup;
    }

    /// <summary>Accrued information for one penalty definition.</summary>
    internal sealed class AccruedInfo
    {
        public decimal TotalDeduction { get; set; }
        public bool HasDisqualify { get; set; }
    }
}
