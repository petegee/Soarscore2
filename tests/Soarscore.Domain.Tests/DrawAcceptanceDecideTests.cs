using System.Linq;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.AcceptDraw"/> and
/// <see cref="Competition.RejectDraw"/> — kanban/in-progress/draw-acceptance-redraw.md
/// WI-6. Mirrors PhaseDrawnDecideTests's style: real seed-corpus ClassDefinitions
/// (Soarscore.SeedData) rather than hand-built fixtures — F3J throughout, whose
/// fixed-sequence preliminary draws with a bare <c>DrawPhase(1, [])</c>. The fold
/// half of each event is asserted too, by applying the emitted event directly.
/// </summary>
public class DrawAcceptanceDecideTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);

    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Draw Acceptance Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    /// <summary>F3J adopted, 12 competitors, one round of the preliminary drawn — the live-phase fixture.</summary>
    private static Competition DrawnF3J()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var drawn = competition.DrawPhase(1, [], Now);
        drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "draw succeeded");
        return competition.Apply(drawn.Value);
    }

    [Fact]
    public void AcceptDraw_succeeds_and_folds_the_live_phase_status_to_accepted()
    {
        var competition = DrawnF3J();

        var result = competition.AcceptDraw(Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.At.Should().Be(Now);

        var accepted = competition.Apply(result.Value);
        accepted.Phases.Single().Draw.Status.Should().Be("accepted");
        // Nothing else about the phase moves: rounds and groups stay as drawn.
        accepted.Phases.Single().Rounds.Should().BeEquivalentTo(competition.Phases.Single().Rounds);
    }

    [Fact]
    public void AcceptDraw_twice_fails_with_a_stable_code()
    {
        var competition = DrawnF3J();
        competition = competition.Apply(competition.AcceptDraw(Now).Value);

        var result = competition.AcceptDraw(Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("acceptDraw.alreadyAccepted");
    }

    [Fact]
    public void AcceptDraw_with_nothing_drawn_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);

        var result = competition.AcceptDraw(Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("acceptDraw.noDrawnPhase");
    }

    [Fact]
    public void RejectDraw_with_nothing_drawn_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);

        var result = competition.RejectDraw(phaseHasEntries: false, "Groups look wrong", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("rejectDraw.noDrawnPhase");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectDraw_with_a_blank_reason_fails_with_a_stable_code(string blankReason)
    {
        // F2: Reason is a substantive CD ruling record, validated in the decide
        // function — AnnulTaskRound's precedent.
        var competition = DrawnF3J();

        var result = competition.RejectDraw(phaseHasEntries: false, blankReason, Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("rejectDraw.reasonRequired");
    }

    [Fact]
    public void RejectDraw_with_entries_against_the_phase_fails_with_a_stable_code()
    {
        // D5: entries reference the doomed draw's GroupIds. Unreachable through
        // the API under D4, but the decide function does not trust that.
        var competition = DrawnF3J();

        var result = competition.RejectDraw(phaseHasEntries: true, "Groups look wrong", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("rejectDraw.entriesExist");
    }

    [Fact]
    public void RejectDraw_of_an_accepted_draw_is_permitted_and_removes_the_phase_from_the_fold()
    {
        // D2 + the story's "Semantics spelled out": rejecting an accepted draw
        // nobody has flown against is the ordinary correction path — there is
        // deliberately no rejectDraw.alreadyAccepted.
        var competition = DrawnF3J();
        competition = competition.Apply(competition.AcceptDraw(Now).Value);

        var result = competition.RejectDraw(phaseHasEntries: false, "Accepted in error", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhaseOrdinal.Should().Be(0);

        var rejected = competition.Apply(result.Value);
        rejected.Phases.Should().BeEmpty();
    }

    // The core cycle — draw → reject → register latecomer → redraw. Asserts
    // D2's ordinal-correctness claim concretely: the redraw's PhaseOrdinal is
    // again 0, addressing phase definition 0 (the preliminary), not the flyoff,
    // and its groups come from a field that includes the latecomer.

    [Fact]
    public void A_rejected_draw_can_be_redrawn_after_a_latecomer_registers_with_the_preliminary_addressed_again()
    {
        var competition = DrawnF3J();
        competition = competition.Apply(competition.RejectDraw(phaseHasEntries: false, "Late entrant arrived", Now).Value);

        // The freeze lifted with the rejection (D6): the latecomer registers.
        var latecomer = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
        latecomer.IsSuccess.Should().BeTrue(latecomer.Code ?? "registration succeeded");
        competition = competition.Apply(latecomer.Value);

        var redrawn = competition.DrawPhase(1, [], Now);

        redrawn.IsSuccess.Should().BeTrue(redrawn.Code ?? "redraw succeeded");
        redrawn.Value.PhaseOrdinal.Should().Be(0);

        var folded = competition.Apply(redrawn.Value);
        folded.Phases.Length.Should().Be(1);
        folded.Phases.Single().Ordinal.Should().Be(0);
        folded.Phases.Single().Draw.Status.Should().Be("drawn");

        foreach (var round in redrawn.Value.Rounds)
        {
            var placed = round.TaskRounds[0].Groups.SelectMany(g => g.CompetitorRefs).ToArray();
            placed.Length.Should().Be(13); // the original 12 plus the latecomer
            placed.Distinct().Count().Should().Be(13);
            placed.Should().Contain(latecomer.Value.Competitor.Id);
        }
    }

    // Entry gates — D4: an entry cannot be opened until the draw is accepted.

    private static (Competition Competition, GroupId GroupRef, CompetitorId CompetitorRef) DrawnF3JWithCoordinates()
    {
        var competition = DrawnF3J();
        var group = competition.Phases[0].Rounds[0].TaskRounds[0].Groups[0];
        return (competition, group.Id, group.CompetitorRefs[0]);
    }

    [Fact]
    public void OpenEntry_before_acceptance_fails_with_entry_drawNotAccepted()
    {
        var (competition, groupRef, competitorRef) = DrawnF3JWithCoordinates();

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitorRef, ReflightRole.Original, Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.drawNotAccepted");
    }

    [Fact]
    public void OpenEntry_after_acceptance_succeeds()
    {
        var (competition, groupRef, competitorRef) = DrawnF3JWithCoordinates();
        competition = competition.Apply(competition.AcceptDraw(Now).Value);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitorRef, ReflightRole.Original, Now);

        result.IsSuccess.Should().BeTrue(result.Code ?? "entry opened");
    }

    [Fact]
    public void Withdrawing_after_acceptance_leaves_the_draw_intact_and_the_withdrawal_honoured()
    {
        // Withdrawal stays ungated forever (D6): it never removes or reopens
        // the accepted draw — the competitor's entries simply never occur.
        var (competition, groupRef, competitorRef) = DrawnF3JWithCoordinates();
        competition = competition.Apply(competition.AcceptDraw(Now).Value);

        var withdrawn = competition.WithdrawCompetitor(competitorRef, Now);
        withdrawn.IsSuccess.Should().BeTrue(withdrawn.Code ?? "withdrawal succeeded");
        competition = competition.Apply(withdrawn.Value);

        competition.Phases.Single().Draw.Status.Should().Be("accepted");

        var reopened = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitorRef, ReflightRole.Original, Now);

        reopened.IsFailure.Should().BeTrue();
        reopened.Code.Should().Be("openEntry.competitorWithdrawn");
    }
}
