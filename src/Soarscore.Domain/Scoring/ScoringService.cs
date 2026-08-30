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
    /// <param name="entries">
    /// The entries to score, keyed caller-chosen (reflight-groups.md WI-6b):
    /// the ordinary key is CompetitorRef (stringified), but under reflights the
    /// caller keys BY ENTRY (ReflightSelector.EntryKey) so one competitor's
    /// two live entries both normalise (decision 3). The dictionary key simply
    /// names the result row.
    /// </param>
    /// <param name="parameterBindings">Bound parameter values (from Competition.ParameterBindings).</param>
    /// <param name="taskRoundPenalties">
    /// Aggregate-scoped Zero* penalties routed to this task-round, keyed by
    /// competitor (stringified CompetitorRef) — from
    /// <see cref="GetTaskRoundZeroPenalties"/>. Optional: null/empty is exactly
    /// the pre-WI-1 behaviour. Merged into the entry's own penalties at step 2c
    /// (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-1,
    /// D-A1: an aggregate-scoped Zero* record zeroes the named task-round via
    /// the same raw-stage engine path an entry-scoped one takes, not a third
    /// apply function). The map is keyed by COMPETITOR, so lookup is by the
    /// entry's subject — <c>entry.CompetitorRef</c>, never the dictionary key,
    /// which under reflights is the entry key (ReflightSelector.EntryKey).
    /// </param>
    public static GroupResult ScoreGroup(
        string groupRef,
        TaskDefinition task,
        ClassDefinition classDef,
        ImmutableDictionary<string, Entry> entries,
        IReadOnlyDictionary<string, MeasuredValue> parameterBindings,
        ImmutableDictionary<string, ImmutableArray<RecordedPenalty>>? taskRoundPenalties = null)
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

            // 2c. Apply raw penalties scoped to this Entry (Flight/Entry scope),
            //     plus any aggregate-scoped Zero* records routed to this
            //     task-round (WI-1/D-A1 — see the taskRoundPenalties doc). The
            //     Zero* dominance early-out in ApplyRawPenalties returns before
            //     its deduction loop, so a mixed-effect definition's
            //     DeductPoints half is never applied at this stage — the
            //     deduction still acts at the aggregate stage via
            //     GetAggregatePenalties/ApplyAggregatePenalties, which are
            //     unchanged: each half of the record acts once, in its own
            //     stage (D-A4, no double-count by construction).
            var entryPenalties = GetEntryPenalties(entry);

            // Subject-keyed merge: the map is per competitor, the entries dict
            // may be keyed by entry under reflights (see the param doc).
            var routed = taskRoundPenalties is not null
                && taskRoundPenalties.TryGetValue(entry.CompetitorRef.ToString(), out var zeros)
                    ? zeros
                    : ImmutableArray<RecordedPenalty>.Empty;

            taskResult = PenaltyEngine.ApplyRawPenalties(
                taskResult, entryPenalties.AddRange(routed), classDef.Penalties);

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
        var preDropTotals = new Dictionary<string, decimal>();

        foreach (var phase in competition.Phases)
        {
            // Positional index into the class's ordered phase list, not a
            // PhaseDefinition.Ordinal lookup — the same convention
            // Competition.DrawPhase uses to mint Phase.Ordinal in the first place.
            var phaseDefinition = classDef.Phases[phase.Ordinal];

            var roundData = ImmutableArray.CreateBuilder<RoundData>();
            var scoresByCompetitor = new Dictionary<string, List<TaskRoundScore>>();

            // D7 bookkeeping for this phase (reflight-aggregate-destination.md
            // WI-1): the walk's slot universe — every task-round the finding-5
            // filter let through, keyed (round, task ordinal, task code), which
            // is exactly the keying PhaseAggregator matches cells by — and
            // every emitted cell with the task-round that hosted it, so the
            // destination checks below can refuse loudly rather than let an
            // unmatched cell vanish inside PhaseAggregator's Aggregate.
            var walkedSlots = new HashSet<(int RoundOrdinal, int TaskOrdinal, string TaskCode)>();
            var emittedCells = new List<EmittedCell>();

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

                    // The reflight shape guard (reflight-groups.md WI-6b, replacing the old
                    // score.reflightNotSupported refusal; destination-aware per
                    // reflight-aggregate-destination.md WI-1): each competitor's
                    // LIVE entries must satisfy the destination-aware law — per
                    // (competitor, task-round, destination), one entry of any
                    // role, or exactly one Original plus exactly one
                    // reflight-role entry, with every explicit counts-for naming
                    // an earlier round of the phase. ReflightSelector owns the
                    // shape law; this walks the guard per competitor.
                    foreach (var competitorGroup in taskRoundEntries.GroupBy(e => e.CompetitorRef))
                    {
                        var live = competitorGroup
                            .Where(e => e.Annulment is null)
                            .Select(e => (e.Role, e.CountsForRoundOrdinal))
                            .ToList();

                        if (!ReflightSelector.ShapePermits(round.Ordinal, live))
                        {
                            return Result<CompetitionResult>.Failure(
                                "score.reflightShapeUnsupported",
                                $"Competitor {competitorGroup.Key} holds {live.Count} live entries for "
                                + $"phase {phase.Ordinal}/round {round.Ordinal}/task-round {taskRound.Ordinal} "
                                + $"(roles: {string.Join(", ", live.Select(e => e.Role))}; "
                                + "destinations: "
                                + $"{string.Join(", ", live.Select(e => e.CountsForRoundOrdinal ?? round.Ordinal))}). "
                                + "Expected, per destination: one entry of any role, or an Original "
                                + "paired with one reflight-role entry, with any explicit counts-for "
                                + "naming an earlier round of the phase.");
                        }
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

                    walkedSlots.Add((round.Ordinal, taskRound.Ordinal, taskRound.TaskRef));

                    // Route this task-round's share of aggregate-scoped Zero*
                    // penalties into the raw stage (WI-1, D-A1/D-A2). Only
                    // task-rounds the walk actually reaches anchor zeros —
                    // skipped (entries-absent) task-rounds never get here.
                    var coordinate = new TaskRoundCoordinate(phase.Ordinal, round.Ordinal, taskRound.Ordinal);

                    var taskRoundZeroPenalties = GetTaskRoundZeroPenalties(competition.Penalties, classDef, coordinate);
                    if (taskRoundZeroPenalties.IsFailure)
                    {
                        return Result<CompetitionResult>.Failure(
                            taskRoundZeroPenalties.Code!, taskRoundZeroPenalties.Message!, taskRoundZeroPenalties.Defects);
                    }

                    var reflightRule = taskDefinition.Reflight ?? classDef.Reflight;

                    // The CD rulings for this task-round, keyed by competitor
                    // (reflight-scoring-rulings.md WI-3b). GroupBy preserves
                    // within-group order and g.Last() is therefore the most
                    // recently LOGGED ruling — RR3 (last ruling wins) made
                    // code: Rulings folds in log order via ImmutableArray.Add.
                    // A competitor with no ruling is simply absent from the
                    // dictionary, so the collapse below passes a null ruled
                    // selection — byte-identical to the pre-ruling behaviour
                    // (RR1's regression guard).
                    var rulingByCompetitor = competition.Rulings
                        .Where(r => r.TaskRound.PhaseOrdinal == phase.Ordinal
                                 && r.TaskRound.RoundOrdinal == round.Ordinal
                                 && r.TaskRound.TaskRoundOrdinal == taskRound.Ordinal)
                        .GroupBy(r => r.CompetitorRef.ToString())
                        .ToDictionary(g => g.Key, g => g.Last());

                    // Candidates per competitor across every group of this
                    // task-round: one (role, destination, normalised score)
                    // tuple per LIVE entry (reflight-aggregate-destination.md
                    // WI-1 — the destination is the entry's counts-for round,
                    // resolved to the hosting round when null). Candidate
                    // collection is per-entry (a competitor may hold several
                    // live entries in one group — the Original competing for
                    // the 1000 basis beside its reflight role, and comp-135's
                    // make-ups beside both, decision 3); the collapse to one
                    // score per (competitor, destination) happens after the
                    // group loop (invariant R1′).
                    var candidatesByCompetitor = new Dictionary<string, List<(ReflightRole Role, int Destination, decimal Score)>>();

                    foreach (var group in taskRound.Groups)
                    {
                        // Annulled entries are excluded from group scoring: they
                        // produce NoResult (FlightSelector step 0) and, more
                        // importantly, an annulled attempt alongside its live
                        // replacement is the F3F.1.5 shape — the replacement is
                        // the one that scores.
                        //
                        // Keyed BY ENTRY (finding 7): the old competitor-string
                        // key collides when a competitor holds two live entries
                        // in one group, which is the legal reflight shape.
                        var groupEntries = taskRoundEntries
                            .Where(e => e.GroupRef == group.Id && e.Annulment is null)
                            .ToImmutableDictionary(e => ReflightSelector.EntryKey(e), e => e);

                        // A competitor drawn into a group with no Entry
                        // contributes no candidate — absent, not zero.
                        if (groupEntries.IsEmpty)
                            continue;

                        var groupResult = ScoreGroup(
                            group.Id.ToString(), taskDefinition, classDef, groupEntries, bindings,
                            taskRoundZeroPenalties.Value);

                        foreach (var (entryKey, taskResult) in groupResult.Results)
                        {
                            var entry = groupEntries[entryKey];
                            var competitorRef = entry.CompetitorRef.ToString();

                            if (!candidatesByCompetitor.TryGetValue(competitorRef, out var list))
                            {
                                list = [];
                                candidatesByCompetitor[competitorRef] = list;
                            }

                            list.Add((entry.Role, entry.CountsForRoundOrdinal ?? round.Ordinal, taskResult.RawScore));
                        }
                    }

                    // Collapse to ONE TaskRoundScore per competitor per
                    // destination (invariant R1′ — reflight-aggregate-destination.md
                    // WI-1), so the aggregate's keying at the phase close cannot
                    // see a duplicate. Candidates group by destination; each
                    // destination's candidates collapse per the class's
                    // ReflightRule exactly as the two-role law always did — a
                    // single-candidate destination passes Select unchanged (the
                    // lone-make-up shape). The CD ruling (below) is passed to
                    // every destination but can only ever land on the one
                    // two-candidate destination: the shape law implies at most
                    // one Original per (competitor, task-round), and only an
                    // Original+reflight pair makes two candidates, so the
                    // ruling's destination is never ambiguous.
                    //
                    // TaskCode/TaskOrdinal stay the HOSTING task-round's — the
                    // flight is scored in the group that hosted it — while
                    // RoundOrdinal is the destination, which is what keys the
                    // cell into the destination round's ladder slot
                    // (PhaseAggregator matches by (RoundOrdinal, TaskOrdinal,
                    // TaskCode) and is deliberately unchanged, D8).
                    foreach (var (competitorRef, candidates) in candidatesByCompetitor)
                    {
                        // Absent → null → the selector behaves exactly as with
                        // no ruling at all (GetValueOrDefault on the ruling
                        // itself, not on a Selection enum — ReflightSelection's
                        // default member is Replacement, which would silently
                        // rule for everyone).
                        var ruled = rulingByCompetitor.GetValueOrDefault(competitorRef)?.Selection;

                        foreach (var destinationGroup in candidates.GroupBy(c => c.Destination))
                        {
                            var selected = ReflightSelector.Select(
                                [.. destinationGroup.Select(c => (c.Role, c.Score))], reflightRule, ruled);
                            if (selected.IsFailure)
                            {
                                return Result<CompetitionResult>.Failure(
                                    selected.Code!, selected.Message!, selected.Defects);
                            }

                            if (!scoresByCompetitor.TryGetValue(competitorRef, out var list))
                            {
                                list = [];
                                scoresByCompetitor[competitorRef] = list;
                            }

                            list.Add(new TaskRoundScore(
                                taskRound.TaskRef, destinationGroup.Key, taskRound.Ordinal, selected.Value));

                            emittedCells.Add(new EmittedCell(
                                competitorRef,
                                destinationGroup.Key,
                                taskRound.TaskRef,
                                round.Ordinal,
                                taskRound.Ordinal));
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

            // D7 (reflight-aggregate-destination.md): score-time validation,
            // after the walk and before aggregation — a make-up's destination
            // must resolve to a walked slot or scoring refuses, never silently
            // drops the cell (an unmatched allScores entry vanishes inside
            // PhaseAggregator's Aggregate today, which is exactly the silence
            // these three checks exist to make unrepresentable).
            var walkedRounds = walkedSlots.Select(s => s.RoundOrdinal).ToHashSet();

            foreach (var cell in emittedCells.Where(c => c.RoundOrdinal != c.HostingRoundOrdinal))
            {
                if (!phase.Rounds.Any(r => r.Ordinal == cell.RoundOrdinal))
                {
                    return Result<CompetitionResult>.Failure(
                        "score.reflightDestinationUnresolved",
                        $"Competitor {cell.CompetitorRef} has a score counting for round {cell.RoundOrdinal}, "
                        + $"which does not exist in phase {phase.Ordinal} "
                        + $"(flown in round {cell.HostingRoundOrdinal}/task-round {cell.HostingTaskRoundOrdinal}).");
                }

                if (!walkedRounds.Contains(cell.RoundOrdinal))
                {
                    return Result<CompetitionResult>.Failure(
                        "score.reflightDestinationUnresolved",
                        $"Competitor {cell.CompetitorRef} has a score counting for round {cell.RoundOrdinal}, "
                        + "which was not walked — no entries anywhere in it, so the finding-5 filter dropped "
                        + $"it from the walk (flown in round {cell.HostingRoundOrdinal}/task-round "
                        + $"{cell.HostingTaskRoundOrdinal}).");
                }

                if (!walkedSlots.Contains((cell.RoundOrdinal, cell.HostingTaskRoundOrdinal, cell.TaskCode)))
                {
                    return Result<CompetitionResult>.Failure(
                        "score.reflightDestinationTaskMismatch",
                        $"Competitor {cell.CompetitorRef}'s make-up flown in round {cell.HostingRoundOrdinal}"
                        + $"/task-round {cell.HostingTaskRoundOrdinal} counts for round {cell.RoundOrdinal}, "
                        + $"whose task-round at (ordinal {cell.HostingTaskRoundOrdinal}, '{cell.TaskCode}') does "
                        + "not match the hosting task-round — single-task rounds always match; a multi-task "
                        + "mismatch is unwitnessed and refused (D3).");
                }
            }

            foreach (var conflict in emittedCells
                .GroupBy(c => (c.CompetitorRef, c.RoundOrdinal, c.TaskCode))
                .Where(g => g.Count() > 1))
            {
                return Result<CompetitionResult>.Failure(
                    "score.reflightDestinationConflict",
                    $"Competitor {conflict.Key.CompetitorRef} holds {conflict.Count()} scores for one "
                    + $"destination slot (round {conflict.Key.RoundOrdinal}, task '{conflict.Key.TaskCode}') — "
                    + "contributed by "
                    + $"{string.Join(", ", conflict.Select(c => $"round {c.HostingRoundOrdinal}/task-round {c.HostingTaskRoundOrdinal}"))}. "
                    + "Merging scores for one destination across task-rounds is unwitnessed and refused (D3).");
            }

            foreach (var (competitorRef, scores) in scoresByCompetitor)
            {
                var allScores = scores
                    .Select((score, index) => (score, index))
                    .ToDictionary(x => $"{x.score.RoundOrdinal}|{x.score.TaskOrdinal}|{x.index}", x => x.score);

                var phaseScores = Aggregate(competitorRef, phaseDefinition, rounds, allScores);

                totalsByCompetitor[competitorRef] =
                    totalsByCompetitor.GetValueOrDefault(competitorRef) + phaseScores.Aggregate;
                preDropTotals[competitorRef] =
                    preDropTotals.GetValueOrDefault(competitorRef) + phaseScores.PreDropAggregate;
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
                PreDropScore: preDropTotals.GetValueOrDefault(competitorRef) - penaltyResult.Deduction,
                Disqualified: penaltyResult.Disqualified));
        }

        // PromotionRule "appears only on a phase after the first"
        // (PhaseDefinition's doc comment) — the same phases[1] read
        // ScoreCompetition always used, kept for when a second phase exists.
        var promotion = classDef.Phases.Length > 1 ? classDef.Phases[1].Promotion : null;

        return Result<CompetitionResult>.Success(
            Rank(finalScores.ToImmutable(), classDef.FinalRanking, promotion));
    }

    // ---------------------------------------------------- D7 bookkeeping

    /// <summary>
    /// One TaskRoundScore emitted by the walk, with the task-round that hosted
    /// it — D7's raw material. <see cref="RoundOrdinal"/> is the round the cell
    /// aggregates into (the entry's counts-for round, or the hosting round when
    /// null); a cell whose RoundOrdinal differs from its hosting round is the
    /// destination-keyed make-up cell the checks below resolve.
    /// </summary>
    private sealed record EmittedCell(
        string CompetitorRef,
        int RoundOrdinal,
        string TaskCode,
        int HostingRoundOrdinal,
        int HostingTaskRoundOrdinal);

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
    /// The per-competitor map of aggregate-scoped Zero* penalties that anchor to
    /// one task-round (WI-1, D-A2) — the routing input <see cref="ScoreGroup"/>
    /// merges at its raw stage. A record qualifies when:
    /// <list type="bullet">
    /// <item>its recorded scope is TaskRound or Competition (aggregate scope),</item>
    /// <item>the class definition matching its infraction type (first match
    /// wins, mirroring PenaltyEngine.BuildDefinitionLookup) carries at least one
    /// ZeroFlight/ZeroRound/ZeroTask effect — keyed off <c>PenaltyEffect</c>
    /// values generically, never on a class (NFR-1),</item>
    /// <item>its <see cref="Penalty.TaskRound"/> coordinate equals
    /// <paramref name="coordinate"/> component-wise — the task-round the record
    /// names is the one that zeroes (D-A2), and</item>
    /// <item>it names a subject (<c>CompetitorRef</c>, same filter as
    /// GetAggregatePenalties — one competitor's penalty never hits the field).</item>
    /// </list>
    /// A Zero*-carrying record with a NULL coordinate cannot be anchored to any
    /// task-round: refused loudly here (D-A3 read-side safety net for events
    /// already in the log — the write side now rejects them at
    /// <c>Competition.RecordPenalty</c>), never skipped.
    /// One record is one occurrence; occurrences are grouped per competitor and
    /// infraction type, the same shape GetEntryPenalties produces.
    /// GetAggregatePenalties/ApplyAggregatePenalties are unchanged: the
    /// aggregate stage ignores Zero* effects, so no effect acts twice (D-A4).
    /// </summary>
    public static Result<ImmutableDictionary<string, ImmutableArray<RecordedPenalty>>> GetTaskRoundZeroPenalties(
        ImmutableArray<Penalty> competitionPenalties,
        ClassDefinition classDef,
        TaskRoundCoordinate coordinate)
    {
        // First-match-wins, same lookup discipline PenaltyEngine builds for both
        // of its stages.
        var defLookup = new Dictionary<string, PenaltyDefinition>();
        foreach (var def in classDef.Penalties)
        {
            if (!defLookup.ContainsKey(def.InfractionType))
                defLookup[def.InfractionType] = def;
        }

        static bool CarriesZeroEffect(PenaltyDefinition def) =>
            def.Effects.Any(e => e.Effect is PenaltyEffect.ZeroFlight
                                            or PenaltyEffect.ZeroRound
                                            or PenaltyEffect.ZeroTask);

        // Read-side safety net (D-A3): a Zero*-carrying aggregate record with no
        // coordinate would silently zero nothing if skipped — refuse instead.
        foreach (var p in competitionPenalties.Where(p =>
                     p.Scope is PenaltyScope.TaskRound or PenaltyScope.Competition))
        {
            if (defLookup.TryGetValue(p.InfractionType, out var def)
                && CarriesZeroEffect(def)
                && p.TaskRound is null)
            {
                return Result<ImmutableDictionary<string, ImmutableArray<RecordedPenalty>>>.Failure(
                    "score.zeroEffectUnanchored",
                    $"Penalty '{p.InfractionType}' against competitor "
                    + $"{p.CompetitorRef!.Value} (scope {p.Scope}) carries a zeroing effect but names no "
                    + "task-round — a zeroing rule always names the round it zeroes, so the record "
                    + "cannot be anchored and scoring refuses rather than zero nothing.");
            }
        }

        var map = competitionPenalties
            .Where(p => p.Scope is PenaltyScope.TaskRound or PenaltyScope.Competition)
            .Where(p => defLookup.TryGetValue(p.InfractionType, out var def) && CarriesZeroEffect(def))
            .Where(p => p.TaskRound == coordinate)
            .Where(p => p.CompetitorRef is not null)
            .GroupBy(p => p.CompetitorRef!.Value.ToString())
            .ToImmutableDictionary(
                g => g.Key,
                g => g.GroupBy(p => p.InfractionType)
                    .Select(t => new RecordedPenalty(t.Key, t.Count()))
                    .ToImmutableArray());

        return Result<ImmutableDictionary<string, ImmutableArray<RecordedPenalty>>>.Success(map);
    }

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
