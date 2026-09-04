// Validate() — the twenty adoption checks (17–19: tie-break ladder,
// kanban/in-progress/tie-break-policy-in-class-definition.md WI-2; 20:
// kanban/completed/permitted-scopes-on-penalty-definitions.md#wi-2).
// kanban/completed/class-definition-adoption-steel-thread-plan.md
// WI-2, LADR-0002 §4 ("deserialise -> Validate -> canonicalise+hash -> append"),
// docs/high-level-architecture.md "Validated at adoption" (the numbered, canonical
// inventory this file implements — cited by number here, not restated).
//
// Total and non-throwing on well-typed input: every check runs regardless of
// whether earlier checks found anything, and Validate() returns every Defect
// found, not the first (LADR-0002 §4). Placed in Application, not Domain — see
// ClassDefinitionHashing.cs's precedent in this same folder: validation and
// hashing are two steps of one ingestion pipeline, not an aggregate invariant.
//
// Checks 4 and, in the FAI-corpus-only sense checks 5/6, are worth a word on
// why the numbered list below has no method doing real work for two of them:
//
//   Check 4 ("a ParameterRef occurs only in the thirteen permitted slots") has
//   no method at all — ParameterReference.cs's header comment: typing exactly
//   those slots as NumberOrParam/FlagOrParam and every other numeric slot as
//   plain `decimal` makes the violation unrepresentable in the type system, the
//   same way the two checks that left the sixteen (high-level-architecture.md)
//   did when a subtype absorbed what they used to guard.
//
//   Checks 5 and 6 ("every `use` names a declared class-scope group" / "every
//   declared group is named by a `use`") guard `metricSet`/`rows`/`bands`/`use`
//   — notation §7.1 sugar that expands away before a ClassDefinition exists
//   (ClassDefinition.cs's own header: "Nothing here is a notation construct").
//   LADR-0002 §3 builds no notation parser into the core, so the wire format
//   this Validate() runs against — canonical JSON, for a seed class and a user
//   POST alike — is always the already-expanded model. There is no `use` site
//   and no class-scope group left to check by the time a ClassDefinition
//   exists to call Validate() on. Kept as named, permanently-empty methods
//   below (not omitted) so a reader grepping "check-5"/"check-6" finds the
//   reasoning rather than a silent gap in the 1-19 sequence.

using System.Collections.Immutable;
using Soarscore.Domain;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.CompetitionClasses;

public static class ClassDefinitionValidation
{
    private const string SecondsUnit = "s";

    /// <summary>The one closed intrinsic flight fact (F6, notation §5) — always resolvable, never declared as a MetricDefinition.</summary>
    private const string FlightSequenceIntrinsic = "flight.sequence";

    public static IReadOnlyList<Defect> Validate(ClassDefinition definition)
    {
        var defects = new List<Defect>();

        CheckMetricReferencesResolve(definition, defects);
        CheckRankByMetricResolves(definition, defects);
        CheckParameterReferencesResolve(definition, defects);
        CheckUseNamesDeclaredGroup(definition, defects);
        CheckDeclaredGroupIsUsed(definition, defects);
        CheckParameterUnitAgreement(definition, defects);
        CheckAdjacentPiecewiseBandsMeet(definition, defects);
        CheckLookupRowsWellFormed(definition, defects);
        CheckDropPolicyGatesDescending(definition, defects);
        CheckFinalRankingNotSinglePhaseWithMultiplePhases(definition, defects);
        CheckFinalRankingRequiredForMultiplePhases(definition, defects);
        CheckReflightMinNewGroupSizeNotWithNoReflight(definition, defects);
        CheckNormalisedTermsRequireNormalisation(definition, defects);
        CheckNormalisationRequiresGroup(definition, defects);
        CheckExclusionGroupsAreDeductOnly(definition, defects);
        CheckPermittedScopesNotEmpty(definition, defects);
        CheckQualifyingPositionSourceIsEarlierPhase(definition, defects);
        CheckUndefinedRequiresRulingStandsAlone(definition, defects);
        CheckEqualPlacesStandsAlone(definition, defects);
        CheckBestDroppedScoreRequiresDropPolicy(definition, defects);

        return defects;
    }

    /// <summary>Check 1 — every metric a ScoreTerm or Predicate names resolves to a MetricDefinition on that task (notation §9).</summary>
    private static void CheckMetricReferencesResolve(ClassDefinition definition, List<Defect> defects)
    {
        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            var declared = task.Metrics.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
            declared.Add(FlightSequenceIntrinsic);

            foreach (var (termPath, term) in AllTermsOf(taskPath, task))
            {
                foreach (var (refPath, metricRef) in OwnMetricRef(termPath, term))
                {
                    if (!declared.Contains(metricRef))
                    {
                        defects.Add(new Defect("class-definition.check-1.unresolved-metric-ref", refPath,
                            $"Metric '{metricRef}' is not declared on task '{task.Code}'."));
                    }
                }
            }

            foreach (var (predPath, predicate) in AllPredicates(taskPath, task))
            {
                if (predicate is not Comparison comparison)
                {
                    continue;
                }

                if (!declared.Contains(comparison.LeftMetricRef))
                {
                    defects.Add(new Defect("class-definition.check-1.unresolved-metric-ref", $"{predPath}.leftMetricRef",
                        $"Metric '{comparison.LeftMetricRef}' is not declared on task '{task.Code}'."));
                }

                if (comparison.RightMetricRef is { } rightMetricRef && !declared.Contains(rightMetricRef))
                {
                    defects.Add(new Defect("class-definition.check-1.unresolved-metric-ref", $"{predPath}.rightMetricRef",
                        $"Metric '{rightMetricRef}' is not declared on task '{task.Code}'."));
                }
            }
        }
    }

    /// <summary>Check 2 — BestNFlights.rankByMetric resolves to a metric declared on that task (diagram §3).</summary>
    private static void CheckRankByMetricResolves(ClassDefinition definition, List<Defect> defects)
    {
        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            if (task.Flights is BestNFlights { RankByMetric: { } metric } && task.Metrics.All(m => m.Name != metric))
            {
                defects.Add(new Defect("class-definition.check-2.unresolved-rank-by-metric", $"{taskPath}.flights.rankByMetric",
                    $"rankByMetric '{metric}' is not declared on task '{task.Code}'."));
            }
        }
    }

    /// <summary>
    /// Check 3 — every ParameterRef resolves to a declared Parameter (notation §3).
    /// One way only: a Parameter no ref names is legal (F3F.1.5) and is
    /// deliberately not checked here — there is no orphan-parameter analogue of
    /// check 6. The "bound before the pipeline stage that reads it" half of the
    /// notation's wording is a Competition-time (ParameterBinding) fact, not a
    /// property of a ClassDefinition in isolation — it has nothing to check
    /// against here and is left to the Competition side of the system.
    /// </summary>
    private static void CheckParameterReferencesResolve(ClassDefinition definition, List<Defect> defects)
    {
        var declared = definition.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        CheckNumberRef(definition.Reflight.MinNewGroupSize, "$.reflight.minNewGroupSize", declared, defects);

        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var phase = definition.Phases[p];
            var phasePath = $"$.phases[{p}]";

            // ValidityRule.MinTasks is a plain int? — not one of the thirteen
            // ParameterRef-permitted slots (ClassDefinition.cs), so there is
            // nothing here for check 3 to resolve.
            CheckNumberRef(phase.Validity.MinRounds, $"{phasePath}.validity.minRounds", declared, defects);

            if (phase.Promotion is { } promotion)
            {
                CheckNumberRef(promotion.TopN, $"{phasePath}.promotion.topN", declared, defects);
                CheckNumberRef(promotion.MinGroupSize, $"{phasePath}.promotion.minGroupSize", declared, defects);
                CheckNumberRef(promotion.MaxGroupSize, $"{phasePath}.promotion.maxGroupSize", declared, defects);
                CheckFlagRef(promotion.CarryPenalties, $"{phasePath}.promotion.carryPenalties", declared, defects);
            }

            for (var t = 0; t < phase.Tasks.Length; t++)
            {
                var task = phase.Tasks[t];
                var taskPath = $"{phasePath}.tasks[{t}]";

                CheckNumberRef(task.Timing.WorkingTime, $"{taskPath}.timing.workingTime", declared, defects);
                CheckNumberRef(task.Timing.MaxLaunches, $"{taskPath}.timing.maxLaunches", declared, defects);

                if (task.Group is { } group)
                {
                    CheckNumberRef(group.MinPerGroup, $"{taskPath}.group.minPerGroup", declared, defects);
                }

                if (task.Reflight is { } reflight)
                {
                    CheckNumberRef(reflight.MinNewGroupSize, $"{taskPath}.reflight.minNewGroupSize", declared, defects);
                }

                foreach (var (termPath, term) in AllTermsOf(taskPath, task))
                {
                    switch (term)
                    {
                        case RateTerm rate:
                            CheckNumberRef(rate.Cap, $"{termPath}.cap", declared, defects);
                            break;
                        case PiecewiseTerm piecewise:
                            CheckNumberRef(piecewise.Origin, $"{termPath}.origin", declared, defects);
                            for (var b = 0; b < piecewise.Bands.Length; b++)
                            {
                                CheckNumberRef(piecewise.Bands[b].From, $"{termPath}.bands[{b}].from", declared, defects);
                                CheckNumberRef(piecewise.Bands[b].To, $"{termPath}.bands[{b}].to", declared, defects);
                            }

                            break;
                    }
                }
            }
        }
    }

    /// <summary>Check 5 — unrepresentable against the expanded wire model. See the file header.</summary>
    private static void CheckUseNamesDeclaredGroup(ClassDefinition definition, List<Defect> defects)
    {
        // No-op — see the file header comment.
    }

    /// <summary>Check 6 — unrepresentable against the expanded wire model. See the file header.</summary>
    private static void CheckDeclaredGroupIsUsed(ClassDefinition definition, List<Defect> defects)
    {
        // No-op — see the file header comment.
    }

    /// <summary>
    /// Check 7 — where a ParameterRef-consuming slot has a unit of its own, the
    /// referenced Parameter states that same unit (notation §3, diagram §2).
    /// Five of the thirteen slots carry a unit: TaskTiming.workingTime (always
    /// seconds) and RateTerm.cap / PiecewiseTerm.origin / Band.from / Band.to
    /// (the unit of the metric the term reads, where that metric states one).
    /// The other eight slots are counts or a Flag and require no unit either way.
    /// </summary>
    private static void CheckParameterUnitAgreement(ClassDefinition definition, List<Defect> defects)
    {
        var declared = definition.Parameters.ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            var metricsByName = task.Metrics.ToDictionary(m => m.Name, StringComparer.Ordinal);

            CheckUnit(task.Timing.WorkingTime, SecondsUnit, $"{taskPath}.timing.workingTime", declared, defects);

            foreach (var (termPath, term) in AllTermsOf(taskPath, task))
            {
                switch (term)
                {
                    case RateTerm rate when metricsByName.TryGetValue(rate.MetricRef, out var metric) && metric.Unit is not null:
                        CheckUnit(rate.Cap, metric.Unit, $"{termPath}.cap", declared, defects);
                        break;

                    case PiecewiseTerm piecewise when metricsByName.TryGetValue(piecewise.MetricRef, out var metric) && metric.Unit is not null:
                        CheckUnit(piecewise.Origin, metric.Unit, $"{termPath}.origin", declared, defects);
                        for (var b = 0; b < piecewise.Bands.Length; b++)
                        {
                            CheckUnit(piecewise.Bands[b].From, metric.Unit, $"{termPath}.bands[{b}].from", declared, defects);
                            CheckUnit(piecewise.Bands[b].To, metric.Unit, $"{termPath}.bands[{b}].to", declared, defects);
                        }

                        break;
                }
            }
        }
    }

    /// <summary>
    /// Check 8 — adjacent piecewise bands meet (F27, notation §3): where one
    /// band's `to` and the next band's `from` are both ParameterRefs, they must
    /// name the same Parameter. Literal-to-literal bounds are out of this
    /// check's scope, per the check's own wording.
    /// </summary>
    private static void CheckAdjacentPiecewiseBandsMeet(ClassDefinition definition, List<Defect> defects)
    {
        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            foreach (var (termPath, term) in AllTermsOf(taskPath, task))
            {
                if (term is not PiecewiseTerm piecewise)
                {
                    continue;
                }

                var bands = piecewise.Bands;
                for (var i = 1; i < bands.Length; i++)
                {
                    if (bands[i - 1].To is NumberOrParam.Ref prevTo
                        && bands[i].From is NumberOrParam.Ref currFrom
                        && !string.Equals(prevTo.ParameterName, currFrom.ParameterName, StringComparison.Ordinal))
                    {
                        defects.Add(new Defect("class-definition.check-8.piecewise-bands-do-not-meet", $"{termPath}.bands[{i}].from",
                            $"Band {i}'s from-parameter '{currFrom.ParameterName}' does not match band {i - 1}'s to-parameter '{prevTo.ParameterName}'."));
                    }
                }
            }
        }
    }

    /// <summary>Check 9 — lookup rows ascend, at most one row is unbounded, and an unbounded row is last (F9, notation §5).</summary>
    private static void CheckLookupRowsWellFormed(ClassDefinition definition, List<Defect> defects)
    {
        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            foreach (var (termPath, term) in AllTermsOf(taskPath, task))
            {
                if (term is not LookupTerm lookup)
                {
                    continue;
                }

                var rows = lookup.Rows;
                for (var i = 0; i < rows.Length; i++)
                {
                    if (rows[i].UpTo is null && i != rows.Length - 1)
                    {
                        defects.Add(new Defect("class-definition.check-9.unbounded-row-not-last", $"{termPath}.rows[{i}]",
                            "An unbounded lookup row (upTo omitted) must be the last row."));
                    }

                    if (i > 0 && rows[i - 1].UpTo is { } prevUpTo && rows[i].UpTo is { } thisUpTo && thisUpTo <= prevUpTo)
                    {
                        defects.Add(new Defect("class-definition.check-9.rows-not-ascending", $"{termPath}.rows[{i}]",
                            "Lookup rows must ascend strictly."));
                    }
                }
            }
        }
    }

    /// <summary>Check 10 — a phase's ordered DropPolicy list has strictly descending gates (F22, notation §4).</summary>
    private static void CheckDropPolicyGatesDescending(ClassDefinition definition, List<Defect> defects)
    {
        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var drops = definition.Phases[p].Drops;
            for (var i = 1; i < drops.Length; i++)
            {
                var prev = drops[i - 1];
                var curr = drops[i];
                var path = $"$.phases[{p}].drops[{i}]";

                if (prev.ApplyWhenRoundsCompletedAtLeast is { } prevRounds
                    && curr.ApplyWhenRoundsCompletedAtLeast is { } currRounds
                    && currRounds >= prevRounds)
                {
                    defects.Add(new Defect("class-definition.check-10.drops-not-descending", path,
                        "Drop policy gates must be strictly descending (whenRoundsCompletedAtLeast)."));
                }

                if (prev.ApplyWhenResultsAtLeast is { } prevResults
                    && curr.ApplyWhenResultsAtLeast is { } currResults
                    && currResults >= prevResults)
                {
                    defects.Add(new Defect("class-definition.check-10.drops-not-descending", path,
                        "Drop policy gates must be strictly descending (whenResultsAtLeast)."));
                }
            }
        }
    }

    /// <summary>Check 11 — finalRanking written as SinglePhase on a class with more than one phase is rejected (notation §3, diagram §2).</summary>
    private static void CheckFinalRankingNotSinglePhaseWithMultiplePhases(ClassDefinition definition, List<Defect> defects)
    {
        if (definition.FinalRanking == FinalRankingKind.SinglePhase && definition.Phases.Length > 1)
        {
            defects.Add(new Defect("class-definition.check-11.single-phase-final-ranking-with-multiple-phases", "$.finalRanking",
                "finalRanking must not be SinglePhase on a class with more than one phase."));
        }
    }

    /// <summary>Check 12 — a class with more than one phase and no finalRanking is rejected (notation §3, diagram §2).</summary>
    private static void CheckFinalRankingRequiredForMultiplePhases(ClassDefinition definition, List<Defect> defects)
    {
        if (definition.Phases.Length > 1 && definition.FinalRanking is null)
        {
            defects.Add(new Defect("class-definition.check-12.missing-final-ranking", "$.finalRanking",
                "finalRanking is required on a class with more than one phase."));
        }
    }

    /// <summary>
    /// Check 13 — ReflightRule.minNewGroupSize populated while both selections
    /// are NotPermitted is rejected (notation §3, diagram §2). Applies to the
    /// class default and to every task-level override alike.
    /// </summary>
    private static void CheckReflightMinNewGroupSizeNotWithNoReflight(ClassDefinition definition, List<Defect> defects)
    {
        CheckOne(definition.Reflight, "$.reflight");

        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            if (task.Reflight is { } reflight)
            {
                CheckOne(reflight, $"{taskPath}.reflight");
            }
        }

        void CheckOne(ReflightRule rule, string path)
        {
            if (rule.MinNewGroupSize is not null
                && rule.EntitledScores == ReflightSelection.NotPermitted
                && rule.OthersScore == ReflightSelection.NotPermitted)
            {
                defects.Add(new Defect("class-definition.check-13.minnewgroupsize-with-no-reflight", $"{path}.minNewGroupSize",
                    "minNewGroupSize must not be set when both entitledScores and othersScore are NotPermitted."));
            }
        }
    }

    /// <summary>Check 14 — a task with a normalised term list and no Normalisation is rejected (F24, notation §5, diagram §3).</summary>
    private static void CheckNormalisedTermsRequireNormalisation(ClassDefinition definition, List<Defect> defects)
    {
        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            if (task.ScoreNormalised.Length > 0 && task.Normalise is null)
            {
                defects.Add(new Defect("class-definition.check-14.normalised-terms-without-normalisation", $"{taskPath}.scoreNormalised",
                    $"Task '{task.Code}' has normalised score terms but no normalise stage."));
            }
        }
    }

    /// <summary>Check 15 — a task with a Normalisation and no GroupConstraint is rejected (notation §5, diagram §3).</summary>
    private static void CheckNormalisationRequiresGroup(ClassDefinition definition, List<Defect> defects)
    {
        foreach (var (taskPath, _, task) in AllTasks(definition))
        {
            if (task.Normalise is not null && task.Group is null)
            {
                defects.Add(new Defect("class-definition.check-15.normalisation-without-group", $"{taskPath}.normalise",
                    $"Task '{task.Code}' normalises but declares no group constraint."));
            }
        }
    }

    /// <summary>
    /// Check 16 — each group named in a PenaltyDefinition.exclusionGroups
    /// contains only DeductPoints effects (F28, notation §3, diagram §2). The
    /// check is per group: a penalty with a non-DeductPoints effect is flagged
    /// once for each exclusion group it claims membership of.
    /// </summary>
    private static void CheckExclusionGroupsAreDeductOnly(ClassDefinition definition, List<Defect> defects)
    {
        for (var i = 0; i < definition.Penalties.Length; i++)
        {
            var penalty = definition.Penalties[i];
            if (penalty.Effects.All(e => e.Effect == PenaltyEffect.DeductPoints))
            {
                continue;
            }

            for (var g = 0; g < penalty.ExclusionGroups.Length; g++)
            {
                defects.Add(new Defect("class-definition.check-16.exclusion-group-non-deduct-effect",
                    $"$.penalties[{i}].exclusionGroups[{g}]",
                    $"Exclusion group '{penalty.ExclusionGroups[g]}' includes penalty '{penalty.InfractionType}', which has a non-DeductPoints effect."));
            }
        }
    }

    /// <summary>
    /// Check 20 — a PenaltyDefinition.permittedScopes that is present must not
    /// be empty (diagram §2): an infraction no scope permits can never be
    /// recorded, so the declaration is provably inert — the check-19 precedent.
    /// Absent (null) is the unrestricted default and needs nothing.
    /// kanban/completed/permitted-scopes-on-penalty-definitions.md#wi-2.
    /// </summary>
    private static void CheckPermittedScopesNotEmpty(ClassDefinition definition, List<Defect> defects)
    {
        for (var i = 0; i < definition.Penalties.Length; i++)
        {
            if (definition.Penalties[i].PermittedScopes is { Length: 0 })
            {
                defects.Add(new Defect("class-definition.check-20.permitted-scopes-empty",
                    $"$.penalties[{i}].permittedScopes",
                    $"Penalty '{definition.Penalties[i].InfractionType}' permits no scope, so it could never be recorded."));
            }
        }
    }

    /// <summary>
    /// Check 17 — every QualifyingPosition tie-break rung names an existing
    /// phase with a strictly lower ordinal (F3J.11.4): the figure is a
    /// previous phase's placing. SourcePhaseOrdinal is a phase's
    /// <see cref="PhaseDefinition.Ordinal"/> — the class definition's own
    /// 1-based ordinal vocabulary, the value the corpus states (F3J's and
    /// F5J's fly-offs write qualifyingPosition 1 for the preliminary) — so
    /// the source must resolve to a phase of THIS definition by Ordinal and
    /// sit strictly below the phase declaring the rung. Unwritable on phase 1
    /// — nothing has a strictly lower ordinal — which is what makes the figure
    /// unsupplyable in the single-phase world (story D9).
    /// </summary>
    private static void CheckQualifyingPositionSourceIsEarlierPhase(ClassDefinition definition, List<Defect> defects)
    {
        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var phase = definition.Phases[p];
            var tieBreaks = phase.TieBreaks;
            for (var i = 0; i < tieBreaks.Length; i++)
            {
                if (tieBreaks[i] is QualifyingPosition { SourcePhaseOrdinal: var source }
                    && (!definition.Phases.Any(ph => ph.Ordinal == source) || source >= phase.Ordinal))
                {
                    defects.Add(new Defect("class-definition.check-17.qualifying-position-source-not-earlier",
                        $"$.phases[{p}].tieBreaks[{i}]",
                        $"SourcePhaseOrdinal {source} must name an existing phase with a strictly lower ordinal than the phase declaring the rung."));
                }
            }
        }
    }

    /// <summary>
    /// Check 18 — a phase's TieBreaks containing UndefinedRequiresRuling
    /// contains only it: mixing "the rulebook is silent" with stated rungs is
    /// a self-contradiction (the re-flight block's NotPermitted/Undefined
    /// distinction applied to lists).
    /// </summary>
    private static void CheckUndefinedRequiresRulingStandsAlone(ClassDefinition definition, List<Defect> defects)
    {
        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var tieBreaks = definition.Phases[p].TieBreaks;
            if (tieBreaks.Length > 1 && tieBreaks.Any(t => t is UndefinedRequiresRuling))
            {
                defects.Add(new Defect("class-definition.check-18.undefined-requires-ruling-mixed-with-stated",
                    $"$.phases[{p}].tieBreaks",
                    "A tie-break ladder containing undefinedRequiresRuling must contain only it."));
            }
        }
    }

    /// <summary>
    /// Check 20 — a phase's TieBreaks containing EqualPlaces contains only
    /// it: "ties are never broken" stated beside any rung that could
    /// separate is a self-contradiction (the check-18 shape; Pete's
    /// 2026-09-04 NZ ruling, which introduced EqualPlaces).
    /// </summary>
    private static void CheckEqualPlacesStandsAlone(ClassDefinition definition, List<Defect> defects)
    {
        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var tieBreaks = definition.Phases[p].TieBreaks;
            if (tieBreaks.Length > 1 && tieBreaks.Any(t => t is EqualPlaces))
            {
                defects.Add(new Defect("class-definition.check-20.equal-places-mixed-with-stated",
                    $"$.phases[{p}].tieBreaks",
                    "A tie-break ladder containing equalPlaces must contain only it."));
            }
        }
    }

    /// <summary>
    /// Check 19 — BestDroppedScore on a phase that declares no DropPolicy is
    /// rejected (the check-13 precedent): no drop policy means no dropped cell
    /// ever exists, so the rung is provably inert.
    /// </summary>
    private static void CheckBestDroppedScoreRequiresDropPolicy(ClassDefinition definition, List<Defect> defects)
    {
        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var phase = definition.Phases[p];
            if (phase.Drops.Length > 0)
            {
                continue;
            }

            for (var i = 0; i < phase.TieBreaks.Length; i++)
            {
                if (phase.TieBreaks[i] is BestDroppedScore)
                {
                    defects.Add(new Defect("class-definition.check-19.best-dropped-score-without-drop-policy",
                        $"$.phases[{p}].tieBreaks[{i}]",
                        "bestDroppedScore requires the phase to declare at least one drop policy."));
                }
            }
        }
    }

    // -------------------------------------------------------------- helpers

    private static void CheckNumberRef(NumberOrParam? value, string path, HashSet<string> declared, List<Defect> defects)
    {
        if (value is NumberOrParam.Ref r && !declared.Contains(r.ParameterName))
        {
            defects.Add(new Defect("class-definition.check-3.unresolved-parameter-ref", path,
                $"Parameter '{r.ParameterName}' is not declared."));
        }
    }

    private static void CheckFlagRef(FlagOrParam? value, string path, HashSet<string> declared, List<Defect> defects)
    {
        if (value is FlagOrParam.Ref r && !declared.Contains(r.ParameterName))
        {
            defects.Add(new Defect("class-definition.check-3.unresolved-parameter-ref", path,
                $"Parameter '{r.ParameterName}' is not declared."));
        }
    }

    private static void CheckUnit(NumberOrParam? value, string expectedUnit, string path, IReadOnlyDictionary<string, Parameter> declared, List<Defect> defects)
    {
        if (value is NumberOrParam.Ref r && declared.TryGetValue(r.ParameterName, out var parameter) && parameter.Unit != expectedUnit)
        {
            defects.Add(new Defect("class-definition.check-7.parameter-unit-mismatch", path,
                $"Parameter '{r.ParameterName}' has unit '{parameter.Unit ?? "(none)"}', but this slot requires '{expectedUnit}'."));
        }
    }

    private static IEnumerable<(string Path, PhaseDefinition Phase, TaskDefinition Task)> AllTasks(ClassDefinition definition)
    {
        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var phase = definition.Phases[p];
            for (var t = 0; t < phase.Tasks.Length; t++)
            {
                yield return ($"$.phases[{p}].tasks[{t}]", phase, phase.Tasks[t]);
            }
        }
    }

    private static IEnumerable<(string Path, ScoreTerm Term)> AllTermsOf(string taskPath, TaskDefinition task) =>
        WalkTerms($"{taskPath}.score", task.Score).Concat(WalkTerms($"{taskPath}.scoreNormalised", task.ScoreNormalised));

    private static IEnumerable<(string Path, ScoreTerm Term)> WalkTerms(string basePath, ImmutableArray<ScoreTerm> terms)
    {
        for (var i = 0; i < terms.Length; i++)
        {
            foreach (var item in WalkTerm($"{basePath}[{i}]", terms[i]))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<(string Path, ScoreTerm Term)> WalkTerm(string path, ScoreTerm term)
    {
        yield return (path, term);

        if (term is ConditionalTerm conditional)
        {
            foreach (var item in WalkTerm($"{path}.then", conditional.Then))
            {
                yield return item;
            }

            if (conditional.Else is not null)
            {
                foreach (var item in WalkTerm($"{path}.else", conditional.Else))
                {
                    yield return item;
                }
            }
        }
    }

    private static IEnumerable<(string Path, string MetricRef)> OwnMetricRef(string path, ScoreTerm term)
    {
        switch (term)
        {
            case RateTerm rate:
                yield return ($"{path}.metricRef", rate.MetricRef);
                break;
            case LookupTerm lookup:
                yield return ($"{path}.metricRef", lookup.MetricRef);
                break;
            case PiecewiseTerm piecewise:
                yield return ($"{path}.metricRef", piecewise.MetricRef);
                break;
        }
    }

    private static IEnumerable<(string Path, Predicate Predicate)> AllPredicates(string taskPath, TaskDefinition task)
    {
        if (task.ValidWhen is not null)
        {
            foreach (var item in WalkPredicate($"{taskPath}.validWhen", task.ValidWhen))
            {
                yield return item;
            }
        }

        if (task.FlightValidWhen is not null)
        {
            foreach (var item in WalkPredicate($"{taskPath}.flightValidWhen", task.FlightValidWhen))
            {
                yield return item;
            }
        }

        foreach (var (termPath, term) in AllTermsOf(taskPath, task))
        {
            if (term is ConditionalTerm conditional)
            {
                foreach (var item in WalkPredicate($"{termPath}.when", conditional.When))
                {
                    yield return item;
                }
            }
        }
    }

    private static IEnumerable<(string Path, Predicate Predicate)> WalkPredicate(string path, Predicate predicate)
    {
        yield return (path, predicate);

        if (predicate is AllOf allOf)
        {
            for (var i = 0; i < allOf.Children.Length; i++)
            {
                foreach (var item in WalkPredicate($"{path}.children[{i}]", allOf.Children[i]))
                {
                    yield return item;
                }
            }
        }
    }
}
