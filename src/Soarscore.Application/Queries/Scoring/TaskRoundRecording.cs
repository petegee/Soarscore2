// GetTaskRoundRecording — kanban/completed/entry-completeness-indicator.md WI-1.
//
// A read-side indicator of what is recorded for one task-round: who was drawn
// into each group and has no Entry, whose Entry exists but holds no Flight,
// and which Flights lack a metric the task declares. A query, never a state:
// it gates nothing (NFR-4) and computes nothing a TaskRound transition could
// be derived from. Presence of data proves a task-round not ready; absence can
// never prove it ready — so every field below states a fact about what is
// recorded ("not recorded", "missing") and none states a verdict. Nothing here
// may be phrased or later renamed as "complete".
//
// Field spots follow the same law (lane-assignment.md WI-5): the recorded
// assignment is stated as recorded, spot-ordered; empty means *unassigned* —
// a fact, never a gap — and a spot whose competitor has since withdrawn is
// still listed, its vacancy the consumer's derivation against
// ExpectedCompetitorRefs.
//
// Shape mirrors ScoreTaskRoundHandler: CompetitionLoader walk by ordinal ->
// task definition by TaskRef -> slice entry_index at the full coordinate ->
// fold each matched stream via EntryLoader -> bucket per group. The bucketing
// itself is RecordingCore, a pure function over already-loaded state, so
// TaskRoundRecordingPropertyTests can drive it with no store at all.

using System.Collections.Immutable;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Queries.Scoring;

/// <summary>One flight's declared-but-absent metrics, in the task's declared order.</summary>
public sealed record FlightGapsView(
    int Sequence,
    ImmutableArray<string> MissingMetrics);

/// <summary>One live Entry's gapped flights — only Entries with at least one gap appear.</summary>
public sealed record EntryGapsView(
    EntryId EntryRef,
    CompetitorId CompetitorRef,
    ReflightRole Role,
    ImmutableArray<FlightGapsView> Flights);

/// <summary>
/// One recorded field-spot assignment — the spot label and the competitor it
/// names, exactly as the <see cref="GroupSpotsAssigned"/> event recorded it
/// (lane-assignment.md WI-5). The label's physical meaning is the venue's;
/// the view only states the mapping.
/// </summary>
public sealed record GroupSpotView(int Spot, CompetitorId CompetitorRef);

/// <summary>
/// One group's recording status. The lists are the answer; counts ("20/20
/// entries, no gaps") derive client-side from their lengths.
///
/// Expected is the drawn allocation minus withdrawn competitors — withdrawal
/// being the established way to record "won't fly". A competitor who never
/// launched and was never withdrawn reads as NotRecorded, which is the truth
/// of the record: only the CD can say which it was, which is exactly why this
/// view decides nothing.
///
/// Spots are the field-spot assignment as recorded, ordered by spot
/// (lane-assignment.md WI-5, decisions D2/D4): empty means *unassigned* — a
/// fact, not an error and not a default — and an entry whose competitor has
/// since withdrawn is still listed, reading as vacant by the consumer's own
/// join against <see cref="ExpectedCompetitorRefs"/>; the view states facts,
/// never verdicts.
/// </summary>
public sealed record GroupRecordingView(
    GroupId GroupRef,
    int Ordinal,
    ImmutableArray<CompetitorId> ExpectedCompetitorRefs,
    ImmutableArray<CompetitorId> NotRecordedCompetitorRefs,
    ImmutableArray<CompetitorId> RecordedWithoutFlightCompetitorRefs,
    ImmutableArray<EntryGapsView> MetricGaps,
    ImmutableArray<GroupSpotView> Spots);

/// <summary>One task-round's recording status — the GET /task-round-recording response shape.</summary>
public sealed record TaskRoundRecordingView(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    string TaskRef,
    ImmutableArray<GroupRecordingView> Groups);

public readonly record struct GetTaskRoundRecording(
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId? GroupRef) : IQuery<TaskRoundRecordingView>;

public sealed class GetTaskRoundRecordingHandler(IEventStore eventStore, IEntryQuery entryQuery)
    : IQueryHandler<GetTaskRoundRecording, TaskRoundRecordingView>
{
    public async Task<Result<TaskRoundRecordingView>> HandleAsync(
        GetTaskRoundRecording query, CancellationToken cancellationToken)
    {
        var competitionLoaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (competitionLoaded.IsFailure)
        {
            return Result<TaskRoundRecordingView>.Failure(
                competitionLoaded.Code!, competitionLoaded.Message!, competitionLoaded.Defects);
        }

        var competition = competitionLoaded.Value.Competition;

        var phase = competition.Phases.FirstOrDefault(p => p.Ordinal == query.PhaseOrdinal);
        if (phase is null)
        {
            return Result<TaskRoundRecordingView>.Failure(
                "taskRoundRecording.taskRoundNotFound", $"No phase with ordinal {query.PhaseOrdinal}.");
        }

        var round = phase.Rounds.FirstOrDefault(r => r.Ordinal == query.RoundOrdinal);
        if (round is null)
        {
            return Result<TaskRoundRecordingView>.Failure(
                "taskRoundRecording.taskRoundNotFound",
                $"No round with ordinal {query.RoundOrdinal} in phase {query.PhaseOrdinal}.");
        }

        var taskRound = round.TaskRounds.FirstOrDefault(tr => tr.Ordinal == query.TaskRoundOrdinal);
        if (taskRound is null)
        {
            return Result<TaskRoundRecordingView>.Failure(
                "taskRoundRecording.taskRoundNotFound",
                $"No task-round with ordinal {query.TaskRoundOrdinal} in round {query.RoundOrdinal}.");
        }

        var groups = taskRound.Groups;
        if (query.GroupRef is { } groupRef)
        {
            groups = groups.Where(g => g.Id == groupRef).ToImmutableArray();
            if (groups.IsEmpty)
            {
                return Result<TaskRoundRecordingView>.Failure(
                    "taskRoundRecording.groupNotFound", $"No group with id {groupRef} in this task-round.");
            }
        }

        var taskDefinition = competition.AdoptedRules.Definition.Phases
            .SelectMany(p => p.Tasks)
            .FirstOrDefault(t => t.Code == taskRound.TaskRef);

        if (taskDefinition is null)
        {
            return Result<TaskRoundRecordingView>.Failure(
                "taskRoundRecording.taskNotDeclared",
                $"Task-round references task '{taskRound.TaskRef}', which is not declared by the adopted class definition.");
        }

        // Sliced at the exact coordinate, so only this task-round's streams are
        // folded — fewer than EntryCollector's whole-competition fan-out, same
        // justification for folding streams at all (LADR-0001 §3).
        var summaries = await entryQuery.FindAsync(
            query.CompetitionRef,
            phaseOrdinal: query.PhaseOrdinal,
            roundOrdinal: query.RoundOrdinal,
            taskRoundOrdinal: query.TaskRoundOrdinal,
            groupRef: query.GroupRef,
            competitorRef: null,
            cancellationToken);

        var entries = new Dictionary<EntryId, Entry>(summaries.Count);
        foreach (var summary in summaries)
        {
            var loadedEntry = await EntryLoader.LoadAsync(eventStore, summary.Id, cancellationToken);
            if (loadedEntry.IsFailure)
            {
                return Result<TaskRoundRecordingView>.Failure(
                    loadedEntry.Code!, loadedEntry.Message!, loadedEntry.Defects);
            }

            entries[summary.Id] = loadedEntry.Value.Entry;
        }

        var declaredMetrics = taskDefinition.Metrics.Select(m => m.Name).ToImmutableArray();

        var groupViews = RecordingCore.ComputeGroupViews(
            competition, query.PhaseOrdinal, query.RoundOrdinal, query.TaskRoundOrdinal,
            groups, entries, declaredMetrics);

        return Result<TaskRoundRecordingView>.Success(new TaskRoundRecordingView(
            query.CompetitionRef,
            query.PhaseOrdinal,
            query.RoundOrdinal,
            query.TaskRoundOrdinal,
            taskRound.TaskRef,
            groupViews));
    }
}

/// <summary>
/// The bucketing rules, pure over already-loaded state. Keyed BY COMPETITOR
/// for the three membership lists — a competitor holding two live Entries in
/// one group (the reflight shape) records once and flies once — while gaps are
/// reported PER ENTRY, both Entries', since either may hold an unrecorded
/// metric. Annulled Entries have no result and count as neither recording nor
/// gapping; a competitor whose only Entry is annulled reads as NotRecorded.
/// </summary>
internal static class RecordingCore
{
    public static ImmutableArray<GroupRecordingView> ComputeGroupViews(
        Competition competition,
        int phaseOrdinal,
        int roundOrdinal,
        int taskRoundOrdinal,
        ImmutableArray<Group> groups,
        IReadOnlyDictionary<EntryId, Entry> entries,
        ImmutableArray<string> declaredMetrics)
    {
        var competitorsById = competition.Competitors.ToDictionary(c => c.Id);

        var views = new List<GroupRecordingView>(groups.Length);

        foreach (var group in groups)
        {
            // Draw order preserved everywhere: Expected keeps Group.CompetitorRefs'
            // order, and the other two lists keep Expected's relative order.
            var expected = group.CompetitorRefs
                .Where(c => competitorsById.TryGetValue(c, out var competitor) && competitor.WithdrawnAt is null)
                .ToImmutableArray();
            var expectedSet = expected.ToImmutableHashSet();

            var liveEntries = entries.Values
                .Where(e => e.Annulment is null
                         && e.PhaseOrdinal == phaseOrdinal
                         && e.RoundOrdinal == roundOrdinal
                         && e.TaskRoundOrdinal == taskRoundOrdinal
                         && e.GroupRef == group.Id
                         && expectedSet.Contains(e.CompetitorRef))
                .OrderBy(e => e.Id.Value)
                .ToImmutableArray();

            var recorded = expected
                .Where(c => liveEntries.Any(e => e.CompetitorRef == c))
                .ToImmutableArray();
            var flown = recorded
                .Where(c => liveEntries.Any(e => e.CompetitorRef == c && e.Flights.Length > 0))
                .ToImmutableHashSet();
            var notRecorded = expected.Where(c => !recorded.Contains(c)).ToImmutableArray();
            var recordedWithoutFlight = recorded.Where(c => !flown.Contains(c)).ToImmutableArray();

            var metricGaps = liveEntries
                .Select(entry => new EntryGapsView(
                    entry.Id,
                    entry.CompetitorRef,
                    entry.Role,
                    entry.Flights
                        .Select(flight => new FlightGapsView(
                            flight.Sequence,
                            declaredMetrics
                                .Where(metric => !flight.Measurements.Any(m => m.Metric == metric))
                                .ToImmutableArray()))
                        .Where(gaps => !gaps.MissingMetrics.IsEmpty)
                        .ToImmutableArray()))
                .Where(gaps => !gaps.Flights.IsEmpty)
                .ToImmutableArray();

            // The spot assignment as recorded, spot-ordered (lane-assignment.md
            // WI-5): the fold stores it as given, the view sorts. Empty means
            // unassigned — a fact, not an error and not a default (D2); a
            // competitor withdrawn after assignment is still listed, reading
            // as vacant by the consumer's join against Expected (D4).
            var spots = group.Spots
                .OrderBy(s => s.Spot)
                .Select(s => new GroupSpotView(s.Spot, s.CompetitorRef))
                .ToImmutableArray();

            views.Add(new GroupRecordingView(
                group.Id, group.Ordinal, expected, notRecorded, recordedWithoutFlight, metricGaps, spots));
        }

        return [.. views];
    }
}
