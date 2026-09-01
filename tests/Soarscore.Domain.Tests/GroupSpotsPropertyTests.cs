// kanban/in-progress/lane-assignment.md WI-6, invariant P1, stated there
// verbatim as:
//
//   **P1 — a spot assignment is an explicit bijection over the live group,
//   replaced whole, and dies with the draw.** For any generated competition
//   (field, drawn phase, withdrawals) and any generated sequence of
//   AssignGroupSpots attempts driven through the real decide functions and
//   folded via Apply: (a) after every successful attempt, Group.Spots is
//   exactly a bijection between the group's live members (drawn ∧ ¬withdrawn)
//   and a set of distinct positive integers; (b) every defective attempt —
//   unknown/withdrawn competitor, repeated competitor, repeated spot, spot < 1,
//   missing live member, empty list, unknown coordinate, annulled round — fails
//   with its stable code and leaves the fold unchanged; (c) a second success
//   replaces the first in its entirety; (d) DrawRejected removes the phase and
//   every assignment on it, and the redraw's groups start unassigned; (e)
//   RecordingCore projects the assignment verbatim, spot-ordered. Small
//   reference model tracks {drawn, withdrawn, assigned?} in lockstep — the
//   CompetitionFieldPropertyTests shape, in its own file citing P1.
//
// Item (e) is implemented below (P1_e_…): it drives WI-5's read view
// (RecordingCore, internal to Soarscore.Application — that project grants
// Soarscore.Domain.Tests InternalsVisibleTo for exactly this drive, and this
// test project references the Application assembly), asserting the
// projection is the recorded assignment verbatim, spot-ordered, withdrawn
// competitors still listed (vacancy is the consumer's derivation against
// ExpectedCompetitorRefs), and an unassigned group projecting an empty
// array — a fact, not an error.
//
// Mutation-check non-vacuity (the task-round-lifecycle.md WI-10 discipline):
// removing the decide function's spotDuplicated check makes the RepeatedSpot
// defective attempt succeed and (a)'s distinctness clause fail the property;
// making the fold merge instead of replace (Spots = g.Spots.AddRange(...))
// makes (c)'s model agreement fail the property. Both mutations were run and
// reverted. The_bijection_oracle_rejects_defective_mappings additionally
// stands as the in-repo guard that (a)'s oracle itself can fail.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Queries.Scoring;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class GroupSpotsPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition F3K = Corpus.All[0].Definition;

    // SeedF3K's Group.MinPerGroup literal (F3K.9.1) — the model can predict
    // draw success exactly (eligible ≥ 5), and field 5..9 over that minimum
    // yields exactly one group per round.
    private const int MinPerGroupFloor = 5;

    private static readonly string[] CatalogueHead = ["A", "B", "C"];

    private enum OpKind
    {
        Valid,
        UnknownCompetitor,
        WithdrawnCompetitor,
        RepeatedCompetitor,
        RepeatedSpot,
        InvalidSpot,
        MissingMember,
        EmptyList,
        UnknownGroup,
        UnknownTaskRound,
        AnnulRound,
    }

    private sealed record Op(OpKind Kind, int TargetRound, int Index, int SpotBase, int[] Perm);

    private static Gen<Op> GenOp(int rounds, int fieldSize) =>
        from kind in Gen.OneOfConst(Enum.GetValues<OpKind>())
        from targetRound in Gen.Int[1, rounds]
        from index in Gen.Int[0, 999]
        from spotBase in Gen.Int[1, 3]
        from perm in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        select new Op(kind, targetRound, index, spotBase, perm);

    private static readonly Gen<(int FieldSize, int Rounds, int[] WithdrawnIndices, Op[] Ops)> Scenario =
        from fieldSize in Gen.Int[MinPerGroupFloor, MinPerGroupFloor + 4]
        from rounds in Gen.Int[1, 3]
        from withdrawalCount in Gen.Int[0, fieldSize - MinPerGroupFloor]
        from order in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        from ops in GenOp(rounds, fieldSize).Array[1, 12]
        select (fieldSize, rounds, order.Take(withdrawalCount).ToArray(), ops);

    private sealed class Model
    {
        public List<CompetitorId> Drawn { get; } = [];

        public HashSet<CompetitorId> Withdrawn { get; } = [];

        public Dictionary<int, GroupId> Groups { get; } = [];

        // Round ordinal -> the assignment exactly as commanded (absent = never
        // assigned). Withdrawal never touches it — vacancy is read-side (D4).
        public Dictionary<int, List<GroupSpot>> Assigned { get; } = [];

        public List<CompetitorId> Live => Drawn.Where(d => !Withdrawn.Contains(d)).ToList();
    }

    private static Competition SampleCompetition()
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = F3K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3K.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Group Spots Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        return Competition.Create(created);
    }

    private static Group GroupOf(Competition competition, int roundOrdinal, GroupId groupRef) =>
        competition.Phases.Single()
            .Rounds.Single(r => r.Ordinal == roundOrdinal)
            .TaskRounds[0]
            .Groups.Single(g => g.Id == groupRef);

    /// <summary>
    /// (a)'s oracle: <paramref name="spots"/> is exactly a bijection between
    /// the live members and distinct positive integers. Extracted so the
    /// non-vacuity test can prove the oracle can fail.
    /// </summary>
    private static bool IsBijection(IReadOnlyList<GroupSpot> spots, IReadOnlyCollection<CompetitorId> live) =>
        spots.Count == live.Count
        && spots.Select(s => s.CompetitorRef).Distinct().Count() == spots.Count
        && spots.All(s => s.Spot >= 1)
        && spots.Select(s => s.Spot).Distinct().Count() == spots.Count
        && live.All(member => spots.Any(s => s.CompetitorRef == member));

    /// <summary>A full would-be-valid mapping over the live members, as-given order scrambled by the permutation, distinct positive (SpotBase-offset, so sometimes non-contiguous) spots.</summary>
    private static List<GroupSpot> Mapping(List<CompetitorId> live, int[] perm, int spotBase) =>
        perm.Where(i => i < live.Count)
            .Select((liveIndex, position) => new GroupSpot(live[liveIndex], spotBase + position))
            .ToList();

    [Fact]
    public void P1_a_spot_assignment_is_a_whole_replaced_bijection_that_dies_with_the_draw()
    {
        Scenario.Sample(t =>
        {
            var competition = SampleCompetition();
            var model = new Model();

            foreach (var personRef in Enumerable.Range(0, t.FieldSize).Select(_ => PersonId.New()))
            {
                competition = competition.Apply(
                    competition.RegisterCompetitor(CompetitorId.New(), personRef, Now).Value);
            }

            var taskRefs = CatalogueHead.Take(t.Rounds).ToImmutableArray();
            var drawn = competition.DrawPhase(t.Rounds, taskRefs, Now);
            drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "draw succeeded");
            competition = competition.Apply(drawn.Value);

            var rounds = competition.Phases[0].Rounds;
            rounds.Length.Should().Be(t.Rounds);
            foreach (var round in rounds)
            {
                // field ≤ 9 with F3K's minPerGroup 5 ⇒ exactly one group per round.
                round.TaskRounds.Length.Should().Be(1);
                round.TaskRounds[0].Groups.Length.Should().Be(1);

                var group = round.TaskRounds[0].Groups[0];
                model.Groups[round.Ordinal] = group.Id;

                if (round.Ordinal == 1)
                {
                    model.Drawn.AddRange(group.CompetitorRefs);
                }
                else
                {
                    // Every round partitions the same whole field — this is
                    // what makes the model's per-round live set one shared set.
                    group.CompetitorRefs.Should().BeEquivalentTo(model.Drawn);
                }
            }

            // Withdrawals are part of the generated competition (before the
            // assign sequence), capped so the (d) redraw always stays legal.
            foreach (var index in t.WithdrawnIndices)
            {
                var target = competition.Competitors[index];
                competition = competition.Apply(competition.WithdrawCompetitor(target.Id, Now).Value);
                model.Withdrawn.Add(target.Id);
            }

            foreach (var op in t.Ops)
            {
                var groupRef = model.Groups[op.TargetRound];
                var live = model.Live;
                var before = competition;

                switch (op.Kind)
                {
                    case OpKind.Valid:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        result.IsSuccess.Should().BeTrue(result.Code ?? "assignment succeeded");
                        competition = competition.Apply(result.Value);

                        var assigned = GroupOf(competition, op.TargetRound, groupRef);

                        // (a) after every success: exactly a bijection between
                        // the live members and distinct positive integers.
                        IsBijection(assigned.Spots, live).Should().BeTrue();

                        // (c)'s mechanics: the fold is the new list verbatim —
                        // as given, no reordering, nothing of the first left.
                        assigned.Spots.ToArray().Should().Equal(commanded);

                        model.Assigned[op.TargetRound] = [.. commanded];
                        break;
                    }

                    case OpKind.UnknownCompetitor:
                    case OpKind.WithdrawnCompetitor:
                    {
                        // Same defect, different provenance: neither id is a
                        // live member. Withdrawn falls back to unknown when the
                        // generated competition has no withdrawal to point at.
                        var competitorRef = op.Kind == OpKind.WithdrawnCompetitor && model.Withdrawn.Count > 0
                            ? model.Withdrawn.ElementAt(op.Index % model.Withdrawn.Count)
                            : CompetitorId.New();

                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        commanded[op.Index % commanded.Count] =
                            commanded[op.Index % commanded.Count] with { CompetitorRef = competitorRef };

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.competitorNotInGroup");
                        break;
                    }

                    case OpKind.RepeatedCompetitor:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        var last = commanded.Count - 1;
                        commanded[last] = commanded[last] with { CompetitorRef = commanded[0].CompetitorRef };

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.competitorRepeated");
                        break;
                    }

                    case OpKind.RepeatedSpot:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        var last = commanded.Count - 1;
                        commanded[last] = commanded[last] with { Spot = commanded[0].Spot };

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.spotDuplicated");
                        break;
                    }

                    case OpKind.InvalidSpot:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        commanded[0] = commanded[0] with { Spot = -1 - (op.Index % 3) };

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.spotInvalid");
                        break;
                    }

                    case OpKind.MissingMember:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        commanded.RemoveAt(commanded.Count - 1);

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.memberMissing");
                        break;
                    }

                    case OpKind.EmptyList:
                    {
                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, [], Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.assignmentsEmpty");
                        break;
                    }

                    case OpKind.UnknownGroup:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, GroupId.New(), commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.groupNotFound");
                        break;
                    }

                    case OpKind.UnknownTaskRound:
                    {
                        var commanded = Mapping(live, op.Perm, op.SpotBase);

                        var result = competition.AssignGroupSpots(0, op.TargetRound, 99, groupRef, commanded, Now);

                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.taskRoundNotFound");
                        break;
                    }

                    case OpKind.AnnulRound:
                    {
                        competition = competition.Apply(
                            new TaskRoundAnnulled(0, op.TargetRound, 1, "found faulty", Now));

                        var commanded = Mapping(live, op.Perm, op.SpotBase);
                        var result = competition.AssignGroupSpots(0, op.TargetRound, 1, groupRef, commanded, Now);

                        // (b): the annulled round refuses with its stable code.
                        result.IsFailure.Should().BeTrue();
                        result.Code.Should().Be("assignSpots.taskRoundAnnulled");

                        // Reopen so later ops see a live round again — the
                        // surviving assignments (the annul/reopen cycle never
                        // touches Spots) are asserted by the lockstep below.
                        competition = competition.Apply(
                            new TaskRoundReopened(0, op.TargetRound, 1, "annulled in error", Now));
                        break;
                    }
                }

                if (op.Kind is not (OpKind.Valid or OpKind.AnnulRound))
                {
                    // (b): the fold is untouched — the reference is the same
                    // instance (nothing was folded) and the model still agrees.
                    ReferenceEquals(competition, before).Should().BeTrue();
                }

                AssertModelAgreement(competition, model);
            }

            // (d): rejection removes the phase and every assignment on it, and
            // the redraw's groups start unassigned. Eligible ≥ F3K's floor is
            // guaranteed by the withdrawal cap.
            var rejected = competition.RejectDraw(phaseHasEntries: false, "CD rejected the draw", Now);
            rejected.IsSuccess.Should().BeTrue(rejected.Code ?? "reject succeeded");
            competition = competition.Apply(rejected.Value);
            competition.Phases.Should().BeEmpty();

            var redrawn = competition.DrawPhase(t.Rounds, taskRefs, Now);
            redrawn.IsSuccess.Should().BeTrue(redrawn.Code ?? "redraw succeeded");
            competition = competition.Apply(redrawn.Value);

            foreach (var round in competition.Phases[0].Rounds)
            {
                foreach (var group in round.TaskRounds[0].Groups)
                {
                    group.Spots.IsEmpty.Should().BeTrue();
                    group.CompetitorRefs.Should().BeSubsetOf(model.Live);
                }
            }
        });
    }

    // P1 item (e) — lane-assignment.md WI-5/WI-6. RecordingCore (Application,
    // internal, driven through the InternalsVisibleTo granted for exactly
    // this) projects the recorded assignment verbatim, spot-ordered: the fold
    // stores as given, the view sorts. A competitor withdrawn after
    // assignment is still listed — vacancy is the consumer's derivation
    // against ExpectedCompetitorRefs — and a never-assigned group projects an
    // empty array, the unassigned fact, not an error and not a default (D2).

    private static readonly Gen<(int FieldSize, int[] Perm, int SpotBase, bool WithdrawAfterAssign)> SpotViewScenario =
        from fieldSize in Gen.Int[MinPerGroupFloor, MinPerGroupFloor + 4]
        from perm in Gen.Shuffle(Enumerable.Range(0, fieldSize).ToArray())
        from spotBase in Gen.Int[1, 3]
        from withdrawAfterAssign in Gen.Bool
        select (fieldSize, perm, spotBase, withdrawAfterAssign);

    [Fact]
    public void P1_e_RecordingCore_projects_the_assignment_verbatim_spot_ordered()
    {
        SpotViewScenario.Sample(t =>
        {
            var competition = SampleCompetition();

            foreach (var personRef in Enumerable.Range(0, t.FieldSize).Select(_ => PersonId.New()))
            {
                competition = competition.Apply(
                    competition.RegisterCompetitor(CompetitorId.New(), personRef, Now).Value);
            }

            // Two rounds: round 1 gets the assignment, round 2 stays
            // unassigned — the empty-array fact asserted beside the
            // projection, not as a separate example.
            var drawn = competition.DrawPhase(2, ["A", "B"], Now);
            drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "draw succeeded");
            competition = competition.Apply(drawn.Value);

            var live = competition.Phases[0].Rounds[0].TaskRounds[0].Groups[0].CompetitorRefs;
            var groupRef = competition.Phases[0].Rounds[0].TaskRounds[0].Groups[0].Id;
            var commanded = Mapping([.. live], t.Perm, t.SpotBase);

            var assigned = competition.AssignGroupSpots(0, 1, 1, groupRef, commanded, Now);
            assigned.IsSuccess.Should().BeTrue(assigned.Code ?? "assignment succeeded");
            competition = competition.Apply(assigned.Value);

            var vacated = (CompetitorId?)null;
            if (t.WithdrawAfterAssign)
            {
                // Withdrawal after assignment — the spot stays recorded and
                // the view lists it; vacancy is the consumer's derivation.
                vacated = commanded[0].CompetitorRef;
                competition = competition.Apply(competition.WithdrawCompetitor(vacated.Value, Now).Value);
            }

            var declaredMetrics = competition.AdoptedRules.Definition.Phases
                .SelectMany(p => p.Tasks)
                .First(task => task.Code == "A")
                .Metrics
                .Select(m => m.Name)
                .ToImmutableArray();

            var view = RecordingCore.ComputeGroupViews(
                competition, 0, 1, 1,
                competition.Phases[0].Rounds.SelectMany(r => r.TaskRounds[0].Groups).ToImmutableArray(),
                new Dictionary<EntryId, Entry>(),
                declaredMetrics);

            view.Length.Should().Be(2);
            view[0].GroupRef.Should().Be(assigned.Value.GroupRef);
            view[0].Spots.ToArray().Should().Equal(
                [.. commanded.OrderBy(s => s.Spot).Select(s => new GroupSpotView(s.Spot, s.CompetitorRef))]);
            view[0].Spots.Select(s => s.Spot).Should().BeInAscendingOrder();

            if (vacated is { } vacancy)
            {
                // The withdrawn competitor's spot is still listed — the
                // consumer derives its vacancy against Expected, which no
                // longer names them.
                view[0].Spots.Should().Contain(s => s.CompetitorRef == vacancy);
                view[0].ExpectedCompetitorRefs.Should().NotContain(vacancy);
            }

            // (e)'s empty-fact half: the never-assigned round projects an
            // empty array — a fact, not an error and not a default (D2).
            view[1].Spots.Should().BeEmpty();
        });
    }

    private static void AssertModelAgreement(Competition competition, Model model)
    {
        foreach (var (roundOrdinal, groupRef) in model.Groups)
        {
            var group = GroupOf(competition, roundOrdinal, groupRef);
            var expected = model.Assigned.TryGetValue(roundOrdinal, out var assigned) ? assigned : [];
            group.Spots.ToArray().Should().Equal(expected);
        }
    }

    // Mutation-check non-vacuity, standing form: (a)'s oracle is not
    // vacuously true — each deliberately defective shape below fails it, so
    // the property's bijection assertion has teeth.

    [Fact]
    public void The_bijection_oracle_rejects_defective_mappings()
    {
        var a = CompetitorId.New();
        var b = CompetitorId.New();
        var c = CompetitorId.New();

        // Repeated spot — the mutation (a) exists to catch.
        IsBijection([new GroupSpot(a, 1), new GroupSpot(b, 1)], [a, b]).Should().BeFalse();

        // Non-positive spot.
        IsBijection([new GroupSpot(a, 0)], [a]).Should().BeFalse();

        // A live member missing (coverage hole).
        IsBijection([new GroupSpot(a, 1), new GroupSpot(b, 2)], [a, b, c]).Should().BeFalse();

        // Repeated competitor.
        IsBijection([new GroupSpot(a, 1), new GroupSpot(a, 2)], [a]).Should().BeFalse();

        // The genuine article passes.
        IsBijection([new GroupSpot(a, 2), new GroupSpot(b, 1)], [a, b]).Should().BeTrue();
    }
}
