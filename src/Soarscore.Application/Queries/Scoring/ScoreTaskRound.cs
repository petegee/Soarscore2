// ScoreTaskRound — kanban/completed/scoring-steel-thread-plan.md WI-7, slice 1.
//
// Scores one task-round's groups: what gets read out at the field when a
// group lands. GroupRef optional — unset scores every group in the task-round.
//
// CompetitionLoader.LoadAsync -> EntryCollector.CollectAsync ->
// ScoringService.ScoreGroup per group -> map the engine's string refs
// (finding 3) back to typed ids for the view, so no bare string crosses the
// Api boundary where the rest of the API uses ids.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Queries.Scoring;

/// <summary>One competitor's result within a scored group. A competitor with two
/// live entries in one group (the reflight shape) appears twice — once per
/// Entry — distinguished by <see cref="Role"/>. Collapse to one score per
/// task-round is the aggregate's job (ScoreCompetition), not this per-group
/// view's (reflight-groups.md WI-7).</summary>
/// <param name="RawScore">The post-normalisation score — unchanged semantics
/// (pre-normalisation-score-view-field.md trap 5).</param>
/// <param name="PreNormalisationScore">The engine's unnormalised (pre-normalisation)
/// score for this row — the value <see cref="RawScore"/> held entering
/// Normalise, preserved through the overwrite
/// (kanban/in-progress/pre-normalisation-score-view-field.md#WI-2).</param>
/// <param name="AwaitingCapture">The row's pending flights' awaited-metric
/// diagnostics, carried on the engine's TaskResult and surfaced here rather
/// than re-derived (kanban/in-progress/metric-absence-semantics.md#WI-3) — so
/// the field readout distinguishes "awaiting capture" from a genuine
/// no-result. Diagnostics are per TaskResult and rows are per Entry, so a
/// competitor with two live entries sees each entry's on its own row.</param>
/// <summary>One score term's awarded contribution on one flight, projected
/// verbatim from the engine's own <see cref="TermContribution"/>
/// (kanban/in-progress/per-term-score-breakdown.md#WI-1). Key by
/// <see cref="MetricRef"/>, not position: term order is an engine detail
/// (NdcScore law 3). Raw <c>Score</c> terms only — <c>ScoreNormalised</c>
/// terms evaluate inside normalisation and are out of scope.</summary>
/// <param name="TermIndex">Term-list position in the resolved task's
/// <c>Score</c> list — the same index the engine keys contributions by.</param>
/// <param name="MetricRef">The metric the term consumes, unwrapped through
/// conditionals per <see cref="ScoreTermRefs.GetTermMetricRef"/> — null for
/// <c>ConstantTerm</c>, which consumes none.</param>
/// <param name="MetricConsumed">Verbatim contribution input (always the
/// uncapped raw value).</param>
/// <param name="Points">Verbatim contribution award — no rounding, no
/// formatting, no normalisation math.</param>
public sealed record ScoreTermView(
    int TermIndex,
    string? MetricRef,
    decimal MetricConsumed,
    decimal Points);

/// <summary>One selected flight's per-term breakdown, in that entry's
/// selection (kanban/in-progress/per-term-score-breakdown.md#WI-1).
/// Per-flight <see cref="ScoreTermView.Points"/> sum to the flight's score
/// <b>before</b> PerTask-cap correction and <c>RawScore</c> rounding — the
/// deltas stay server-side, so the column never re-derives
/// <c>RawScore</c> by addition. Flight-gate-zeroed flights carry the
/// interpreter's own zeroing (empty terms); pending flights are omitted
/// (their signal stays on <c>AwaitingCapture</c>).</summary>
/// <param name="Sequence">The flight's 1-based sequence number (the
/// <c>flight.sequence</c> intrinsic), not the selection rank.</param>
public sealed record FlightScoreView(
    int Sequence,
    ImmutableArray<ScoreTermView> Terms);

/// <summary>One engine score warning, projected verbatim for the field readout
/// (kanban/backlog/turn-around-window-score-validation.md WI-2): NdcScore
/// renders code + message as-is — no arithmetic, no thresholds client-side.
/// </summary>
/// <param name="Code">Stable warning code (<c>score.windowSumExceeded</c>,
/// <c>score.turnaroundCapExceeded</c>).</param>
/// <param name="Message">Human sentence naming group, competitor/entry, summed
/// seconds, and thresholds at recorded precision.</param>
public sealed record ScoreWarningView(string Code, string Message);

public sealed record CompetitorTaskResultView(
    CompetitorId CompetitorRef,
    ReflightRole Role,
    TaskResultState State,
    decimal RawScore,
    decimal PreNormalisationScore,
    ImmutableArray<PendingFlightDiagnostic> AwaitingCapture,
    /// <summary>The selected flights' per-term breakdowns, verbatim from
    /// <c>TaskResult.Selection</c> (kanban/in-progress/per-term-score-breakdown.md#WI-1).
    /// Empty (never null) for <c>NoResult</c> rows — absence, never zero.</summary>
    ImmutableArray<FlightScoreView> Flights,
    /// <summary>The row's plausibility warnings, verbatim from the engine's
    /// per-entry warnings (kanban/backlog/turn-around-window-score-validation.md#WI-2).
    /// Empty (never null) for clean rows; NoResult rows carry empty. Scores are
    /// never altered by warnings — warn-through only.</summary>
    ImmutableArray<ScoreWarningView> Warnings);

/// <summary>One group's scored result — the GET /task-round-result response shape.</summary>
public sealed record GroupScoreView(
    GroupId GroupRef,
    ImmutableArray<CompetitorTaskResultView> Results,
    CompetitorId? WinnerRef,
    int ValidCount,
    bool IsAnnulled);

public readonly record struct ScoreTaskRound(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId? GroupRef) : IQuery<IReadOnlyList<GroupScoreView>>;

public sealed class ScoreTaskRoundHandler(IEventStore eventStore, IEntryQuery entryQuery)
    : IQueryHandler<ScoreTaskRound, IReadOnlyList<GroupScoreView>>
{
    public async Task<Result<IReadOnlyList<GroupScoreView>>> HandleAsync(
        ScoreTaskRound query, CancellationToken cancellationToken)
    {
        var competitionLoaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (competitionLoaded.IsFailure)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                competitionLoaded.Code!, competitionLoaded.Message!, competitionLoaded.Defects);
        }

        var competition = competitionLoaded.Value.Competition;

        var phase = competition.Phases.FirstOrDefault(p => p.Ordinal == query.PhaseOrdinal);
        if (phase is null)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                "scoreTaskRound.taskRoundNotFound", $"No phase with ordinal {query.PhaseOrdinal}.");
        }

        var round = phase.Rounds.FirstOrDefault(r => r.Ordinal == query.RoundOrdinal);
        if (round is null)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                "scoreTaskRound.taskRoundNotFound", $"No round with ordinal {query.RoundOrdinal} in phase {query.PhaseOrdinal}.");
        }

        var taskRound = round.TaskRounds.FirstOrDefault(tr => tr.Ordinal == query.TaskRoundOrdinal);
        if (taskRound is null)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                "scoreTaskRound.taskRoundNotFound", $"No task-round with ordinal {query.TaskRoundOrdinal} in round {query.RoundOrdinal}.");
        }

        var groups = taskRound.Groups;
        if (query.GroupRef is { } groupRef)
        {
            groups = groups.Where(g => g.Id == groupRef).ToImmutableArray();
            if (groups.IsEmpty)
            {
                return Result<IReadOnlyList<GroupScoreView>>.Failure(
                    "scoreTaskRound.groupNotFound", $"No group with id {groupRef} in this task-round.");
            }
        }

        var taskDefinition = competition.AdoptedRules.Definition.Phases
            .SelectMany(p => p.Tasks)
            .FirstOrDefault(t => t.Code == taskRound.TaskRef);

        if (taskDefinition is null)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                "scoreTaskRound.taskNotDeclared",
                $"Task-round references task '{taskRound.TaskRef}', which is not declared by the adopted class definition.");
        }

        var entriesLoaded = await EntryCollector.CollectAsync(eventStore, entryQuery, query.CompetitionRef, cancellationToken);
        if (entriesLoaded.IsFailure)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                entriesLoaded.Code!, entriesLoaded.Message!, entriesLoaded.Defects);
        }

        var entries = entriesLoaded.Value;
        var classDef = competition.AdoptedRules.Definition;
        var bindings = ScoringService.FlattenParameterBindings(competition.ParameterBindings, query.PhaseOrdinal, query.RoundOrdinal);

        // Aggregate-scoped Zero* penalties routed to this task-round (WI-1,
        // D-A2 of the story it cites): the per-competitor map is shared by every
        // group here, exactly as ScoreCompetition shares it across its walk, so
        // the provisional leaderboard shows the zeroing too.
        var taskRoundZeroPenalties = ScoringService.GetTaskRoundZeroPenalties(
            competition.Penalties,
            classDef,
            new TaskRoundCoordinate(query.PhaseOrdinal, query.RoundOrdinal, query.TaskRoundOrdinal));

        if (taskRoundZeroPenalties.IsFailure)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                taskRoundZeroPenalties.Code!, taskRoundZeroPenalties.Message!, taskRoundZeroPenalties.Defects);
        }

        var views = new List<GroupScoreView>();

        // WI-2 (per-term-score-breakdown.md): resolve once per task-round —
        // the group loop shares the task — so MapGroupResult can project each
        // row's Selection through the resolved term list. ScoreGroup resolves
        // internally too; this re-resolution is pure and cheap. Unreachable in
        // normal operation (bindings were validated at declaration/adoption);
        // TaskResolver's entry.parameterUnbound precedent applies.
        ImmutableArray<ScoreTerm> scoreTerms;
        try
        {
            scoreTerms = ParameterResolver.ResolveTask(taskDefinition, bindings, classDef.Parameters).Score;
        }
        catch (UnresolvedParameterException ex)
        {
            return Result<IReadOnlyList<GroupScoreView>>.Failure(
                "scoreTaskRound.parameterUnbound", ex.Message);
        }

        foreach (var group in groups)
        {
            // Keyed BY ENTRY (reflight-groups.md WI-7, finding 7): a competitor
            // may hold two live entries in one group (the reflight shape), so
            // the old competitor-string key would collide — and would also
            // silently drop one of the two rows the per-group view must report
            // honestly (planner's call). The side map decodes the results'
            // entry keys back to the Entry for the view rows.
            var groupEntries = entries.Values
                .Where(e => e.PhaseOrdinal == query.PhaseOrdinal
                         && e.RoundOrdinal == query.RoundOrdinal
                         && e.TaskRoundOrdinal == query.TaskRoundOrdinal
                         && e.GroupRef == group.Id
                         && e.Annulment is null)
                .ToImmutableDictionary(e => ReflightSelector.EntryKey(e), e => e);

            // A group nobody has flown yet contributes no view — mirrors
            // ScoreCompetition's "absent, not zero" rule (finding 5).
            if (groupEntries.IsEmpty)
                continue;

            var groupResult = ScoringService.ScoreGroup(
                group.Id.ToString(), taskDefinition, classDef, groupEntries, bindings,
                taskRoundZeroPenalties.Value,
                // WI-4 (tape-points-landing-seeds.md): readings compose with the
                // class table at resolution; distances naming none take the
                // existing path. No declaration here means distances only.
                competition.DeclaredInstruments?.Instruments ?? []);

            views.Add(MapGroupResult(group.Id, groupResult, groupEntries, scoreTerms));
        }

        return Result<IReadOnlyList<GroupScoreView>>.Success(views);
    }

    private static GroupScoreView MapGroupResult(
        GroupId groupRef,
        GroupResult result,
        IReadOnlyDictionary<string, Entry> entriesByKey,
        ImmutableArray<ScoreTerm> scoreTerms)
    {
        // The engine's uninitialised AwaitingCapture (a default ImmutableArray —
        // TaskResult's own doc) must cross the Api boundary as a real empty
        // array, or response serialisation dies mid-stream on exactly those
        // rows: on this runtime even IsEmpty throws on a default instance, so
        // the guard is IsDefaultOrEmpty (found by the Gliderscore parity
        // harness, metric-absence-semantics.md#WI-3).
        static ImmutableArray<PendingFlightDiagnostic> BoundaryEmpty(
            ImmutableArray<PendingFlightDiagnostic> awaiting) =>
            awaiting.IsDefaultOrEmpty ? ImmutableArray<PendingFlightDiagnostic>.Empty : awaiting;

        var results = result.Results
            .Select(kv => new CompetitorTaskResultView(
                entriesByKey[kv.Key].CompetitorRef,
                entriesByKey[kv.Key].Role,
                kv.Value.State,
                kv.Value.RawScore,
                result.PreNormalisationScores[kv.Key],
                BoundaryEmpty(kv.Value.AwaitingCapture),
                ProjectFlights(kv.Value.Selection, scoreTerms),
                ProjectWarnings(result.Warnings, kv.Key)))
            .ToImmutableArray();

        return new GroupScoreView(
            GroupRef: groupRef,
            Results: results,
            WinnerRef: result.WinnerRef is { } winner && entriesByKey.ContainsKey(winner)
                ? entriesByKey[winner].CompetitorRef
                : null,
            ValidCount: result.ValidCount,
            IsAnnulled: result.IsAnnulled);
    }

    /// <summary>
    /// WI-2 (turn-around-window-score-validation.md): per-row warning projection
    /// from the engine's <c>GroupResult.Warnings</c> (WI-1). Verbatim — no
    /// formatting, no threshold math. A missing key yields empty (never
    /// throws); a null map yields empty; a default array value is normalised
    /// to a real empty so serialisation stays safe (the AwaitingCapture
    /// precedent above). NoResult rows carry empty — the engine never warns
    /// them, and absence here is never a warning.
    /// </summary>
    internal static ImmutableArray<ScoreWarningView> ProjectWarnings(
        IReadOnlyDictionary<string, ImmutableArray<ScoreWarning>>? warningsByKey,
        string key)
    {
        if (warningsByKey is null)
            return ImmutableArray<ScoreWarningView>.Empty;
        if (!warningsByKey.TryGetValue(key, out var warnings) || warnings.IsDefaultOrEmpty)
            return ImmutableArray<ScoreWarningView>.Empty;
        return [.. warnings.Select(w => new ScoreWarningView(w.Code, w.Message))];
    }

    /// <summary>
    /// WI-2 (per-term-score-breakdown.md): project <c>TaskResult.Selection</c>
    /// through the resolved term list. Values cross verbatim (decimal as-is —
    /// no rounding, no formatting, no normalisation math). NoResult
    /// (<c>Selection: null</c>) yields an empty array, never null; pending
    /// flights never enter <c>Selection</c> so they are omitted by
    /// construction. Flight-gate-zeroed flights carry the interpreter's own
    /// zeroing (empty contributions → empty terms).
    /// </summary>
    private static ImmutableArray<FlightScoreView> ProjectFlights(
        SelectedFlights? selection,
        ImmutableArray<ScoreTerm> scoreTerms)
    {
        if (selection is null)
            return ImmutableArray<FlightScoreView>.Empty;

        var builder = ImmutableArray.CreateBuilder<FlightScoreView>(selection.Flights.Length);
        for (var i = 0; i < selection.Flights.Length; i++)
        {
            var flight = selection.Flights[i];
            builder.Add(new FlightScoreView(
                ReadSequence(flight, i),
                ProjectTerms(flight, scoreTerms)));
        }
        return builder.ToImmutable();
    }

    private static ImmutableArray<ScoreTermView> ProjectTerms(
        InterpretedFlight flight,
        ImmutableArray<ScoreTerm> scoreTerms)
    {
        if (flight.TermContributions.Count == 0)
            return ImmutableArray<ScoreTermView>.Empty;

        var builder = ImmutableArray.CreateBuilder<ScoreTermView>(flight.TermContributions.Count);
        foreach (var (termIndex, contribution) in flight.TermContributions)
        {
            var metricRef = termIndex >= 0 && termIndex < scoreTerms.Length
                ? ScoreTermRefs.GetTermMetricRef(scoreTerms[termIndex])
                : null;
            builder.Add(new ScoreTermView(
                termIndex, metricRef, contribution.MetricConsumed, contribution.Points));
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// The flight's 1-based sequence from its own <c>flight.sequence</c>
    /// intrinsic — never the selection rank (BestNFlights re-ranks). Falls
    /// back to position when the intrinsic is absent, which is unreachable
    /// through the interpreter (it always injects it).
    /// </summary>
    private static int ReadSequence(InterpretedFlight flight, int position)
    {
        if (flight.Metrics.TryGetValue("flight.sequence", out var seq)
            && seq.Kind == MeasuredKind.Number
            && seq.Number.HasValue)
            return (int)seq.Number.Value;
        return position + 1;
    }
}
