// kanban/completed/task-round-lifecycle.md WI-10, invariant A, stated there
// verbatim as:
//
//   **Invariant A — the validity gate counts completed rounds, never annulled
//   ones.** For any drawn phase of *n* rounds and any assignment of each round
//   to {left `Drawn`, `Complete`, `Annulled`}, `Competition.Finalise` succeeds
//   iff `count(rounds where every task-round is Complete) >= resolved
//   MinRounds` and the distinct-task count meets `MinTasks`. Generated over the
//   shape and the assignment, so no fixed fixture can hide a case. This is the
//   invariant that makes annulment-vs-completion meaningful rather than a
//   naming difference.
//
// A small synthetic class rather than a corpus one, following
// ScoringServicePropertyTests's precedent: the invariant is about the gate's
// arithmetic over PhaseDefinition.Validity, not about any one class's numbers,
// and only a synthetic definition lets MinRounds and MinTasks themselves be
// generated. The phase is ChooseFromCatalogue over two tasks so the
// distinct-task half of the gate has something to count.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class FinaliseValidityPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);

    private enum RoundOutcome { Drawn, Complete, Annulled }

    private static readonly Gen<(
        int rounds,
        string[] taskRefs,
        RoundOutcome[] outcomes,
        int minRounds,
        int? minTasks)> Scenario =
        from rounds in Gen.Int[1, 5]
        from taskRefs in Gen.OneOfConst("A", "B").Array[rounds]
        from outcomes in Gen.OneOfConst(RoundOutcome.Drawn, RoundOutcome.Complete, RoundOutcome.Annulled).Array[rounds]
        from minRounds in Gen.Int[1, 5]
        from minTasks in Gen.OneOfConst<int?>(null, 1, 2)
        select (rounds, taskRefs, outcomes, minRounds, minTasks);

    [Fact]
    public void Finalise_succeeds_exactly_when_fully_flown_rounds_meet_MinRounds_and_their_distinct_tasks_meet_MinTasks()
    {
        Scenario.Sample(t =>
        {
            var competition = BuildDrawnCompetition(t.rounds, [.. t.taskRefs], t.minRounds, t.minTasks);

            for (var i = 0; i < t.rounds; i++)
            {
                var roundOrdinal = i + 1;
                competition = t.outcomes[i] switch
                {
                    RoundOutcome.Complete => competition.Apply(new TaskRoundCompleted(0, roundOrdinal, 1, Now)),
                    RoundOutcome.Annulled => competition.Apply(new TaskRoundAnnulled(0, roundOrdinal, 1, "test", Now)),
                    _ => competition,
                };
            }

            // The oracle, written straight from the invariant's own wording —
            // rounds where every task-round is Complete, and the distinct tasks
            // across exactly those rounds. Annulled rounds contribute to
            // neither count.
            var flownIndices = Enumerable.Range(0, t.rounds)
                .Where(i => t.outcomes[i] == RoundOutcome.Complete)
                .ToImmutableArray();
            var flownCount = flownIndices.Length;
            var distinctTasks = flownIndices.Select(i => t.taskRefs[i]).Distinct().Count();

            var enoughRounds = flownCount >= t.minRounds;
            var enoughTasks = t.minTasks is not { } minTasks || distinctTasks >= minTasks;

            var result = competition.Finalise([SampleDeclaredResult()], [], "CD", Now);

            result.IsSuccess.Should().Be(enoughRounds && enoughTasks);

            if (!enoughRounds)
            {
                result.Code.Should().Be("finalise.notEnoughRounds");
            }
            else if (!enoughTasks)
            {
                result.Code.Should().Be("finalise.notEnoughTasks");
            }
        });
    }

    /// <summary>
    /// The half of the invariant that makes annulment mean something: an
    /// annulled round is not merely renamed, it is subtracted. Every round
    /// Complete finalises; the same shape with one round annulled instead
    /// must not, when MinRounds is the full count.
    /// </summary>
    [Fact]
    public void Annulling_any_one_of_a_just_sufficient_set_of_completed_rounds_always_breaks_the_gate()
    {
        (from rounds in Gen.Int[1, 5]
         from annulled in Gen.Int[1, rounds]
         select (rounds, annulled))
        .Sample(t =>
        {
            // MinRounds == rounds, and a distinct task per round so MinTasks
            // never masks the round count. taskRefs cycles A/B, which is all
            // MinTasks 2 can ask for.
            var taskRefs = Enumerable.Range(0, t.rounds).Select(i => i % 2 == 0 ? "A" : "B").ToImmutableArray();
            var drawn = BuildDrawnCompetition(t.rounds, taskRefs, minRounds: t.rounds, minTasks: null);

            var allComplete = Enumerable.Range(1, t.rounds)
                .Aggregate(drawn, (c, r) => c.Apply(new TaskRoundCompleted(0, r, 1, Now)));

            allComplete.Finalise([SampleDeclaredResult()], [], "CD", Now).IsSuccess.Should().BeTrue();

            var oneAnnulled = allComplete.Apply(new TaskRoundAnnulled(0, t.annulled, 1, "test", Now));

            var result = oneAnnulled.Finalise([SampleDeclaredResult()], [], "CD", Now);
            result.IsFailure.Should().BeTrue();
            result.Code.Should().Be("finalise.notEnoughRounds");
        });
    }

    // ------------------------------------------------------------- helpers

    private static DeclaredResult SampleDeclaredResult() => new()
    {
        CompetitorRef = CompetitorId.New(),
        Aggregate = 1000m,
        Placing = 1,
        Promoted = false,
    };

    private static TaskDefinition MakeTask(string code) => new()
    {
        Code = code,
        Name = $"Task {code}",
        Metrics = [new MetricDefinition { Name = "alpha", Kind = MeasuredKind.Number }],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [new RateTerm { MetricRef = "alpha", Rate = 1 }],
    };

    private static ClassDefinition MakeClassDefinition(int minRounds, int? minTasks) => new()
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
                Rounds = new RoundComposition { Kind = CompositionKind.ChooseFromCatalogue, TasksPerRound = 1 },
                Validity = new ValidityRule { MinRounds = minRounds, MinTasks = minTasks },
                Tasks = [MakeTask("A"), MakeTask("B")],
            },
        ],
    };

    private static Competition BuildDrawnCompetition(
        int rounds, ImmutableArray<string> taskRefs, int minRounds, int? minTasks)
    {
        var classDefinition = MakeClassDefinition(minRounds, minTasks);
        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Validity Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now));

        // Two competitors, one whole-field group per round: the task declares
        // no GroupConstraint, so DrawPhase needs no parameter binding.
        for (var i = 0; i < 2; i++)
        {
            competition = competition.Apply(competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now).Value);
        }

        var drawn = competition.DrawPhase(rounds, taskRefs, Now);
        drawn.IsSuccess.Should().BeTrue();

        return competition.Apply(drawn.Value);
    }
}
