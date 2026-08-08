// ScoreTaskRound — docs/plans/scoring-steel-thread-plan.md WI-7, slice 1.
//
// Scores one task-round's groups: what gets read out at the field when a
// group lands. GroupRef optional — unset scores every group in the task-round.
//
// CompetitionLoader.LoadAsync -> EntryCollector.CollectAsync ->
// ScoringService.ScoreGroup per group -> map the engine's string refs
// (finding 3) back to typed ids for the view, so no bare string crosses the
// Api boundary where the rest of the API uses ids.

using System.Collections.Immutable;
using Soarscore.Application.Competitions;
using Soarscore.Application.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Scoring;

/// <summary>One competitor's result within a scored group.</summary>
public sealed record CompetitorTaskResultView(
    CompetitorId CompetitorRef,
    TaskResultState State,
    decimal RawScore);

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
        var bindings = ScoringService.FlattenParameterBindings(competition.ParameterBindings);

        var views = new List<GroupScoreView>();

        foreach (var group in groups)
        {
            var groupEntries = entries.Values
                .Where(e => e.PhaseOrdinal == query.PhaseOrdinal
                         && e.RoundOrdinal == query.RoundOrdinal
                         && e.TaskRoundOrdinal == query.TaskRoundOrdinal
                         && e.GroupRef == group.Id)
                .ToImmutableDictionary(e => e.CompetitorRef.ToString(), e => e);

            // A group nobody has flown yet contributes no view — mirrors
            // ScoreCompetition's "absent, not zero" rule (finding 5).
            if (groupEntries.IsEmpty)
                continue;

            var groupResult = ScoringService.ScoreGroup(
                group.Id.ToString(), taskDefinition, classDef, groupEntries, bindings);

            views.Add(MapGroupResult(group.Id, groupResult));
        }

        return Result<IReadOnlyList<GroupScoreView>>.Success(views);
    }

    private static GroupScoreView MapGroupResult(GroupId groupRef, GroupResult result)
    {
        var results = result.Results
            .Select(kv => new CompetitorTaskResultView(
                CompetitorId.Parse(kv.Key, null), kv.Value.State, kv.Value.RawScore))
            .ToImmutableArray();

        return new GroupScoreView(
            GroupRef: groupRef,
            Results: results,
            WinnerRef: result.WinnerRef is { } winner ? CompetitorId.Parse(winner, null) : null,
            ValidCount: result.ValidCount,
            IsAnnulled: result.IsAnnulled);
    }
}
