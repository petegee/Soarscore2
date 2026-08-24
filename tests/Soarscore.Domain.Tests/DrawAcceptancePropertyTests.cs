using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property test P1 — the draw-lifecycle state machine
/// (kanban/in-progress/draw-acceptance-redraw.md WI-6):
/// for any generated sequence over the alphabet {register person, withdraw
/// competitor, draw, accept, reject} driven through the real decide functions
/// and folded via Apply: (a) Phases holds at most one phase; (b) DrawPhase
/// succeeds iff Phases is empty; (c) RegisterCompetitor succeeds iff no live
/// phase is "accepted"; (d) every successful draw partitions exactly the
/// eligible field — each non-withdrawn competitor appears exactly once per
/// round of the live phase; (e) reject always leaves Phases empty and the
/// next draw legal. A small mutable reference model tracks {registered,
/// withdrawn, live?, accepted?} in lockstep, the
/// CompetitionModelBasedFoldTests / CompetitionFieldPropertyTests shape.
///
/// The corpus class is Corpus.All[0] (F3K): its MinPerGroup is a literal (5),
/// so the model can predict draw success exactly — eligible ≥ 5 — rather than
/// having to resolve parameters; its preliminary is ChooseFromCatalogue with
/// distinct tasks per round, so generated draws name that many distinct codes
/// from the catalogue head. The person pool is deliberately small relative to
/// the attempt sequence and drawn with replacement, so duplicate-registration
/// and post-rejection redraw attempts are frequent rather than rare edges.
/// </summary>
public class DrawAcceptancePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition F3K = Corpus.All[0].Definition;

    // SeedF3K's Group.MinPerGroup literal (F3K.9.1).
    private const int MinPerGroupFloor = 5;

    private static readonly string[] CatalogueHead = ["A", "B", "C", "D", "E"];

    private sealed class Model
    {
        // Parallel to actual Competitors: append-only, same order.
        public List<bool> Withdrawn { get; } = [];

        public List<PersonId> Persons { get; } = [];

        // null = no live phase; otherwise "drawn" | "accepted".
        public string? LiveStatus { get; set; }

        public int Eligible => Withdrawn.Count(w => !w);
    }

    private enum DrawOp { Register, Withdraw, DrawPhase, Accept, Reject }

    private sealed record Op(DrawOp Kind, int Index, int Rounds, bool BlankReason);

    private static readonly Gen<Op> Operation =
        from kind in Gen.OneOfConst(DrawOp.Register, DrawOp.Withdraw, DrawOp.DrawPhase, DrawOp.Accept, DrawOp.Reject)
        from index in Gen.Int[0, 999]
        from rounds in Gen.Int[1, 3]
        from blank in Gen.Bool
        select new Op(kind, index, rounds, blank);

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
            CompetitionId.New(), "Draw Lifecycle Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        return Competition.Create(created);
    }

    [Fact]
    public void P1_the_draw_lifecycle_state_machine_holds_over_random_command_sequences()
    {
        (from initialField in Gen.Int[MinPerGroupFloor, MinPerGroupFloor + 4]
         from ops in Operation.Array[1, 40]
         select (initialField, ops))
        .Sample(t =>
        {
            // Eight persons over up-to-40 attempts keeps collisions frequent.
            var pool = Enumerable.Range(0, 8).Select(_ => PersonId.New()).ToArray();
            var competition = SampleCompetition();
            var model = new Model();

            foreach (var personRef in pool.Take(t.initialField))
            {
                competition = competition.Apply(
                    competition.RegisterCompetitor(CompetitorId.New(), personRef, Now).Value);
                model.Persons.Add(personRef);
                model.Withdrawn.Add(false);
            }

            foreach (var op in t.ops)
            {
                switch (op.Kind)
                {
                    case DrawOp.Register:
                    {
                        var personRef = pool[op.Index % pool.Length];
                        var modelAlreadyPresent = model.Persons.Contains(personRef);
                        var modelFrozen = model.LiveStatus == "accepted";

                        var result = competition.RegisterCompetitor(CompetitorId.New(), personRef, Now);

                        if (!modelAlreadyPresent && !modelFrozen)
                        {
                            // (c) in the success direction.
                            result.IsSuccess.Should().BeTrue(result.Code ?? "registration succeeded");
                            competition = competition.Apply(result.Value);
                            model.Persons.Add(personRef);
                            model.Withdrawn.Add(false);
                        }
                        else
                        {
                            result.IsFailure.Should().BeTrue();
                            // alreadyRegistered outranks field.frozen in the chain.
                            result.Code.Should().Be(modelAlreadyPresent
                                ? "competition.competitor.alreadyRegistered"
                                : "competition.field.frozen");
                        }

                        break;
                    }

                    case DrawOp.Withdraw:
                    {
                        if (model.Persons.Count == 0)
                        {
                            break;
                        }

                        var index = op.Index % competition.Competitors.Length;
                        var target = competition.Competitors[index];
                        var result = competition.WithdrawCompetitor(target.Id, Now);

                        if (model.Withdrawn[index])
                        {
                            // Withdrawal is not idempotent: re-withdrawing fails.
                            result.IsFailure.Should().BeTrue();
                            result.Code.Should().Be("competition.competitor.alreadyWithdrawn");
                        }
                        else
                        {
                            // Withdrawal stays ungated forever (D6) — always succeeds.
                            result.IsSuccess.Should().BeTrue(result.Code ?? "withdrawal succeeded");
                            competition = competition.Apply(result.Value);
                            model.Withdrawn[index] = true;
                        }

                        break;
                    }

                    case DrawOp.DrawPhase:
                    {
                        var modelCanDraw = model.LiveStatus is null && model.Eligible >= MinPerGroupFloor;
                        var taskRefs = CatalogueHead.Take(op.Rounds).ToImmutableArray();

                        var result = competition.DrawPhase(op.Rounds, taskRefs, Now);

                        if (modelCanDraw)
                        {
                            // (b) in the success direction.
                            result.IsSuccess.Should().BeTrue(result.Code ?? "draw succeeded");
                            result.Value.PhaseOrdinal.Should().Be(0);

                            // (d): the schedule partitions exactly the eligible field,
                            // once per round — this is what a later accept stands behind.
                            var eligibleNow = competition.Competitors
                                .Where(c => c.WithdrawnAt is null)
                                .Select(c => c.Id)
                                .ToImmutableArray();
                            foreach (var round in result.Value.Rounds)
                            {
                                var placed = round.TaskRounds[0].Groups
                                    .SelectMany(g => g.CompetitorRefs)
                                    .ToArray();
                                placed.Length.Should().Be(eligibleNow.Length);
                                placed.Distinct().Count().Should().Be(eligibleNow.Length);
                                placed.Should().BeSubsetOf(eligibleNow);
                            }

                            competition = competition.Apply(result.Value);
                            model.LiveStatus = "drawn";
                        }
                        else
                        {
                            result.IsFailure.Should().BeTrue();
                            result.Code.Should().Be(model.LiveStatus is not null
                                ? "drawPhase.alreadyDrawn"
                                : model.Eligible == 0 ? "drawPhase.fieldEmpty" : "drawPhase.fieldTooSmall");
                        }

                        break;
                    }

                    case DrawOp.Accept:
                    {
                        var result = competition.AcceptDraw(Now);

                        if (model.LiveStatus == "drawn")
                        {
                            result.IsSuccess.Should().BeTrue(result.Code ?? "accept succeeded");
                            result.Value.PhaseOrdinal.Should().Be(0);
                            competition = competition.Apply(result.Value);
                            model.LiveStatus = "accepted";
                        }
                        else
                        {
                            result.IsFailure.Should().BeTrue();
                            result.Code.Should().Be(model.LiveStatus is null
                                ? "acceptDraw.noDrawnPhase"
                                : "acceptDraw.alreadyAccepted");
                        }

                        break;
                    }

                    case DrawOp.Reject:
                    {
                        var reason = op.BlankReason ? "   " : "CD spotted a problem";
                        var result = competition.RejectDraw(phaseHasEntries: false, reason, Now);

                        if (model.LiveStatus is null)
                        {
                            result.IsFailure.Should().BeTrue();
                            result.Code.Should().Be("rejectDraw.noDrawnPhase");
                        }
                        else if (op.BlankReason)
                        {
                            result.IsFailure.Should().BeTrue();
                            result.Code.Should().Be("rejectDraw.reasonRequired");
                        }
                        else
                        {
                            // (e): rejection removes the live phase whatever its status.
                            result.IsSuccess.Should().BeTrue(result.Code ?? "reject succeeded");
                            result.Value.PhaseOrdinal.Should().Be(0);
                            competition = competition.Apply(result.Value);
                            model.LiveStatus = null;
                            competition.Phases.Should().BeEmpty();
                        }

                        break;
                    }
                }

                // (a): at most one live phase through every step of every sequence.
                competition.Phases.Length.Should().BeLessThanOrEqualTo(1);
            }

            // Final structural agreement between model and fold.
            competition.Phases.Length.Should().Be(model.LiveStatus is null ? 0 : 1);
            if (model.LiveStatus is not null)
            {
                competition.Phases.Single().Draw.Status.Should().Be(model.LiveStatus);
            }

            competition.Competitors.Length.Should().Be(model.Persons.Count);
            for (var i = 0; i < model.Persons.Count; i++)
            {
                competition.Competitors[i].PersonRef.Should().Be(model.Persons[i]);
                (competition.Competitors[i].WithdrawnAt is not null).Should().Be(model.Withdrawn[i]);
            }
        });
    }
}
