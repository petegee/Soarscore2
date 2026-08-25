// Property tests for Competition.PrescribeDraw —
// kanban/in-progress/prescribed-draw-import.md WI-2. CsCheck, in
// DrawAcceptancePropertyTests's style, driven on Corpus.All[0] (F3K): its
// MinPerGroup is a literal (5), so resolved minima are known without binding
// parameters, and its preliminary is ChooseFromCatalogue with
// RequireDistinctTaskPerRound.
//
// PD-P1 — generation/prescription self-consistency. For any generated field
// and round count on which PhaseDraw.BuildGroups succeeds, feeding its output
// back through PrescribeDraw succeeds, and the two events fold to schedules
// equal up to GroupId minting. Invariant: EVERY DRAW THE SYSTEM CAN GENERATE
// IS A LEGAL PRESCRIPTION — this guards the validation set drifting stricter
// than what generation guarantees (the exact-once partition, the >=2 floor and
// the minPerGroup floor are precisely what generation promises — nothing more
// may be required).
//
// PD-P2 — partition enforcement. For any valid prescription, mutating it —
// deleting a competitor from a group, duplicating one across groups of a
// round, swapping in an unregistered or withdrawn id, shrinking a group below
// 2 or below the resolved minimum — is rejected with the corresponding code
// from WI-1's table. Invariant: NOTHING ENTERS THE LOG THAT VIOLATES THE
// DRAWN-ALLOCATION INVARIANTS. Non-vacuity (task-round-lifecycle WI-10
// discipline): the mutation-check test pins each check to its code directly,
// so weakening any single check in the decide fails the suite instead of
// silently re-routing which defect answers.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class PrescribeDrawPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition F3K = Corpus.All[0].Definition;

    // SeedF3K's Group.MinPerGroup literal (F3K.9.1).
    private const int MinPerGroupFloor = 5;

    private static readonly string[] CatalogueHead = ["A", "B", "C"];

    private enum Mutation
    {
        DeleteMember,          // breaks exact-once coverage      -> competitorMissing
        DuplicateMember,       // breaks exact-once uniqueness    -> competitorRepeated
        SubstituteUnregistered,// grouped id is not eligible      -> competitorNotInField
        WithdrawAMember,       // grouped id is no longer eligible-> competitorNotInField
        SplitOffSingleton,     // group below the 2 floor         -> groupTooSmall
        MoveBetweenGroups,     // group below the resolved minimum-> groupBelowClassMinimum
    }

    // ------------------------------------------------------------- fixtures

    private static Competition SampleCompetition(ClassDefinition definition)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Prescription Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        return Competition.Create(created);
    }

    private static Competition Registered(ClassDefinition definition, IReadOnlyList<CompetitorId> ids)
    {
        var competition = SampleCompetition(definition);

        foreach (var id in ids)
        {
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
        }

        return competition;
    }

    /// <summary>The generated event's schedule, verbatim, as prescription input.</summary>
    private static IReadOnlyList<PrescribedRound> FeedBack(PhaseDrawn drawn) =>
        [.. drawn.Rounds.Select(round => new PrescribedRound(
            round.TaskRounds[0].TaskRef,
            [.. round.TaskRounds[0].Groups.Select(g => new PrescribedGroup([.. g.CompetitorRefs]))]))];

    private static string ExpectedCode(Mutation mutation) => mutation switch
    {
        Mutation.DeleteMember => "prescribeDraw.competitorMissing",
        Mutation.DuplicateMember => "prescribeDraw.competitorRepeated",
        Mutation.SubstituteUnregistered => "prescribeDraw.competitorNotInField",
        Mutation.WithdrawAMember => "prescribeDraw.competitorNotInField",
        Mutation.SplitOffSingleton => "prescribeDraw.groupTooSmall",
        _ => "prescribeDraw.groupBelowClassMinimum",
    };

    /// <summary>
    /// The generated schedule as mutable lists plus the mutation applied to one
    /// round (chosen by <paramref name="seed"/>). Returns the prescription input
    /// and, for WithdrawAMember, the id to withdraw before prescribing.
    /// </summary>
    private static (IReadOnlyList<PrescribedRound> Rounds, CompetitorId? WithdrawnId) Mutate(
        PhaseDrawn drawn, Mutation mutation, int seed)
    {
        var groups = drawn.Rounds
            .Select(r => r.TaskRounds[0].Groups.Select(g => g.CompetitorRefs.ToList()).ToList())
            .ToList();

        var ri = seed % drawn.Rounds.Length;
        var round = groups[ri];
        var gi = (seed / drawn.Rounds.Length) % round.Count;
        var mi = (seed * 7 + gi) % round[gi].Count;

        CompetitorId? withdrawnId = null;

        switch (mutation)
        {
            case Mutation.DeleteMember:
                round[gi].RemoveAt(mi);
                break;

            case Mutation.DuplicateMember:
                round[(gi + 1) % round.Count].Add(round[gi][mi]);
                break;

            case Mutation.SubstituteUnregistered:
                round[gi][mi] = CompetitorId.New();
                break;

            case Mutation.WithdrawAMember:
                withdrawnId = round[gi][mi];
                break;

            case Mutation.SplitOffSingleton:
                // Singleton goes FIRST so the size checks meet it before any
                // below-minimum donor — the code under test is groupTooSmall.
                var loner = round[gi][mi];
                round[gi].RemoveAt(mi);
                round.Insert(0, [loner]);
                break;

            case Mutation.MoveBetweenGroups:
                // Generated groups may exceed the minimum (e.g. two groups of
                // six against a minimum of five), so a single moved member
                // does not necessarily breach it. Shrinking the round's
                // smallest group to exactly the 2-member floor — overflow
                // moved to another group, keeping the partition exact — lands
                // below any resolved minimum BuildGroups honours.
                var donorIndex = 0;
                for (var candidate = 1; candidate < round.Count; candidate++)
                {
                    if (round[candidate].Count < round[donorIndex].Count)
                    {
                        donorIndex = candidate;
                    }
                }

                var receiver = (donorIndex + 1) % round.Count;
                while (round[donorIndex].Count > 2)
                {
                    var moved = round[donorIndex][^1];
                    round[donorIndex].RemoveAt(round[donorIndex].Count - 1);
                    round[receiver].Add(moved);
                }

                break;
        }

        IReadOnlyList<PrescribedRound> rounds = [.. groups.Select((memberLists, i) => new PrescribedRound(
            drawn.Rounds[i].TaskRounds[0].TaskRef,
            [.. memberLists.Select(members => new PrescribedGroup([.. members]))]))];

        return (rounds, withdrawnId);
    }

    // --------------------------------------------------------------- PD-P1

    [Fact]
    public void P1_every_generated_draw_is_a_legal_prescription_and_folds_to_the_same_schedule_up_to_GroupId_minting()
    {
        (from fieldSize in Gen.Int[MinPerGroupFloor, MinPerGroupFloor + 5]
         from rounds in Gen.Int[1, 3]
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var taskRefs = CatalogueHead.Take(t.rounds).ToImmutableArray();
            var ids = Enumerable.Range(0, t.fieldSize).Select(_ => CompetitorId.New()).ToArray();

            var source = Registered(F3K, ids);
            var drawn = source.DrawPhase(t.rounds, taskRefs, Now);
            drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "generation succeeded");

            var target = Registered(F3K, ids);
            var result = target.PrescribeDraw(FeedBack(drawn.Value), "property", Now);

            // PD-P1: every draw the system can generate is a legal prescription.
            result.IsSuccess.Should().BeTrue(result.Code ?? "prescribing the generated draw succeeded");

            // ...and the two events fold to schedules equal up to GroupId minting.
            var generatedFolded = source.Apply(drawn.Value);
            var prescribedFolded = target.Apply(result.Value);

            result.Value.PhaseOrdinal.Should().Be(drawn.Value.PhaseOrdinal);
            result.Value.Type.Should().Be(drawn.Value.Type);
            drawn.Value.PrescribedBy.Should().BeNull();
            result.Value.PrescribedBy.Should().Be("property"); // provenance is the only difference

            var generatedPhase = generatedFolded.Phases.Single();
            var prescribedPhase = prescribedFolded.Phases.Single();
            generatedPhase.Ordinal.Should().Be(prescribedPhase.Ordinal);
            generatedPhase.Draw.Status.Should().Be(prescribedPhase.Draw.Status);
            generatedPhase.Draw.CreatedAt.Should().Be(prescribedPhase.Draw.CreatedAt);

            generatedPhase.Rounds.Length.Should().Be(t.rounds);
            for (var r = 0; r < t.rounds; r++)
            {
                var generatedTaskRound = generatedPhase.Rounds[r].TaskRounds[0];
                var prescribedTaskRound = prescribedPhase.Rounds[r].TaskRounds[0];

                generatedTaskRound.Ordinal.Should().Be(prescribedTaskRound.Ordinal);
                generatedTaskRound.TaskRef.Should().Be(prescribedTaskRound.TaskRef);
                generatedTaskRound.State.Should().Be(prescribedTaskRound.State);

                generatedTaskRound.Groups.Length.Should().Be(prescribedTaskRound.Groups.Length);
                for (var g = 0; g < generatedTaskRound.Groups.Length; g++)
                {
                    prescribedTaskRound.Groups[g].Ordinal.Should().Be(generatedTaskRound.Groups[g].Ordinal);
                    prescribedTaskRound.Groups[g].CompetitorRefs.Should().Equal(generatedTaskRound.Groups[g].CompetitorRefs);
                }
            }
        });
    }

    // --------------------------------------------------------------- PD-P2

    [Fact]
    public void P2_every_mutated_prescription_is_rejected_with_the_code_of_the_invariant_it_breaks()
    {
        // Fields start at 10: against MinPerGroup 5 that guarantees BuildGroups
        // produces at least two groups, which MoveBetweenGroups needs.
        (from fieldSize in Gen.Int[10, 16]
         from rounds in Gen.Int[1, 3]
         from mutation in Gen.OneOfConst(
             Mutation.DeleteMember,
             Mutation.DuplicateMember,
             Mutation.SubstituteUnregistered,
             Mutation.WithdrawAMember,
             Mutation.SplitOffSingleton,
             Mutation.MoveBetweenGroups)
         from seed in Gen.Int[0, 999]
         select (fieldSize, rounds, mutation, seed))
        .Sample(t =>
        {
            var taskRefs = CatalogueHead.Take(t.rounds).ToImmutableArray();
            var ids = Enumerable.Range(0, t.fieldSize).Select(_ => CompetitorId.New()).ToArray();

            var source = Registered(F3K, ids);
            var drawn = source.DrawPhase(t.rounds, taskRefs, Now);
            drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "generation succeeded");

            // Non-vacuity: the unmutated feedback must be legal, or rejecting
            // a mutant proves nothing.
            var pristine = Registered(F3K, ids);
            pristine.PrescribeDraw(FeedBack(drawn.Value), "property", Now).IsSuccess
                .Should().BeTrue("the base prescription must be legal before mutating it");

            var (mutated, withdrawnId) = Mutate(drawn.Value, t.mutation, t.seed);

            var target = Registered(F3K, ids);
            if (withdrawnId is { } victim)
            {
                var withdrawal = target.WithdrawCompetitor(victim, Now);
                withdrawal.IsSuccess.Should().BeTrue(withdrawal.Code ?? "withdrawal succeeded");
                target = target.Apply(withdrawal.Value);
            }

            var result = target.PrescribeDraw(mutated, "property", Now);

            // PD-P2: nothing enters the log that violates the drawn-allocation
            // invariants — and the rejection names the invariant that broke.
            result.IsFailure.Should().BeTrue($"mutation {t.mutation} must be rejected");
            result.Code.Should().Be(ExpectedCode(t.mutation));
        });
    }

    /// <summary>
    /// Mutation-check non-vacuity: each mutation kind, run deterministically
    /// against one valid prescription, is rejected with its own code — and the
    /// six kinds cover exactly the five partition codes. Weakening or removing
    /// any single check in PrescribeDraw flips its mutation from reject to
    /// accept here, so PD-P2 cannot pass while a check is missing.
    /// </summary>
    [Fact]
    public void Every_mutation_kind_yields_its_own_distinct_code_the_PD_P2_non_vacuity_check()
    {
        const int FieldSize = 12;
        var taskRefs = ImmutableArray.Create("A", "B");
        var ids = Enumerable.Range(0, FieldSize).Select(_ => CompetitorId.New()).ToArray();

        var source = Registered(F3K, ids);
        var drawn = source.DrawPhase(2, taskRefs, Now);
        drawn.IsSuccess.Should().BeTrue();

        var observed = new List<string>();

        foreach (var mutation in Enum.GetValues<Mutation>())
        {
            var (mutated, withdrawnId) = Mutate(drawn.Value, mutation, seed: ((int)mutation + 1) * 13);

            var target = Registered(F3K, ids);
            if (withdrawnId is { } victim)
            {
                var withdrawal = target.WithdrawCompetitor(victim, Now);
                withdrawal.IsSuccess.Should().BeTrue(withdrawal.Code ?? "withdrawal succeeded");
                target = target.Apply(withdrawal.Value);
            }

            var result = target.PrescribeDraw(mutated, "property", Now);

            result.IsFailure.Should().BeTrue($"{mutation} must be rejected");
            result.Code.Should().Be(ExpectedCode(mutation));
            observed.Add(result.Code!);
        }

        observed.Distinct().Should().Equal(
            "prescribeDraw.competitorMissing",
            "prescribeDraw.competitorRepeated",
            "prescribeDraw.competitorNotInField",
            "prescribeDraw.groupTooSmall",
            "prescribeDraw.groupBelowClassMinimum");
    }
}
