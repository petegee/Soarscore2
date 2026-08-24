using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.RecordReflightRuling"/> —
/// kanban/in-progress/reflight-scoring-rulings.md WI-2. One fact per defect
/// code plus the happy paths, mirroring AppendReflightGroupDecideTests's
/// corpus-driven construction (the draw is bypassed; the Phase/Round/TaskRound/
/// Group shape is hand-built directly). The accepting classes are the corpus's
/// genuinely silent ones — NZ Class M ALES 200 and F5L (Undefined × 2) — and
/// F3K is the classRuleSpeaks case (Replacement/BetterOf, both slots concrete).
/// F3F pins the mixed-class stance: one silent slot keeps the ruling acceptable.
/// </summary>
public class RecordReflightRulingDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// <paramref name="definition"/>'s class, two registered competitors, one
    /// hand-built drawn task-round (ordinal 1) in the requested state.
    /// </summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors) BuildDrawnCompetition(
        ClassDefinition definition,
        Soarscore.Domain.Competitions.TaskRoundState taskRoundState =
            Soarscore.Domain.Competitions.TaskRoundState.Drawn)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Reflight Ruling Test Comp", "Nowhere",
            new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 25),
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
        var taskRound = new TaskRound { Ordinal = 1, State = taskRoundState, TaskRef = taskCode, Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = At, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], At));

        return (competition, competitors.ToImmutable());
    }

    private static ReflightRuling Ruling(
        CompetitorId competitor,
        ReflightSelection selection,
        string reason = "Timing failure unresolved by the rulebook",
        string? by = null,
        TaskRoundCoordinate? taskRound = null) =>
        new()
        {
            TaskRound = taskRound ?? new TaskRoundCoordinate(0, 1, 1),
            CompetitorRef = competitor,
            Selection = selection,
            Reason = reason,
            By = by,
            At = At,
        };

    // ------------------------------------------------------- one fact per defect code

    [Theory]
    [InlineData(ReflightSelection.NotPermitted)]
    [InlineData(ReflightSelection.UndefinedRequiresRuling)]
    public void RecordReflightRuling_with_a_non_resolution_selection_fails_with_a_stable_code(
        ReflightSelection selection)
    {
        // NZ Class M ALES 200 (80-nz-m-ales200): silent on both slots, so the
        // refusal below can only be about the ruling's own shape.
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], selection));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.selectionNotAResolution");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordReflightRuling_with_a_blank_reason_fails_with_a_stable_code(string reason)
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.Replacement, reason));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.reasonRequired");
    }

    [Fact]
    public void RecordReflightRuling_with_a_blank_By_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.BetterOf, by: "   "));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.byBlank");
    }

    [Fact]
    public void RecordReflightRuling_against_ordinals_that_name_no_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);

        var result = competition.RecordReflightRuling(
            Ruling(competitors[0], ReflightSelection.Replacement, taskRound: new TaskRoundCoordinate(99, 1, 1)));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.taskRoundNotFound");
    }

    [Fact]
    public void RecordReflightRuling_against_an_annulled_task_round_fails_with_a_stable_code()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Winch failure", At));

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.Replacement));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.taskRoundAnnulled");
    }

    [Fact]
    public void RecordReflightRuling_for_an_unregistered_competitor_fails_with_a_stable_code()
    {
        var (competition, _) = BuildDrawnCompetition(SeedNzMAles200.Definition);

        var result = competition.RecordReflightRuling(Ruling(CompetitorId.New(), ReflightSelection.Replacement));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.competitorNotFound");
    }

    [Fact]
    public void RecordReflightRuling_where_the_class_rule_speaks_fails_with_a_stable_code()
    {
        // F3K.9.6 states both slots (Replacement / BetterOf) — the rulebook
        // governs and there is nothing for a CD to fill (decision 3).
        var (competition, competitors) = BuildDrawnCompetition(SeedF3K.Definition);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.Replacement));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordReflightRuling.classRuleSpeaks");
    }

    // ---------------------------------------------------------------- happy paths

    [Fact]
    public void RecordReflightRuling_under_a_silent_class_succeeds_and_the_event_carries_the_ruling_verbatim()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);
        var ruling = Ruling(competitors[0], ReflightSelection.Replacement, by: "the contest director");

        var result = competition.RecordReflightRuling(ruling);

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
        result.Value.Ruling.Should().Be(ruling);
    }

    [Fact]
    public void RecordReflightRuling_is_accepted_under_F5L_and_folds_one_ruling_on()
    {
        var (competition, competitors) = BuildDrawnCompetition(SeedF5L.Definition);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.BetterOf));
        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");

        var updated = competition.Apply(result.Value);
        updated.Rulings.Should().ContainSingle().Which.Should().Be(result.Value.Ruling);
    }

    [Fact]
    public void Folding_two_rulings_for_one_key_keeps_both_in_log_order()
    {
        // RR3's fold half: supersede accumulates, never replaces — the log
        // keeps every decision in the order it was given.
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);

        var first = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.BetterOf)).Value;
        var second = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.Replacement)).Value;

        var updated = competition.Apply(first).Apply(second);
        updated.Rulings.Should().HaveCount(2);
        updated.Rulings[0].Should().Be(first.Ruling);
        updated.Rulings[1].Should().Be(second.Ruling);
    }

    // ------------------------------------------- planner's calls 2 and 4, pinned

    [Fact]
    public void RecordReflightRuling_for_a_withdrawn_competitor_is_accepted()
    {
        // Withdrawal is not checked (planner's call 2): a moot ruling is inert,
        // not harmful — only registration is typo protection.
        var (competition, competitors) = BuildDrawnCompetition(SeedNzMAles200.Definition);
        competition = competition.Apply(competition.WithdrawCompetitor(competitors[0], At).Value);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.Replacement));

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
    }

    [Fact]
    public void RecordReflightRuling_in_a_mixed_class_with_one_silent_slot_is_accepted()
    {
        // F3F.1.5: EntitledScores Replacement, OthersScore silent. The decide
        // cannot know the competitor's role, so one silence is enough to keep
        // the ruling acceptable (planner's call 4); scoring ignores any ruling
        // that lands on the speaking slot (RR1).
        var (competition, competitors) = BuildDrawnCompetition(SeedF3F.Definition);

        var result = competition.RecordReflightRuling(Ruling(competitors[0], ReflightSelection.Replacement));

        result.IsSuccess.Should().BeTrue($"{result.Code}: {result.Message}");
    }
}
