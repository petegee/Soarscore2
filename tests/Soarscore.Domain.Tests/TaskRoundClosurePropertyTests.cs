// kanban/completed/task-round-lifecycle.md WI-10, invariant C, stated there
// verbatim as:
//
//   **Invariant C — closure is scoped, and always revocable.** For any drawn
//   shape and any task-round: after `TaskRoundCompleted` or `TaskRoundAnnulled`
//   folds, `OpenEntry` into that task-round fails for every competitor drawn
//   into it, while `OpenEntry` into every *other* task-round is unaffected —
//   and after `TaskRoundReopened` folds, `OpenEntry` succeeds again for exactly
//   the competitors it would have accepted before the closure. Generated over
//   the shape, the target ordinal, and the close/reopen sequence, so no
//   arrangement of closures can strand a task-round. The second half is the
//   governing principle expressed as a test: a late score is never permanently
//   locked out.
//
// "Unaffected" and "exactly the competitors it would have accepted" are both
// checked against a probe of the whole drawn shape taken BEFORE any closure —
// every (round, group, competitor) coordinate, recorded as either success or
// the defect code OpenEntry returned — so the property compares three complete
// pictures rather than spot-checking one coordinate.
//
// A small synthetic class, like ScoringServicePropertyTests: the invariant is
// about Competition.OpenEntry's closure check (Competition.cs's
// openEntry.taskRoundClosed), not about any class's rules.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class TaskRoundClosurePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);

    private enum ClosureKind { Complete, Annul }

    private static readonly Gen<(int fieldSize, int rounds, int targetRound, ClosureKind closure)> Scenario =
        from fieldSize in Gen.Int[2, 8]
        from rounds in Gen.Int[1, 4]
        from targetRound in Gen.Int[1, rounds]
        from closure in Gen.OneOfConst(ClosureKind.Complete, ClosureKind.Annul)
        select (fieldSize, rounds, targetRound, closure);

    [Fact]
    public void Closing_a_task_round_blocks_only_that_task_round_and_reopening_restores_it_exactly()
    {
        Scenario.Sample(t =>
        {
            var drawn = BuildDrawnCompetition(t.fieldSize, t.rounds);
            var before = Probe(drawn);

            // Every coordinate of a freshly drawn shape must be open, or the
            // rest of the property is comparing against nothing.
            before.Values.Should().AllBe(Accepted);

            var closed = t.closure switch
            {
                ClosureKind.Complete => drawn.Apply(new TaskRoundCompleted(0, t.targetRound, 1, Now)),
                _ => drawn.Apply(new TaskRoundAnnulled(0, t.targetRound, 1, "found faulty", Now)),
            };

            var afterClosure = Probe(closed);

            var expectedAfterClosure = before.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Key.Round == t.targetRound ? "openEntry.taskRoundClosed" : kvp.Value);

            afterClosure.Should().BeEquivalentTo(expectedAfterClosure);

            var reopened = closed.Apply(new TaskRoundReopened(0, t.targetRound, 1, "late score", Now));

            // "succeeds again for exactly the competitors it would have
            // accepted before the closure" — the whole picture, restored.
            Probe(reopened).Should().BeEquivalentTo(before);
        });
    }

    // ------------------------------------------------------------- helpers

    private const string Accepted = "accepted";

    /// <summary>
    /// One OpenEntry attempt per (round, group, drawn competitor) coordinate in
    /// the whole phase, recorded as <see cref="Accepted"/> or the defect code.
    /// </summary>
    private static Dictionary<(int Round, GroupId Group, CompetitorId Competitor), string> Probe(Competition competition)
    {
        var probe = new Dictionary<(int, GroupId, CompetitorId), string>();

        foreach (var round in competition.Phases[0].Rounds)
        {
            var taskRound = round.TaskRounds[0];
            foreach (var group in taskRound.Groups)
            {
                foreach (var competitorRef in group.CompetitorRefs)
                {
                    var result = competition.OpenEntry(
                        EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, ReflightRole.Original, Now);

                    probe[(round.Ordinal, group.Id, competitorRef)] = result.IsSuccess ? Accepted : result.Code!;
                }
            }
        }

        return probe;
    }

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = [new MetricDefinition { Name = "alpha", Kind = MeasuredKind.Number }],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [new RateTerm { MetricRef = "alpha", Rate = 1 }],
    };

    private static ClassDefinition MakeClassDefinition() => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks = [MakeTask()],
            },
        ],
    };

    private static Competition BuildDrawnCompetition(int fieldSize, int rounds)
    {
        var classDefinition = MakeClassDefinition();
        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Closure Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now));

        for (var i = 0; i < fieldSize; i++)
        {
            competition = competition.Apply(competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now).Value);
        }

        // task.Group is null (whole-field, one group), so DrawPhase needs no
        // parameter binding — see Competition.DrawPhase's minPerGroup default.
        var drawn = competition.DrawPhase(rounds, ImmutableArray<string>.Empty, Now);
        drawn.IsSuccess.Should().BeTrue();

        return competition.Apply(drawn.Value);
    }
}
