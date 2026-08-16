// PenaltyEngine — kanban/completed/scoring-service-plan.md WI-6.
//
// Applies penalties at both the raw-score level (ZeroFlight, ZeroRound, ZeroTask)
// and the final-aggregate level (DeductPoints, Disqualify), with exclusion-group
// semantics and accrual. Exclusion-group suppression is single-pass: compute all
// accrued contributions first, suppress, then apply survivors. The result does
// not depend on evaluation order.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Applies penalties at both pipeline stages. The stage is derived from the
/// effect enum — there is no appliedAt attribute (design rule #6).
/// </summary>
public static class PenaltyEngine
{
    /// <summary>
    /// Apply raw-score penalties (ZeroFlight, ZeroRound, ZeroTask).
    /// Called BEFORE normalisation. If any matched penalty definition has
    /// a zeroing effect, the TaskResult is zeroed to NoResult so that
    /// the penalized competitor is excluded from winner-finding (design rule #4).
    /// </summary>
    /// <param name="result">The task result to potentially zero.</param>
    /// <param name="penalties">Recorded penalties scoped to this Entry/TaskRound.</param>
    /// <param name="definitions">Penalty definitions from the adopted rules.</param>
    public static TaskResult ApplyRawPenalties(
        TaskResult result,
        ImmutableArray<RecordedPenalty> penalties,
        ImmutableArray<PenaltyDefinition> definitions)
    {
        if (penalties.IsDefaultOrEmpty)
            return result;

        var defLookup = BuildDefinitionLookup(definitions);

        foreach (var penalty in penalties)
        {
            if (!defLookup.TryGetValue(penalty.InfractionType, out var def))
                continue;

            foreach (var effect in def.Effects)
            {
                if (effect.Effect is PenaltyEffect.ZeroFlight
                                 or PenaltyEffect.ZeroRound
                                 or PenaltyEffect.ZeroTask)
                {
                    // Zeroed by a raw penalty → NoResult so the competitor
                    // is excluded from normalisation's winner finding.
                    return result with
                    {
                        State = TaskResultState.NoResult,
                        Selection = null,
                        RawScore = 0m
                    };
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Apply aggregate penalties (DeductPoints, Disqualify).
    /// Called AFTER drops, BEFORE ranking.
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

        // 1. Match penalties to definitions, compute total accrued contribution per definition.
        //    Also track which definitions have a Disqualify effect.
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

        if (contributions.Count == 0)
            return new PenaltyApplication(Deduction: 0m, Disqualified: false);

        // 2. Exclusion-group suppression (single-pass).
        var surviving = ResolveExclusion(contributions);

        // 3. Sum deductions from surviving DeductPoints effects.
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
