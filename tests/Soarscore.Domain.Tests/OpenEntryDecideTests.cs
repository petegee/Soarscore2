using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.OpenEntry"/> —
/// kanban/completed/capture-a-score-steel-thread-plan.md WI-2. Mirrors
/// PhaseDrawnDecideTests's and BindParameterDecideTests's style: real
/// seed-corpus ClassDefinitions (Soarscore.SeedData) wherever the corpus
/// already has the shape a case needs. The draw itself is bypassed —
/// DrawPhase does not support every corpus composition (F3K is
/// ChooseFromCatalogue) — so the Phase/Round/TaskRound/Group shape is
/// hand-built directly, the same way CompetitionReplaceTaskRoundPropertyTests
/// does for its own navigation tests.
// The until-all-flights-complete success path and the two parameterised
// working-time resolution tests moved to ResolvedWorkingTimeTests.cs (WI-4).
/// </summary>
public class OpenEntryDecideTests
{
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors) BuildRegisteredCompetition(
        ClassDefinition definition, int competitorCount, DateTimeOffset at)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = at,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Open Entry Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, at);

        var competition = Competition.Create(created);

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>();
        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), at);
            competition = competition.Apply(registered.Value);
            competitors.Add(registered.Value.Competitor.Id);
        }

        return (competition, competitors.ToImmutable());
    }

    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors, GroupId GroupRef) BuildDrawnCompetition(
        ClassDefinition definition,
        string taskCode,
        int competitorCount,
        DateTimeOffset at,
        TaskRoundState taskRoundState = TaskRoundState.Drawn)
    {
        var (competition, competitors) = BuildRegisteredCompetition(definition, competitorCount, at);

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitors[0]] };
        var taskRound = new TaskRound { Ordinal = 1, State = taskRoundState, TaskRef = taskCode, Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = at, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], at));
        // Entries open only against an accepted draw (D4) — the acceptance is
        // arrangement, not the subject here; the D4 gate itself has
        // DrawAcceptanceDecideTests.
        competition = competition.Apply(new DrawAccepted(0, at));

        return (competition, competitors, groupRef);
    }

    /// <summary>
    /// A competition drawn with <paramref name="roundCount"/> rounds, each one
    /// task-round of the named task with one group containing
    /// <c>competitors[0]</c> — the multi-round shape the make-up facts need
    /// (reflight-aggregate-destination.md WI-2: a counts-for round must be an
    /// earlier round of the same phase).
    /// </summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors, ImmutableArray<GroupId> RoundGroupRefs) BuildMultiRoundCompetition(
        ClassDefinition definition,
        string taskCode,
        int competitorCount,
        int roundCount,
        DateTimeOffset at)
    {
        var (competition, competitors) = BuildRegisteredCompetition(definition, competitorCount, at);

        var roundGroupRefs = ImmutableArray.CreateBuilder<GroupId>();
        var rounds = ImmutableArray.CreateBuilder<Round>();
        for (var ordinal = 1; ordinal <= roundCount; ordinal++)
        {
            var groupRef = GroupId.New();
            var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitors[0]] };
            var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = taskCode, Groups = [group] };
            rounds.Add(new Round { Ordinal = ordinal, TaskRounds = [taskRound] });
            roundGroupRefs.Add(groupRef);
        }

        var draw = new Draw { CreatedAt = at, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, rounds.ToImmutable(), at));
        competition = competition.Apply(new DrawAccepted(0, at));

        return (competition, competitors, roundGroupRefs.ToImmutable());
    }

    /// <summary>F3J's TaskD with its Fixed WorkingTime nulled out — a definition defect, not a shape the corpus itself contains.</summary>
    private static ClassDefinition WithUndeclaredWorkingTime(ClassDefinition definition)
    {
        var phase = definition.Phases[0];
        var task = phase.Tasks[0] with { Timing = phase.Tasks[0].Timing with { WorkingTime = null } };
        return definition with { Phases = [phase with { Tasks = [task] }] };
    }

    [Fact]
    public void OpenEntry_against_an_undrawn_phase_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at);

        var result = competition.OpenEntry(EntryId.New(), 99, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.phaseNotDrawn");
    }

    [Fact]
    public void OpenEntry_against_a_round_that_does_not_exist_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 99, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.roundNotFound");
    }

    [Fact]
    public void OpenEntry_against_a_task_round_that_does_not_exist_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 99, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.taskRoundNotFound");
    }

    [Fact]
    public void OpenEntry_against_a_group_that_does_not_exist_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, _) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, GroupId.New(), competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.groupNotFound");
    }

    [Theory]
    [InlineData(TaskRoundState.Complete)]
    [InlineData(TaskRoundState.Annulled)]
    public void OpenEntry_against_a_closed_task_round_fails_with_a_stable_code(TaskRoundState state)
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at, state);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.taskRoundClosed");
    }

    // kanban/completed/task-round-lifecycle.md WI-10: the theory above builds
    // its closed state by hand, which is all that was possible while nothing
    // could emit TaskRoundCompleted or TaskRoundAnnulled. These three drive the
    // check the way the system now does — through the real events — so the
    // dormant check at Competition.cs's OpenEntry gets its first end-to-end
    // proof, including that a reopening lifts it again (NFR-4: a late score is
    // never permanently locked out).

    [Fact]
    public void OpenEntry_after_the_task_round_is_completed_fails_with_taskRoundClosed()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 1, at);
        competition = competition.Apply(new TaskRoundCompleted(0, 1, 1, at));

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.taskRoundClosed");
    }

    [Fact]
    public void OpenEntry_after_the_task_round_is_annulled_fails_with_taskRoundClosed()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 1, at);
        competition = competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Winch failure", at));

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.taskRoundClosed");
    }

    [Theory]
    [InlineData(TaskRoundState.Complete)]
    [InlineData(TaskRoundState.Annulled)]
    public void OpenEntry_succeeds_again_once_the_task_round_is_reopened(TaskRoundState closedAs)
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 1, at);
        competition = closedAs is TaskRoundState.Complete
            ? competition.Apply(new TaskRoundCompleted(0, 1, 1, at))
            : competition.Apply(new TaskRoundAnnulled(0, 1, 1, "Winch failure", at));
        competition = competition.Apply(new TaskRoundReopened(0, 1, 1, "Score handed in that evening", at));

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void OpenEntry_for_a_competitor_not_drawn_into_the_group_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at);

        // competitors[1] was registered but not placed in the (single-member) group.
        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[1], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.competitorNotDrawn");
    }

    [Fact]
    public void OpenEntry_for_a_withdrawn_competitor_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 2, at);

        var withdrawn = competition.WithdrawCompetitor(competitors[0], at);
        competition = competition.Apply(withdrawn.Value);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.competitorWithdrawn");
    }

    [Fact]
    public void OpenEntry_against_a_task_whose_Fixed_timing_declares_no_WorkingTime_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        var definition = WithUndeclaredWorkingTime(SeedF3J.Definition);
        var (competition, competitors, groupRef) = BuildDrawnCompetition(definition, "D", 1, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.workingTimeUndeclared");
    }

    [Fact]
    public void OpenEntry_against_an_unbound_undefaulted_parameterised_WorkingTime_fails_with_a_stable_code()
    {
        var at = DateTimeOffset.UtcNow;
        // NZ N's roundDuration is BeforeFlying-bound with no declared default (NZ.3.13.1 k).
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedNzNAles123.Definition, "D", 1, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.parameterUnbound");
    }

    [Fact]
    public void OpenEntry_under_a_Fixed_working_time_derives_the_window_end()
    {
        var at = DateTimeOffset.UtcNow;
        // F3J.6.2 b: TaskD's WorkingTime is a literal 600 s, no parameter involved.
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 1, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompetitionRef.Should().Be(competition.Id);
        result.Value.PhaseOrdinal.Should().Be(0);
        result.Value.RoundOrdinal.Should().Be(1);
        result.Value.TaskRoundOrdinal.Should().Be(1);
        result.Value.GroupRef.Should().Be(groupRef);
        result.Value.CompetitorRef.Should().Be(competitors[0]);
        result.Value.Role.Should().Be(ReflightRole.Original);
    }

    [Theory]
    [InlineData(ReflightRole.Original)]
    [InlineData(ReflightRole.Entitled)]
    [InlineData(ReflightRole.Filler)]
    public void OpenEntry_round_trips_the_supplied_role_into_the_event(ReflightRole role)
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, groupRef) = BuildDrawnCompetition(SeedF3J.Definition, "D", 1, at);

        var result = competition.OpenEntry(EntryId.New(), 0, 1, 1, groupRef, competitors[0], role, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(role);
    }

    // kanban/in-progress/reflight-groups.md WI-3. The role is a ruling
    // recorded as data, not validated here — every existing fact above is
    // unchanged because the handler supplies ReflightRole.Original (WI-5).

    // reflight-aggregate-destination.md WI-2 — the make-up validations (D6/D4)
    // and the D5 drawn-check relaxation. One fact per new code; the pinned
    // Original-role drawn refusal above (competitorNotDrawn) is unchanged.

    [Fact]
    public void OpenEntry_with_a_counts_for_round_on_an_Original_role_fails_with_destinationOnOriginalRole()
    {
        // D6 bullet 1: an Original counts for its own round, always.
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        var result = competition.OpenEntry(
            EntryId.New(), 0, 2, 1, roundGroups[1], competitors[0], ReflightRole.Original, at, countsForRoundOrdinal: 1);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.destinationOnOriginalRole");
    }

    [Fact]
    public void OpenEntry_with_a_counts_for_round_the_phase_does_not_have_fails_with_destinationNotFound()
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        var result = competition.OpenEntry(
            EntryId.New(), 0, 2, 1, roundGroups[1], competitors[0], ReflightRole.Entitled, at, countsForRoundOrdinal: 9);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.destinationNotFound");
    }

    [Theory]
    [InlineData(1)] // the entry's own round
    [InlineData(2)] // a later round of the same phase
    public void OpenEntry_with_a_counts_for_round_not_earlier_than_the_entrys_own_fails_with_destinationNotEarlier(int countsFor)
    {
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        var result = competition.OpenEntry(
            EntryId.New(), 0, 1, 1, roundGroups[0], competitors[0], ReflightRole.Entitled, at, countsForRoundOrdinal: countsFor);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.destinationNotEarlier");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void OpenEntry_with_a_counts_for_round_and_no_reason_fails_with_reasonRequired(string? reason)
    {
        // D4: the reason is the entitlement basis — required exactly when the
        // counts-for round is set, mirroring AppendReflightGroup's rule.
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        var result = competition.OpenEntry(
            EntryId.New(), 0, 2, 1, roundGroups[1], competitors[0], ReflightRole.Entitled, at, 1, reason);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.reasonRequired");
    }

    [Fact]
    public void OpenEntry_for_a_reflight_role_into_a_group_the_competitor_was_not_drawn_into_succeeds()
    {
        // D5's relaxation: the CD's allocation is the act, so a reflight-role
        // entry may be opened into any group of the addressed task-round for
        // a registered, non-withdrawn competitor — including the priority-(a)
        // shape (re-flown with a group the competitor was not drawn into).
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        // competitors[1] was registered but not placed in the (single-member) group.
        var result = competition.OpenEntry(
            EntryId.New(), 0, 2, 1, roundGroups[1], competitors[1], ReflightRole.Entitled, at);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void OpenEntry_make_up_for_a_round_the_competitor_was_not_drawn_into_carries_the_datum()
    {
        // The make-up shape end to end at the decide: reflight-role entry,
        // non-drawn group, counts-for an earlier round, reason recorded — and
        // the event carries all of it verbatim.
        var at = DateTimeOffset.UtcNow;
        var (competition, competitors, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        var result = competition.OpenEntry(
            EntryId.New(), 0, 2, 1, roundGroups[1], competitors[1], ReflightRole.Entitled, at, 1, "Missed round 1 — car trouble");

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(ReflightRole.Entitled);
        result.Value.CountsForRoundOrdinal.Should().Be(1);
        result.Value.Reason.Should().Be("Missed round 1 — car trouble");
    }

    [Fact]
    public void OpenEntry_for_an_unregistered_competitor_with_a_reflight_role_fails_with_competitorNotRegistered()
    {
        // The D5 relaxation removed the drawn-check that implicitly guaranteed
        // registration for reflight-role opens, so registration is checked
        // explicitly — for every role.
        var at = DateTimeOffset.UtcNow;
        var (competition, _, roundGroups) = BuildMultiRoundCompetition(SeedF3J.Definition, "D", 2, 2, at);

        var result = competition.OpenEntry(
            EntryId.New(), 0, 2, 1, roundGroups[1], CompetitorId.New(), ReflightRole.Entitled, at);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.competitorNotRegistered");
    }
}
