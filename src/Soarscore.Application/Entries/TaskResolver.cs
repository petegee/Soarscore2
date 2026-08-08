// Shared by OpenFlightHandler and CaptureMeasurementHandler — the phase ->
// round -> task-round -> task walk both need to reach the resolved
// MaxLaunches / Metrics for the task-round an Entry was opened against.
// docs/plans/capture-a-score-steel-thread-plan.md WI-8: "there is no third
// copy of a phase->round->task-round->task traversal worth writing".
//
// Competition.OpenEntry (WI-2, already landed) walks this same path inline
// to derive the working time; it is not repointed at this helper here — that
// refactor is out of this work item's scope and is recorded in tech-debt.md
// instead, so as not to touch Competition.cs while other work may be
// in-flight against it.
//
// Every failure here is, by construction, unreachable in normal operation:
// the Entry's coordinate was validated once by Competition.OpenEntry, and
// Competition never removes a Phase, Round or TaskRound once drawn. A single
// shared code covers the whole walk rather than one per step, because the
// only way any of these fires is the same class of defect — the coordinate
// no longer resolves against this Competition.

using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Entries;

internal static class TaskResolver
{
    public static Result<ResolvedTask> Resolve(
        Competition competition, int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal)
    {
        var phase = competition.Phases.FirstOrDefault(p => p.Ordinal == phaseOrdinal);
        if (phase is null)
        {
            return Result<ResolvedTask>.Failure(
                "entry.taskRoundNotFound", $"No phase with ordinal {phaseOrdinal} in competition {competition.Id}.");
        }

        var round = phase.Rounds.FirstOrDefault(r => r.Ordinal == roundOrdinal);
        if (round is null)
        {
            return Result<ResolvedTask>.Failure(
                "entry.taskRoundNotFound", $"No round with ordinal {roundOrdinal} in phase {phaseOrdinal}.");
        }

        var taskRound = round.TaskRounds.FirstOrDefault(tr => tr.Ordinal == taskRoundOrdinal);
        if (taskRound is null)
        {
            return Result<ResolvedTask>.Failure(
                "entry.taskRoundNotFound", $"No task-round with ordinal {taskRoundOrdinal} in round {roundOrdinal}.");
        }

        var task = competition.AdoptedRules.Definition.Phases
            .SelectMany(p => p.Tasks)
            .FirstOrDefault(t => t.Code == taskRound.TaskRef);
        if (task is null)
        {
            return Result<ResolvedTask>.Failure(
                "entry.taskRoundNotFound",
                $"Task-round references task '{taskRound.TaskRef}', which is not declared by the adopted class definition.");
        }

        // Flattened last-write-wins, exactly as Competition.cs's DrawPhase and
        // Competition.OpenEntry already do.
        var bindings = competition.ParameterBindings
            .GroupBy(b => b.ParameterName)
            .ToDictionary(g => g.Key, g => g.Last().BoundValue);

        try
        {
            var resolvedTask = ParameterResolver.ResolveTask(task, bindings, competition.AdoptedRules.Definition.Parameters);
            return Result<ResolvedTask>.Success(resolvedTask);
        }
        catch (UnresolvedParameterException ex)
        {
            return Result<ResolvedTask>.Failure("entry.parameterUnbound", ex.Message);
        }
    }
}
