using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide- and fold-function tests for <see cref="Competition.RecordTieBreakOutcome"/> —
/// kanban/in-progress/operational-tie-break-resolution.md WI-1. One fact per
/// defect code plus the happy paths, mirroring RecordReflightRulingDecideTests's
/// corpus-driven construction (the draw is bypassed; the Phase/Round/TaskRound/
/// Group shape is hand-built directly). The accepting classes are the corpus's
/// operational ones — F3K (<c>tiebreakFlyoff</c> after <c>bestDroppedScore</c>),
/// F3B (<c>additionalFullRound</c>), F3F (<c>classificationRounds</c>) — and F5L
/// (<c>undefinedRequiresRuling</c>, the D7 ruling inclusion).
/// </summary>
public class RecordTieBreakOutcomeDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// <paramref name="definition"/>'s class, two registered competitors, one
    /// hand-built drawn phase (ordinal 0).
    /// </summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors) BuildDrawnCompetition(
        ClassDefinition definition)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Tie-Break Outcome Test Comp", "Nowhere",
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 8),
            "1.0.0", adoptedRules, At);

        var competition = Competition.Create(created);

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>();
        for (var i = 0; i < 2; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), At);
            competition = competition.Apply(registered.Value);
            competitors.Add(registered.Value.Competitor.Id);
        }

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [competitors[0]] };
        var taskCode = definition.Phases[0].Tasks[0].Code;
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = taskCode, Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = At, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], At));

        return (competition, competitors.ToImmutable());
    }

    private static TieBreakOutcome Outcome(
        TieBreakDirective directive,
        ImmutableArray<CompetitorId> refs,
        int[] places,
        int phaseOrdinal = 0,
        string reason = "Fly-off flown, scoresheet with the CD",
        string? by = null) =>
        new()
        {
            PhaseOrdinal = phaseOrdinal,
            Directive = directive,
            Placings = refs.Zip(places, (c, p) => new TieBreakOutcomePlacing(c, p)).ToImmutableArray(),
            Reason = reason,
            By = by,
            At = At,
        };

    private static TieBreakOutcomePlacing Placing(CompetitorId competitor, int place) =>
        new(competitor, place);

    // ------------------------------------------------------- one fact per defect code

    [Fact]
    public void RecordTieBreakOutcome_against_an_undrawn_phase_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [1, 2], phaseOrdinal: 99));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.phaseNotFound");
    }

    [Theory]
    [InlineData(typeof(BestDroppedScore))]
    [InlineData(typeof(EqualPlaces))]
    public void RecordTieBreakOutcome_against_a_non_resolution_directive_fails_with_a_stable_code(
        Type directiveType)
    {
        // F3K states BestDroppedScore on its own ladder, so the refusal below
        // can only be about the directive's own kind — a comparator never
        // halts, and EqualPlaces settles itself.
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        var directive = (TieBreakDirective)Activator.CreateInstance(directiveType)!;

        var result = competition.RecordTieBreakOutcome(
            Outcome(directive, competitors, [1, 2]));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.directiveNotAResolution");
    }

    [Fact]
    public void RecordTieBreakOutcome_against_a_directive_the_class_does_not_state_fails_with_a_stable_code()
    {
        // F3K states [bestDroppedScore, tiebreakFlyoff] — AdditionalFullRound
        // is operational but unstated here, so accepting it would let the CD
        // believe they settled something that had no effect.
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var result = competition.RecordTieBreakOutcome(
            Outcome(new AdditionalFullRound(), competitors, [1, 2]));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.directiveNotStated");
    }

    [Fact]
    public void RecordTieBreakOutcome_for_an_unregistered_competitor_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        var refs = ImmutableArray.Create(competitors[0], CompetitorId.New());

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), refs, [1, 2]));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.competitorNotFound");
    }

    [Theory]
    // A single placing is not a group.
    [InlineData(new[] { 1 })]
    // Gapped numbering: the place after a sole 1st is 2nd, not 3rd.
    [InlineData(new[] { 1, 3 })]
    // Numbering must start at 1.
    [InlineData(new[] { 2, 3 })]
    public void RecordTieBreakOutcome_with_malformed_numbering_fails_with_a_stable_code(
        int[] places)
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        var refs = places.Length == 1
            ? ImmutableArray.Create(competitors[0])
            : competitors;

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), refs, places));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.placingsMalformed");
    }

    [Fact]
    public void RecordTieBreakOutcome_naming_one_competitor_twice_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        var outcome = new TieBreakOutcome
        {
            PhaseOrdinal = 0,
            Directive = new TieBreakFlyoff(),
            Placings = [Placing(competitors[0], 1), Placing(competitors[0], 2)],
            Reason = "Fly-off flown",
            At = At,
        };

        var result = competition.RecordTieBreakOutcome(outcome);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.placingsMalformed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordTieBreakOutcome_with_a_blank_reason_fails_with_a_stable_code(string reason)
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [1, 2], reason: reason));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.reasonRequired");
    }

    [Fact]
    public void RecordTieBreakOutcome_with_a_blank_By_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [1, 2], by: "   "));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordTieBreakOutcome.byBlank");
    }

    // ---------------------------------------------------------------- happy paths

    [Fact]
    public void RecordTieBreakOutcome_for_a_stated_flyoff_succeeds_and_the_event_carries_the_outcome_verbatim()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        var outcome = Outcome(new TieBreakFlyoff(), competitors, [1, 2], by: "the contest director");

        var result = competition.RecordTieBreakOutcome(outcome);

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
        result.Value.Outcome.Should().Be(outcome);
    }

    [Fact]
    public void RecordTieBreakOutcome_is_accepted_under_F5L_as_a_ruling()
    {
        // D7: UndefinedRequiresRuling records through the same event, command
        // and engine path — the directive on the record says which authority
        // acted, so no second kind exists.
        var (competition, competitors) = BuildDrawnCompetition(SeedF5L.Definition);
        var outcome = Outcome(
            new UndefinedRequiresRuling(), competitors, [2, 1],
            reason: "CD ruling: first pilot's landing overran the boundary");

        var result = competition.RecordTieBreakOutcome(outcome);

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
        result.Value.Outcome.Should().Be(outcome);
    }

    [Fact]
    public void RecordTieBreakOutcome_leaving_the_group_tied_is_accepted()
    {
        // D1: equal placings record a tie the fly-off did not separate —
        // the dormant comparator rungs (WI-2) decide what happens next.
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [1, 1]));

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
    }

    [Fact]
    public void RecordTieBreakOutcome_for_a_withdrawn_competitor_is_accepted()
    {
        // Withdrawal is not checked (the ruling precedent's planner's call 2):
        // a moot outcome is inert, not harmful — only registration is typo
        // protection.
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        competition = competition.Apply(competition.WithdrawCompetitor(competitors[0], At).Value);

        var result = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [1, 2]));

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
    }

    // ------------------------------------------------------------------ fold tests

    [Fact]
    public void Applying_a_recorded_outcome_appends_without_touching_anything_else()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);
        var outcome = Outcome(new TieBreakFlyoff(), competitors, [1, 2]);

        var updated = competition.Apply(new TieBreakOutcomeRecorded(outcome));

        updated.TieBreakOutcomes.Should().ContainSingle().Which.Should().Be(outcome);
        updated.Competitors.Should().HaveCount(2);
        updated.Phases.Should().HaveCount(1);
        updated.Rulings.Should().BeEmpty();
    }

    [Fact]
    public void Folding_two_outcomes_for_one_group_keeps_both_in_log_order()
    {
        // D4's fold half: re-recording supersedes at lookup, never by
        // overwrite — the log keeps every decision in the order it was given.
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var first = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [1, 2])).Value;
        var second = competition.RecordTieBreakOutcome(
            Outcome(new TieBreakFlyoff(), competitors, [2, 1],
                reason: "Re-flown: wind shift invalidated the first fly-off")).Value;

        var updated = competition.Apply(first).Apply(second);
        updated.TieBreakOutcomes.Should().HaveCount(2);
        updated.TieBreakOutcomes[0].Should().Be(first.Outcome);
        updated.TieBreakOutcomes[1].Should().Be(second.Outcome);
    }
}
