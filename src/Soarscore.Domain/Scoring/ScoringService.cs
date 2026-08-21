// ScoringService — kanban/completed/scoring-service-plan.md WI-9, superseded by
// kanban/completed/scoring-steel-thread-plan.md (WI-2..WI-4).
//
// Domain service that orchestrates the scoring pipeline. It reads adopted rules
// and structure from the Competition aggregate and raw data from the Entry
// aggregate, resolves parameters and amendments, routes penalties, and wires
// all pipeline stages together.
//
// Granular methods let callers compute only what they need. ScoreGroup and
// ScoreCompetition run the full pipeline. Static, not instantiable (finding 6
// of the steel-thread plan) — it holds no state, matching every other stage
// in this namespace.

using System.Collections.Immutable;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// The scoring domain service. Wires pipeline stages together, resolves
/// parameters and amendments, routes penalties, and orchestrates the
/// competition-wide walk.
/// </summary>
public static class ScoringService
{
    // ---------------------------------------------------- granular methods

    /// <summary>Interpret one flight through flightValidWhen and raw score terms.</summary>
    public static InterpretedFlight InterpretFlight(
        ResolvedTask task,
        int flightSequence,
        IReadOnlyDictionary<string, MeasuredValue> resolvedMetrics) =>
        FlightInterpreter.Interpret(task, flightSequence, resolvedMetrics);

    /// <summary>Select flights from an Entry, apply caps, round.</summary>
    public static TaskResult SelectFlights(
        Entry? entry,
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableArray<InterpretedFlight> interpretedFlights) =>
        FlightSelector.SelectAndScore(entry, task, parameterBindings, interpretedFlights);

    /// <summary>Normalise a group's task results.</summary>
    public static GroupResult NormaliseGroup(
        string groupRef,
        ImmutableDictionary<string, TaskResult> results,
        ResolvedTask task,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings) =>
        NormalisationEngine.Normalise(groupRef, results, task, parameterBindings);

    /// <summary>Aggregate phase scores for one competitor, applying drops.</summary>
    public static PhaseScores Aggregate(
        string competitorRef,
        PhaseDefinition phase,
        ImmutableArray<RoundData> rounds,
        IReadOnlyDictionary<string, TaskRoundScore> allScores) =>
        PhaseAggregator.Aggregate(competitorRef, phase, rounds, allScores);

    /// <summary>Rank competitors by final scores.</summary>
    public static CompetitionResult Rank(
        ImmutableArray<FinalCompetitorScore> scores,
        FinalRankingKind? finalRanking,
        PromotionRule? promotion) =>
        RankingEngine.Rank(scores, finalRanking, promotion);

    // ---------------------------------------------------- convenience methods

    /// <summary>
    /// Full pipeline for one task-round group: interpret flights, select,
    /// apply raw penalties, normalise.
    /// </summary>
    /// <param name="groupRef">Group identifier (stringified GroupId — finding 3).</param>
    /// <param name="task">The task definition (unresolved — parameters are resolved here).</param>
    /// <param name="classDef">The class definition (for penalty definitions).</param>
    /// <param name="entries">CompetitorRef (stringified CompetitorId) → Entry.</param>
    /// <param name="parameterBindings">Bound parameter values (from Competition.ParameterBindings).</param>
    public static GroupResult ScoreGroup(
        string groupRef,
        TaskDefinition task,
        ClassDefinition classDef,
        ImmutableDictionary<string, Entry> entries,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings)
    {
        // 1. Resolve task parameters.
        var resolvedTask = ParameterResolver.ResolveTask(task, parameterBindings, classDef.Parameters);

        // 2. For each Entry: interpret flights, select, apply raw penalties.
        var taskResults = ImmutableDictionary.CreateBuilder<string, TaskResult>();

        foreach (var (competitorRef, entry) in entries)
        {
            // 2a. Resolve amendments for each flight → ResolvedMeasurements
            //     (issue #4: amendment resolution lives in the orchestrator).
            var interpretedFlights = InterpretAllFlights(entry, resolvedTask);

            // 2b. Select flights and assemble raw score. Annulment is checked
            //     inside FlightSelector.SelectAndScore.
            var taskResult = FlightSelector.SelectAndScore(
                entry, resolvedTask, parameterBindings, interpretedFlights);

            // 2c. Apply raw penalties scoped to this Entry (Flight/Entry scope).
            var entryPenalties = GetEntryPenalties(entry);
            taskResult = PenaltyEngine.ApplyRawPenalties(
                taskResult, entryPenalties, classDef.Penalties);

            taskResults[competitorRef] = taskResult;
        }

        // 3. Normalise the group.
        return NormalisationEngine.Normalise(
            groupRef, taskResults.ToImmutable(), resolvedTask, parameterBindings);
    }

    /// <summary>
    /// Full competition result: walk every phase/round/task-round/group, score
    /// each group, aggregate per competitor with drops applied, apply aggregate
    /// penalties, and rank.
    /// </summary>
    /// <param name="competition">The competition, with its adopted rules and drawn structure.</param>
    /// <param name="entries">Every Entry in the competition, keyed by EntryId (EntryCollector's job to assemble).</param>
    public static Result<CompetitionResult> ScoreCompetition(
        Competition competition,
        IReadOnlyDictionary<EntryId, Entry> entries)
    {
        var classDef = competition.AdoptedRules.Definition;

        // competitorRef (string) → running total across every phase they score in.
        // Only phase 0 is ever drawn today (Competition.cs's DrawPhase), so this
        // sum is exactly that phase's aggregate — see the plan's Out of scope
        // note on LastPhaseReplaces/SplitByPromotion.
        var totalsByCompetitor = new Dictionary<string, decimal>();

        foreach (var phase in competition.Phases)
        {
            // Positional index into the class's ordered phase list, not a
            // PhaseDefinition.Ordinal lookup — the same convention
            // Competition.DrawPhase uses to mint Phase.Ordinal in the first place.
            var phaseDefinition = classDef.Phases[phase.Ordinal];

            var roundData = ImmutableArray.CreateBuilder<RoundData>();
            var scoresByCompetitor = new Dictionary<string, List<TaskRoundScore>>();

            foreach (var round in phase.Rounds)
            {
                // Round-scoped, not task-round-scoped: ParameterBinding.RoundOrdinal
                // names Round.Ordinal (kanban/completed/per-round-parameter-bindings-plan.md).
                var bindings = FlattenParameterBindings(competition.ParameterBindings, phase.Ordinal, round.Ordinal);

                var taskRoundData = ImmutableArray.CreateBuilder<TaskRoundData>();

                foreach (var taskRound in round.TaskRounds)
                {
                    var taskRoundEntries = entries.Values
                        .Where(e => e.PhaseOrdinal == phase.Ordinal
                                 && e.RoundOrdinal == round.Ordinal
                                 && e.TaskRoundOrdinal == taskRound.Ordinal)
                        .ToList();

                    // Finding 5: no Entry anywhere in this task-round → it has
                    // not been flown yet. Omit it entirely rather than lying
                    // that it is Complete (which would hand a drop-worst policy
                    // a zero to spend on a round nobody has flown).
                    if (taskRoundEntries.Count == 0)
                        continue;

                    var reflightOffender = taskRoundEntries
                        .GroupBy(e => e.CompetitorRef)
                        .FirstOrDefault(g => g.Count(e => e.Annulment is null) > 1);

                    if (reflightOffender is not null)
                    {
                        return Result<CompetitionResult>.Failure(
                            "score.reflightNotSupported",
                            $"Competitor {reflightOffender.Key} has more than one non-annulled Entry for "
                            + $"phase {phase.Ordinal}/round {round.Ordinal}/task-round {taskRound.Ordinal}. "
                            + "Reflight scoring (entitled/filler selection) is not yet supported.");
                    }

                    var taskDefinition = classDef.Phases
                        .SelectMany(p => p.Tasks)
                        .FirstOrDefault(t => t.Code == taskRound.TaskRef);

                    if (taskDefinition is null)
                    {
                        return Result<CompetitionResult>.Failure(
                            "score.taskNotDeclared",
                            $"Task-round references task '{taskRound.TaskRef}', which is not declared "
                            + "by the adopted class definition.");
                    }

                    foreach (var group in taskRound.Groups)
                    {
                        // Annulled entries are excluded from group scoring: they
                        // produce NoResult (FlightSelector step 0) and, more
                        // importantly, an annulled attempt alongside its live
                        // replacement is the F3F.1.5 shape — two Entries for one
                        // competitor+task-round, which would collide as duplicate
                        // dictionary keys. The replacement is the one that scores.
                        var groupEntries = taskRoundEntries
                            .Where(e => e.GroupRef == group.Id && e.Annulment is null)
                            .ToImmutableDictionary(e => e.CompetitorRef.ToString(), e => e);

                        // A competitor drawn into a group with no Entry
                        // contributes no TaskRoundScore — absent, not zero.
                        if (groupEntries.IsEmpty)
                            continue;

                        var groupResult = ScoreGroup(
                            group.Id.ToString(), taskDefinition, classDef, groupEntries, bindings);

                        foreach (var (competitorRef, taskResult) in groupResult.Results)
                        {
                            if (!scoresByCompetitor.TryGetValue(competitorRef, out var list))
                            {
                                list = [];
                                scoresByCompetitor[competitorRef] = list;
                            }

                            list.Add(new TaskRoundScore(
                                taskRound.TaskRef, round.Ordinal, taskRound.Ordinal, taskResult.RawScore));
                        }
                    }

                    // Total over the write-side states, with no `else`: the
                    // old one existed only because nothing could emit
                    // TaskRoundCompleted, so every state that was not
                    // Annulled collapsed to Complete by default
                    // (kanban/completed/task-round-lifecycle.md WI-4).
                    //
                    // Drawn/InProgress still score as Complete, but now as a
                    // deliberate choice rather than an artefact: this is the
                    // provisional leaderboard, and a task-round with entries
                    // present is scored on what has been captured so far.
                    // Entries-absent task-rounds never reach here at all —
                    // the finding-5 filter above skipped them.
                    var state = taskRound.State switch
                    {
                        Competitions.TaskRoundState.Annulled => TaskRoundState.Annulled,
                        Competitions.TaskRoundState.Complete => TaskRoundState.Complete,
                        Competitions.TaskRoundState.Drawn or Competitions.TaskRoundState.InProgress => TaskRoundState.Complete,
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(competition), taskRound.State, "Unknown TaskRoundState."),
                    };

                    taskRoundData.Add(new TaskRoundData(taskRound.Ordinal, taskRound.TaskRef, state));
                }

                if (taskRoundData.Count > 0)
                    roundData.Add(new RoundData(round.Ordinal, taskRoundData.ToImmutable()));
            }

            var rounds = roundData.ToImmutable();

            foreach (var (competitorRef, scores) in scoresByCompetitor)
            {
                var allScores = scores
                    .Select((score, index) => (score, index))
                    .ToDictionary(x => $"{x.score.RoundOrdinal}|{x.score.TaskOrdinal}|{x.index}", x => x.score);

                var phaseScores = Aggregate(competitorRef, phaseDefinition, rounds, allScores);

                totalsByCompetitor[competitorRef] =
                    totalsByCompetitor.GetValueOrDefault(competitorRef) + phaseScores.Aggregate;
            }
        }

        var finalScores = ImmutableArray.CreateBuilder<FinalCompetitorScore>(totalsByCompetitor.Count);

        foreach (var (competitorRef, totalScore) in totalsByCompetitor)
        {
            // Subject-filtered: a Competition/TaskRound-scoped penalty names the
            // competitor it is against (Penalty.CompetitorRef), so each
            // competitor is deducted only their own penalties — never the whole
            // field's. See kanban/in-progress/annul-and-penalise-the-second-entry-thread.md
            // finding 2: before the subject field existed, every aggregate
            // penalty hit every competitor.
            var aggregatePenalties = GetAggregatePenalties(competition.Penalties, competitorRef);
            var penaltyResult = PenaltyEngine.ApplyAggregatePenalties(totalScore, aggregatePenalties, classDef.Penalties);

            finalScores.Add(new FinalCompetitorScore(
                CompetitorRef: competitorRef,
                Score: totalScore - penaltyResult.Deduction,
                Disqualified: penaltyResult.Disqualified));
        }

        // PromotionRule "appears only on a phase after the first"
        // (PhaseDefinition's doc comment) — the same phases[1] read
        // ScoreCompetition always used, kept for when a second phase exists.
        var promotion = classDef.Phases.Length > 1 ? classDef.Phases[1].Promotion : null;

        return Result<CompetitionResult>.Success(
            Rank(finalScores.ToImmutable(), classDef.FinalRanking, promotion));
    }

    // ---------------------------------------------------- parameter bindings

    /// <summary>
    /// Flattens Competition.ParameterBindings to one value per parameter name,
    /// the one place every caller (Competition.DrawPhase, Competition.OpenEntry,
    /// Application.Entries.TaskResolver, Application.Queries.Scoring.ScoreTaskRound,
    /// ScoreCompetition below) does it. Resolution order — Appendix A of
    /// kanban/completed/catalogue-choice-draws-plan.md, discharged by
    /// kanban/completed/per-round-parameter-bindings-plan.md: for the queried
    /// (phaseOrdinal, roundOrdinal), the last binding scoped to exactly that
    /// round wins; failing that, the last unscoped binding; failing that, the
    /// parameter is simply absent from the result (ParameterResolver falls back
    /// to the declared default, or throws). Omitting both arguments — every
    /// caller that cannot yet know a round, i.e. DrawPhase building rounds that
    /// do not exist yet — degrades to "unscoped bindings only", exactly today's
    /// behaviour, since a round-scoped binding never matches a null round.
    /// </summary>
    public static IReadOnlyDictionary<string, MeasuredValue> FlattenParameterBindings(
        ImmutableArray<ParameterBinding> bindings, int? phaseOrdinal = null, int? roundOrdinal = null)
    {
        var result = new Dictionary<string, MeasuredValue>();

        foreach (var group in bindings.GroupBy(b => b.ParameterName))
        {
            var roundScoped = group
                .Where(b => b.RoundOrdinal is not null && b.PhaseOrdinal == phaseOrdinal && b.RoundOrdinal == roundOrdinal)
                .ToImmutableArray();

            var chosen = roundScoped.IsEmpty
                ? group.Where(b => b.RoundOrdinal is null).LastOrDefault()
                : roundScoped[^1];

            if (chosen is not null)
            {
                result[group.Key] = chosen.BoundValue;
            }
        }

        return result;
    }

    // ---------------------------------------------------- amendment resolution

    /// <summary>
    /// Resolve the effective measurements for every Flight in an Entry, in
    /// sequence order, and interpret each through the task's raw score terms.
    /// </summary>
    private static ImmutableArray<InterpretedFlight> InterpretAllFlights(Entry entry, ResolvedTask task)
    {
        var builder = ImmutableArray.CreateBuilder<InterpretedFlight>(entry.Flights.Length);

        foreach (var flight in entry.Flights)
        {
            var resolved = MeasurementDigest.Resolve(flight);
            builder.Add(FlightInterpreter.Interpret(task, flight.Sequence, resolved.Metrics));
        }

        return builder.ToImmutable();
    }

    // ---------------------------------------------------- penalty routing

    /// <summary>
    /// Extract Flight/Entry-scoped penalties from an Entry, grouped by
    /// infraction type and counted — one recorded Penalty is one occurrence
    /// (finding 4).
    /// </summary>
    private static ImmutableArray<RecordedPenalty> GetEntryPenalties(Entry entry) =>
        entry.Penalties
            .Where(p => p.Scope is PenaltyScope.Flight or PenaltyScope.Entry)
            .GroupBy(p => p.InfractionType)
            .Select(g => new RecordedPenalty(g.Key, g.Count()))
            .ToImmutableArray();

    /// <summary>
    /// Extract TaskRound/Competition-scoped penalties from the Competition
    /// aggregate, grouped by infraction type and counted — filtered to the one
    /// <paramref name="competitorRef"/> that is their subject. Since the Penalty
    /// payload gained a <c>CompetitorRef</c>
    /// (kanban/in-progress/annul-and-penalise-the-second-entry-thread.md), an
    /// aggregate penalty applies to its subject alone, never uniformly to the
    /// whole field.
    /// </summary>
    private static ImmutableArray<RecordedPenalty> GetAggregatePenalties(
        ImmutableArray<Penalty> competitionPenalties, string competitorRef) =>
        competitionPenalties
            .Where(p => p.Scope is PenaltyScope.TaskRound or PenaltyScope.Competition)
            .Where(p => p.CompetitorRef is { } subject && subject.ToString() == competitorRef)
            .GroupBy(p => p.InfractionType)
            .Select(g => new RecordedPenalty(g.Key, g.Count()))
            .ToImmutableArray();
}
