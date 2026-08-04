// ScoringService — docs/plans/scoring-service-plan.md WI-9.
//
// Domain service that orchestrates the scoring pipeline. It reads adopted rules
// and structure from the Competition aggregate and raw data from the Entry
// aggregate, resolves parameters and amendments, routes penalties, handles
// reflights, and wires all pipeline stages together.
//
// Granular methods let callers compute only what they need. The convenience
// methods ScoreGroup and ScoreCompetition run the full pipeline.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// The scoring domain service. Wires pipeline stages together, resolves
/// parameters and amendments, routes penalties, and handles reflights.
/// </summary>
public class ScoringService
{
    // ---------------------------------------------------- granular methods

    /// <summary>Interpret one flight through flightValidWhen and raw score terms.</summary>
    public InterpretedFlight InterpretFlight(
        object flight,              // Flight aggregate (TBD)
        ResolvedTask task,
        int flightSequence,
        IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics)
    {
        return FlightInterpreter.Interpret(flight, task, flightSequence, resolvedMetrics);
    }

    /// <summary>Select flights from an Entry, apply caps, round.</summary>
    public TaskResult SelectFlights(
        object entry,               // Entry aggregate (TBD)
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<InterpretedFlight> interpretedFlights)
    {
        return FlightSelector.SelectAndScore(entry, task, parameterBindings, interpretedFlights);
    }

    /// <summary>Normalise a group's task results.</summary>
    public GroupResult NormaliseGroup(
        string groupRef,
        ImmutableDictionary<string, TaskResult> results,
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings)
    {
        return NormalisationEngine.Normalise(groupRef, results, task, parameterBindings);
    }

    /// <summary>Aggregate phase scores for one competitor, applying drops.</summary>
    public PhaseScores Aggregate(
        string competitorRef,
        PhaseDefinition phase,
        ImmutableArray<RoundData> rounds,
        IReadOnlyDictionary<string, TaskRoundScore> allScores)
    {
        return PhaseAggregator.Aggregate(competitorRef, phase, rounds, allScores);
    }

    /// <summary>Rank competitors by final scores.</summary>
    public CompetitionResult Rank(
        ImmutableArray<FinalCompetitorScore> scores,
        FinalRankingKind? finalRanking,
        PromotionRule? promotion)
    {
        return RankingEngine.Rank(scores, finalRanking, promotion);
    }

    // ---------------------------------------------------- convenience methods

    /// <summary>
    /// Full pipeline for one task-round group: interpret flights, select,
    /// apply raw penalties, normalise.
    /// </summary>
    /// <param name="groupRef">Group identifier.</param>
    /// <param name="task">The task definition (unresolved — parameters are resolved here).</param>
    /// <param name="phase">The phase (for context).</param>
    /// <param name="classDef">The class definition (for penalty definitions).</param>
    /// <param name="entries">CompetitorRef → Entry. Each Entry has Flights with Measurements.</param>
    /// <param name="parameterBindings">Bound parameter values (from Competition.ParameterBindings).</param>
    /// <param name="competitionPenalties">Penalties recorded at the competition level.</param>
    public GroupResult ScoreGroup(
        string groupRef,
        TaskDefinition task,
        PhaseDefinition phase,
        ClassDefinition classDef,
        ImmutableDictionary<string, object> entries,  // CompetitorRef → Entry (TBD)
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<RecordedPenalty> competitionPenalties)
    {
        // 1. Resolve task parameters.
        var resolvedTask = ParameterResolver.ResolveTask(task, parameterBindings);

        // 2. For each Entry: interpret flights, select, apply raw penalties.
        var taskResults = ImmutableDictionary.CreateBuilder<string, TaskResult>();

        foreach (var (competitorRef, entry) in entries)
        {
            // 2a. Resolve amendments for each flight → ResolvedMeasurements.
            //     (Issue #4: amendment resolution lives in the orchestrator.)
            var interpretedFlights = InterpretAllFlights(entry, resolvedTask);

            // 2b. Select flights and assemble raw score.
            var taskResult = FlightSelector.SelectAndScore(
                entry, resolvedTask, parameterBindings, interpretedFlights);

            // 2c. Apply raw penalties scoped to this Entry.
            var entryPenalties = GetEntryPenalties(entry, competitionPenalties);
            taskResult = PenaltyEngine.ApplyRawPenalties(
                taskResult, entryPenalties, classDef.Penalties);

            taskResults[competitorRef] = taskResult;
        }

        // 3. Normalise the group.
        return NormalisationEngine.Normalise(
            groupRef, taskResults.ToImmutable(), resolvedTask, parameterBindings);
    }

    /// <summary>
    /// Full competition result: score all groups, aggregate phases, apply
    /// aggregate penalties, rank.
    /// </summary>
    /// <param name="classDef">The class definition (rules, penalties, phases).</param>
    /// <param name="phases">The phases to score (from Competition.AdoptedRules).</param>
    /// <param name="entriesByCompetitor">CompetitorRef → ordered Entries by round/task.</param>
    /// <param name="parameterBindings">Bound parameter values.</param>
    /// <param name="allPenalties">All recorded penalties (all scopes).</param>
    public CompetitionResult ScoreCompetition(
        ClassDefinition classDef,
        ImmutableArray<PhaseDefinition> phases,
        ImmutableDictionary<string, ImmutableArray<object>> entriesByCompetitor,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<RecordedPenalty> allPenalties)
    {
        // --- Phase 1: Score every task-round group ---
        // This produces per-phase, per-round, per-task, per-group results.
        // The actual grouping structure comes from the Competition aggregate.
        // For now, we define the output shape.

        // Collect all TaskRoundScores: CompetitorRef → list of scores.
        var allTaskRoundScores = new Dictionary<string, List<TaskRoundScore>>();

        // (The full scoring loop would iterate phases → rounds → task-rounds → groups.
        //  Since we don't have the aggregate types, this is structural.)

        // --- Phase 2: Aggregate per competitor ---
        var finalScores = ImmutableArray.CreateBuilder<FinalCompetitorScore>();

        foreach (var (competitorRef, _) in entriesByCompetitor)
        {
            // Aggregate scores across phases.
            // If multi-phase with LastPhaseReplaces, the fly-off phase replaces
            // the preliminary phase scores for promoted competitors.
            // SplitByPromotion splits the ranking into two lists.

            // For now: sum all task-round scores (no phase aggregation yet).
            var competitorScores = allTaskRoundScores.TryGetValue(competitorRef, out var list)
                ? list
                : new List<TaskRoundScore>();

            decimal totalScore = competitorScores.Sum(s => s.Score);

            // Apply aggregate penalties.
            // Separate penalties by scope:
            //   - Competition-level penalties → applied here.
            //   - TaskRound-level penalties → applied here (after drops).
            var aggregatePenalties = GetAggregatePenalties(competitorRef, allPenalties);
            var penaltyResult = PenaltyEngine.ApplyAggregatePenalties(
                totalScore, aggregatePenalties, classDef.Penalties);

            finalScores.Add(new FinalCompetitorScore(
                CompetitorRef: competitorRef,
                Score: totalScore - penaltyResult.Deduction,
                Disqualified: penaltyResult.Disqualified
            ));
        }

        // --- Phase 3: Handle finalRanking logic ---
        // LastPhaseReplaces: the orchestrator pre-computes scores —
        // promoted competitors use fly-off scores, others use preliminary.
        // SplitByPromotion: two separate ranking lists.

        // --- Phase 4: Rank ---
        return RankingEngine.Rank(
            finalScores.ToImmutable(),
            classDef.FinalRanking,
            phases.Length > 1 ? phases[1].Promotion : null
        );
    }

    // ---------------------------------------------------- amendment resolution

    /// <summary>
    /// Resolve the effective measurements for all flights in an Entry.
    /// The effective value of each Measurement is the most recent Amendment's
    /// NewValue, or the original Measurement.Value if no amendments exist.
    /// (Issue #4: this lives in the orchestrator, not in pipeline stages.)
    /// </summary>
    private ImmutableArray<InterpretedFlight> InterpretAllFlights(
        object entry,       // Entry aggregate (TBD)
        ResolvedTask task)
    {
        // Placeholder: when the Entry/Flight/Measurement/Amendment types exist,
        // this method:
        // 1. Iterates over entry.Flights in sequence order.
        // 2. For each Flight, resolves amendments:
        //      For each Measurement:
        //        effective = most recent Amendment.NewValue ?? Measurement.Value
        // 3. Builds a ResolvedMeasurements dictionary.
        // 4. Calls FlightInterpreter.Interpret for each flight.
        // 5. Returns the InterpretedFlight array.

        // Until aggregates exist, return empty.
        return ImmutableArray<InterpretedFlight>.Empty;
    }

    // ---------------------------------------------------- penalty routing

    /// <summary>
    /// Extract penalties scoped to a specific Entry from the full penalty list.
    /// Penalty scope (Flight/Entry/TaskRound/Competition) is determined by the
    /// Penalty entity on the aggregate — the orchestrator reads scope and routes
    /// accordingly (design rule #6: stage is derived from effect, not configured).
    /// </summary>
    private static ImmutableArray<RecordedPenalty> GetEntryPenalties(
        object entry,
        ImmutableArray<RecordedPenalty> allPenalties)
    {
        // Placeholder: when the Penalty entity exists with scope information,
        // filter to penalties scoped to this Entry.
        return ImmutableArray<RecordedPenalty>.Empty;
    }

    /// <summary>
    /// Extract penalties scoped to the Competition aggregate level.
    /// </summary>
    private static ImmutableArray<RecordedPenalty> GetAggregatePenalties(
        string competitorRef,
        ImmutableArray<RecordedPenalty> allPenalties)
    {
        // Placeholder: filter to Competition-level penalties.
        return ImmutableArray<RecordedPenalty>.Empty;
    }
}
