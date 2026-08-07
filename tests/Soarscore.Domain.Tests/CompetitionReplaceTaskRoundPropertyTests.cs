using System.Collections.Immutable;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for <see cref="Competition"/>'s private ReplaceTaskRound
/// navigation (LADR-0003: CsCheck) — the ordinal-addressed find-and-replace
/// across Phase/Round/TaskRound that ReflightGroupAppended, TaskRoundCompleted
/// and TaskRoundAnnulled all share. CompetitionFoldTests exercises it through
/// two or three fixed, hand-built shapes; this generates the shape (how many
/// phases, rounds and task-rounds) and the target ordinal, and checks the
/// general claim: mutating the addressed TaskRound changes exactly that node
/// and leaves every other Phase/Round/TaskRound in the tree untouched.
/// </summary>
public class CompetitionReplaceTaskRoundPropertyTests
{
    private enum EventKind { AppendGroup, Complete, Annul }

    private static readonly Gen<(
        int phaseCount,
        int roundsPerPhase,
        int taskRoundsPerRound,
        int targetPhase,
        int targetRound,
        int targetTaskRound,
        EventKind kind)> Scenario =
        from phaseCount in Gen.Int[1, 3]
        from roundsPerPhase in Gen.Int[1, 3]
        from taskRoundsPerRound in Gen.Int[1, 3]
        from targetPhase in Gen.Int[1, phaseCount]
        from targetRound in Gen.Int[1, roundsPerPhase]
        from targetTaskRound in Gen.Int[1, taskRoundsPerRound]
        from kind in Gen.OneOfConst(EventKind.AppendGroup, EventKind.Complete, EventKind.Annul)
        select (phaseCount, roundsPerPhase, taskRoundsPerRound, targetPhase, targetRound, targetTaskRound, kind);

    [Fact]
    public void Mutating_one_task_round_leaves_every_other_task_round_untouched()
    {
        Scenario.Sample(t =>
        {
            var before = BuildCompetition(t.phaseCount, t.roundsPerPhase, t.taskRoundsPerRound);
            var beforeFlat = Flatten(before);
            var target = (t.targetPhase, t.targetRound, t.targetTaskRound);

            var after = t.kind switch
            {
                EventKind.AppendGroup => before.Apply(new ReflightGroupAppended(
                    t.targetPhase, t.targetRound, t.targetTaskRound,
                    new Group { Id = GroupId.New(), Ordinal = 99, CompetitorRefs = [CompetitorId.New()] },
                    DateTimeOffset.UtcNow)),
                EventKind.Complete => before.Apply(new TaskRoundCompleted(
                    t.targetPhase, t.targetRound, t.targetTaskRound, DateTimeOffset.UtcNow)),
                EventKind.Annul => before.Apply(new TaskRoundAnnulled(
                    t.targetPhase, t.targetRound, t.targetTaskRound, "test", DateTimeOffset.UtcNow)),
                _ => throw new InvalidOperationException(),
            };

            var afterFlat = Flatten(after);

            // Same tree shape: nothing added, removed or reordered.
            if (beforeFlat.Count != afterFlat.Count)
            {
                return false;
            }

            var untouchedNodesUnchanged = beforeFlat.Keys
                .Where(k => k != target)
                .All(k =>
                {
                    var b = beforeFlat[k];
                    var a = afterFlat[k];
                    return a.State == b.State && a.TaskRef == b.TaskRef && a.Groups.SequenceEqual(b.Groups);
                });

            var targetChangedAsExpected = t.kind switch
            {
                EventKind.AppendGroup =>
                    afterFlat[target].Groups.Length == beforeFlat[target].Groups.Length + 1
                    && afterFlat[target].State == beforeFlat[target].State,
                EventKind.Complete =>
                    afterFlat[target].State == TaskRoundState.Complete
                    && afterFlat[target].Groups.SequenceEqual(beforeFlat[target].Groups),
                EventKind.Annul =>
                    afterFlat[target].State == TaskRoundState.Annulled
                    && afterFlat[target].Groups.SequenceEqual(beforeFlat[target].Groups),
                _ => false,
            };

            return untouchedNodesUnchanged && targetChangedAsExpected;
        });
    }

    // ------------------------------------------------------------- helpers

    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static Competition BuildCompetition(int phaseCount, int roundsPerPhase, int taskRoundsPerRound)
    {
        var at = DateTimeOffset.UtcNow;
        var adoptedRules = new AdoptedRules
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = at,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Prop Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, at);

        var competition = Competition.Create(created);

        for (var phaseOrdinal = 1; phaseOrdinal <= phaseCount; phaseOrdinal++)
        {
            var rounds = Enumerable.Range(1, roundsPerPhase)
                .Select(roundOrdinal => BuildRound(phaseOrdinal, roundOrdinal, taskRoundsPerRound))
                .ToImmutableArray();
            var draw = new Draw { CreatedAt = at, Status = "Accepted" };

            competition = competition.Apply(new PhaseDrawn(phaseOrdinal, PhaseType.Preliminary, draw, rounds, at));
        }

        return competition;
    }

    private static Round BuildRound(int phaseOrdinal, int roundOrdinal, int taskRoundsPerRound)
    {
        var taskRounds = Enumerable.Range(1, taskRoundsPerRound)
            .Select(taskRoundOrdinal => BuildTaskRound(phaseOrdinal, roundOrdinal, taskRoundOrdinal))
            .ToImmutableArray();

        return new Round { Ordinal = roundOrdinal, TaskRounds = taskRounds };
    }

    /// <summary>TaskRef and Group ordinal are derived from the full coordinate so decoy nodes are distinguishable from the target and from each other.</summary>
    private static TaskRound BuildTaskRound(int phaseOrdinal, int roundOrdinal, int taskRoundOrdinal) => new()
    {
        Ordinal = taskRoundOrdinal,
        State = TaskRoundState.Drawn,
        TaskRef = $"P{phaseOrdinal}R{roundOrdinal}T{taskRoundOrdinal}",
        Groups = [new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] }],
    };

    private static Dictionary<(int Phase, int Round, int TaskRound), TaskRound> Flatten(Competition competition) =>
        competition.Phases
            .SelectMany(phase => phase.Rounds
                .SelectMany(round => round.TaskRounds
                    .Select(taskRound => ((phase.Ordinal, round.Ordinal, taskRound.Ordinal), taskRound))))
            .ToDictionary(x => x.Item1, x => x.taskRound);
}
