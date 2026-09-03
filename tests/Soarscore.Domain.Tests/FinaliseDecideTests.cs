using System.Collections.Immutable;
using System.Linq;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.Finalise"/> — the validity
/// gate, kanban/completed/task-round-lifecycle.md WI-3. One case per defect
/// code plus the success case.
/// <para>
/// The gate is entirely data-driven off <c>PhaseDefinition.Validity</c>, so the
/// fixtures are the real seed classes that carry the shapes it has to read:
/// F3J's literal <c>MinRounds = 4</c> (F3J.3.1 a), F3B's <c>MinTasks</c> (the
/// corpus's only one, F3B.2.1 b) and F5K's unbound <c>param("minRounds")</c>
/// (5.5.10 states no rule, so the CD decides). F3J goes through the real
/// DrawPhase; F3B and F5K cannot be drawn today (multi-task rounds and
/// catalogue choice respectively), so their phase shape is applied directly as
/// a PhaseDrawn, the way OpenEntryDecideTests does.
/// </para>
/// </summary>
public class FinaliseDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableArray<DeclaredResult> OneResult =
    [
        new DeclaredResult { CompetitorRef = CompetitorId.New(), Aggregate = 1000m, Placing = 1, Promoted = false },
    ];

    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = At,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Finalise Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, At);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), At);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    /// <summary>F3J, 12 competitors, <paramref name="rounds"/> drawn, the first <paramref name="complete"/> of them completed.</summary>
    private static Competition DrawnF3J(int rounds, int complete)
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var drawn = competition.DrawPhase(rounds, [], At);
        drawn.IsSuccess.Should().BeTrue();
        competition = competition.Apply(drawn.Value);

        for (var round = 1; round <= complete; round++)
        {
            competition = competition.Apply(new TaskRoundCompleted(0, round, 1, At));
        }

        return competition;
    }

    /// <summary>
    /// A phase of one round holding one Complete task-round per named task —
    /// applied as a PhaseDrawn rather than drawn, since neither F3B nor F5K is
    /// drawable today. Groups are empty: Finalise reads state and TaskRef only.
    /// </summary>
    private static Competition WithOneFlownRound(Competition competition, params string[] taskCodes)
    {
        var taskRounds = taskCodes
            .Select((code, i) => new TaskRound
            {
                Ordinal = i + 1,
                State = TaskRoundState.Complete,
                TaskRef = code,
                Groups = [new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [] }],
            })
            .ToImmutableArray();

        var round = new Round { Ordinal = 1, TaskRounds = taskRounds };
        var draw = new Draw { CreatedAt = At, Status = "drawn" };
        return competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], At));
    }

    /// <summary>
    /// F3B with its MinTasks raised from 1 to 3 — a definition-level variation,
    /// not a corpus shape. F3B's real MinTasks of 1 can never fail: a round only
    /// counts once every task-round in it is Complete, so any round that counts
    /// already contributes at least one distinct task. Raising it is the only
    /// way to exercise the check at all.
    /// </summary>
    private static ClassDefinition WithMinTasks(ClassDefinition definition, int minTasks)
    {
        var phase = definition.Phases[0];
        return definition with
        {
            Phases = definition.Phases.SetItem(0, phase with { Validity = phase.Validity with { MinTasks = minTasks } }),
        };
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Finalise_with_a_blank_By_fails_with_a_stable_code(string by)
    {
        var competition = DrawnF3J(rounds: 4, complete: 4);

        var result = competition.Finalise(OneResult, [], by, At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.byRequired");
    }

    [Fact]
    public void Finalise_with_no_declared_results_fails_with_a_stable_code()
    {
        // Finalisation.DeclaredResults is 1..*.
        var competition = DrawnF3J(rounds: 4, complete: 4);

        var result = competition.Finalise([], [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.noResults");
    }

    [Fact]
    public void Finalise_against_an_already_finalised_competition_fails_with_a_stable_code()
    {
        var competition = DrawnF3J(rounds: 4, complete: 4);
        var first = competition.Finalise(OneResult, [], "CD Jane", At);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.alreadyFinalised");
    }

    [Fact]
    public void Finalise_when_MinRounds_is_an_unbound_parameter_fails_with_a_stable_code()
    {
        // F5K: 5.5.10 defines no minimum-rounds rule, so minRounds is a
        // CD-bound parameter with no declared default (SeedF5K.cs).
        var competition = CompetitionAdopting(SeedF5K.Definition, 10);
        competition = WithOneFlownRound(competition, "A");

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.parameterUnbound");
    }

    [Fact]
    public void Finalise_with_MinRounds_bound_after_being_unbound_passes_the_same_gate()
    {
        // The other half of the F5K case: the CD's decision is what makes the
        // competition finalisable, and nothing but the binding changed.
        // Bound before the phase is applied: minRounds is a CompetitionSetup
        // parameter, so it freezes at the draw like any other.
        var competition = CompetitionAdopting(SeedF5K.Definition, 10);
        var bound = competition.BindParameter("minRounds", MeasuredValue.Of(1m), "CD Jane", At);
        bound.IsSuccess.Should().BeTrue();
        competition = competition.Apply(bound.Value);
        competition = WithOneFlownRound(competition, "A");

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Finalise_with_fewer_complete_rounds_than_the_class_requires_fails_with_a_stable_code()
    {
        // F3J.3.1 a: MinRounds = 4.
        var competition = DrawnF3J(rounds: 4, complete: 3);

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.notEnoughRounds");
    }

    [Fact]
    public void Finalise_does_not_count_an_annulled_round_toward_validity()
    {
        // The distinction WI-0 named: IsFullyFlown, not IsCompleteOrAnnulled.
        // Nothing is left to fly here, and the competition is still invalid.
        var competition = DrawnF3J(rounds: 4, complete: 3);
        competition = competition.Apply(new TaskRoundAnnulled(0, 4, 1, "Winch failure", At));

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.notEnoughRounds");
    }

    [Fact]
    public void Finalise_of_an_undrawn_competition_fails_with_notEnoughRounds()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        // The plan (WI-3) argued this needed no Phases.IsEmpty check of its
        // own, because "an undrawn competition has zero complete rounds and
        // fails notEnoughRounds". The reasoning was wrong — the gate is a loop
        // over Phases, which with none simply never runs — so the check was
        // added explicitly, under the same code. This test is what caught it.
        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.notEnoughRounds");
    }

    [Fact]
    public void Finalise_with_fewer_distinct_complete_tasks_than_MinTasks_fails_with_a_stable_code()
    {
        // F3B is the corpus's only class populating MinTasks (F3B.2.1 b);
        // raised to 3 here so the check can fail at all (see WithMinTasks).
        // minRounds's declared default of 1 (SeedF3B.cs) is met by the single
        // flown round, so the rounds gate passes and the tasks gate is reached.
        var competition = CompetitionAdopting(WithMinTasks(SeedF3B.Definition, 3), 12);
        competition = WithOneFlownRound(competition, "A");

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.notEnoughTasks");
    }

    [Fact]
    public void Finalise_counts_distinct_tasks_across_the_complete_rounds_and_passes_MinTasks_when_they_are_all_flown()
    {
        // The real F3B round: one flight each of A, B and C (F3B.2.1).
        var competition = CompetitionAdopting(WithMinTasks(SeedF3B.Definition, 3), 12);
        competition = WithOneFlownRound(competition, "A", "B", "C");

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Finalise_resolves_MinRounds_from_a_binding_rather_than_the_declared_default()
    {
        // F3B's minRounds: default 1, allowed [1, 5] — 5 at World/Continental
        // Championships (F3B.2.1 b). Bound to 5, one flown round is not enough.
        var competition = CompetitionAdopting(SeedF3B.Definition, 12);
        var bound = competition.BindParameter("minRounds", MeasuredValue.Of(5m), "CD Jane", At);
        bound.IsSuccess.Should().BeTrue();
        competition = competition.Apply(bound.Value);
        competition = WithOneFlownRound(competition, "A");

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.notEnoughRounds");
    }

    [Fact]
    public void Finalise_succeeds_at_revision_one_and_carries_the_declared_results_through()
    {
        var competition = DrawnF3J(rounds: 4, complete: 4);
        var declared = ImmutableArray.Create(
            new DeclaredResult { CompetitorRef = competition.Competitors[0].Id, Aggregate = 1000m, Placing = 1, Promoted = false },
            new DeclaredResult { CompetitorRef = competition.Competitors[1].Id, Aggregate = 987.5m, Placing = 2, Promoted = false });

        var result = competition.Finalise(declared, [], "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
        var finalisation = result.Value.Finalisation;
        finalisation.Scope.Should().Be(FinalisationScope.Competition);
        finalisation.Revision.Should().Be(1);
        finalisation.By.Should().Be("CD Jane");
        finalisation.At.Should().Be(At);
        finalisation.DeclaredResults.Should().Equal(declared);

        // Promotion is competition-scope finalisation's non-question: there is
        // no next phase to be promoted into (decision 2 in the plan).
        finalisation.DeclaredResults.Should().OnlyContain(r => !r.Promoted);

        competition = competition.Apply(result.Value);
        competition.Finalisations.Should().ContainSingle();
    }

    [Fact]
    public void Finalise_is_not_blocked_by_more_rounds_being_drawn_than_the_class_minimum()
    {
        // MinRounds is a floor, never a target: 5 drawn, 4 complete, F3J's 4 met.
        var competition = DrawnF3J(rounds: 5, complete: 4);

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
    }

    // ---------------------------------------------- team results (teams-mvp.md WI-7)

    /// <summary>One full declared team standing, shaped exactly as
    /// FinaliseCompetitionHandler maps a TeamStanding (teams-mvp.md decision 4:
    /// total, place, contributors, placing sum, best individual placing).</summary>
    private static ImmutableArray<DeclaredTeamResult> OneTeamResult =>
    [
        new DeclaredTeamResult
        {
            TeamRef = ScoringTeamId.New(),
            Name = "Eagles",
            Total = 1500m,
            Placing = 1,
            Contributors =
            [
                new DeclaredTeamContributor { CompetitorRef = CompetitorId.New(), Score = 600m, Placing = 1 },
                new DeclaredTeamContributor { CompetitorRef = CompetitorId.New(), Score = 500m, Placing = 2 },
            ],
            PlacingSum = 3,
            BestIndividualPlacing = 1,
        },
    ];

    private static Competition WithTeamClassification(Competition competition, bool enabled)
    {
        var configured = competition.ConfigureTeamClassification(enabled, "CD Jane", At);
        configured.IsSuccess.Should().BeTrue(configured.Code ?? "classification configured");
        return competition.Apply(configured.Value);
    }

    private static Competition WithScoringTeam(Competition competition)
    {
        var defined = competition.DefineScoringTeam(ScoringTeamId.New(), "Eagles", At);
        defined.IsSuccess.Should().BeTrue(defined.Code ?? "scoring team defined");
        return competition.Apply(defined.Value);
    }

    [Fact]
    public void Finalise_with_team_results_while_classification_is_absent_fails_with_teamResultsNotPermitted()
    {
        // Never configured: the classification has never been switched on, so
        // there is nothing for a declared team result to answer to.
        var competition = DrawnF3J(rounds: 4, complete: 4);

        var result = competition.Finalise(OneResult, OneTeamResult, "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.teamResultsNotPermitted");
    }

    [Fact]
    public void Finalise_with_team_results_while_classification_is_disabled_fails_with_teamResultsNotPermitted()
    {
        // Explicitly disabled: same refusal, a different way of getting there —
        // both halves of "disabled/absent" are the same state to the decide.
        var competition = WithTeamClassification(DrawnF3J(rounds: 4, complete: 4), enabled: false);

        var result = competition.Finalise(OneResult, OneTeamResult, "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.teamResultsNotPermitted");
    }

    [Fact]
    public void Finalise_with_classification_enabled_and_teams_defined_but_no_team_results_fails_with_teamResultsMissing()
    {
        // The handler derives standings for EVERY defined team, so an empty
        // declaration beside a running classification with teams can only be
        // caller error — the decide refuses it rather than silently finalising
        // without the team half of the declaration.
        var competition = WithScoringTeam(WithTeamClassification(DrawnF3J(rounds: 4, complete: 4), enabled: true));

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("finalise.teamResultsMissing");
    }

    [Fact]
    public void Finalise_with_classification_enabled_but_no_teams_defined_permits_empty_team_results()
    {
        // No teams defined means there is nothing to declare — a state, not an
        // error, matching the engine's own empty-standings stance. The missing
        // check above is armed only when teams exist.
        var competition = WithTeamClassification(DrawnF3J(rounds: 4, complete: 4), enabled: true);

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.Finalisation.DeclaredTeamResults.Should().BeEmpty();
    }

    [Fact]
    public void Finalise_with_classification_disabled_permits_empty_team_results()
    {
        var competition = WithTeamClassification(DrawnF3J(rounds: 4, complete: 4), enabled: false);

        var result = competition.Finalise(OneResult, [], "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
        result.Value.Finalisation.DeclaredTeamResults.Should().BeEmpty();
    }

    [Fact]
    public void Finalise_carries_the_declared_team_results_through_to_the_finalisation()
    {
        var competition = WithScoringTeam(WithTeamClassification(DrawnF3J(rounds: 4, complete: 4), enabled: true));

        // Hoisted once: OneTeamResult mints fresh ids per access, and the
        // comparison below is against the very instances that were declared.
        var teamResults = OneTeamResult;

        var result = competition.Finalise(OneResult, teamResults, "CD Jane", At);

        result.IsSuccess.Should().BeTrue();
        var finalisation = result.Value.Finalisation;

        // Field-by-field, not record equality: DeclaredTeamResult nests an
        // ImmutableArray, whose equality is reference-based — the contributors
        // are compared separately below.
        finalisation.DeclaredTeamResults.Should().HaveCount(1);
        var declaredTeam = finalisation.DeclaredTeamResults[0];
        declaredTeam.TeamRef.Should().Be(teamResults[0].TeamRef);
        declaredTeam.Name.Should().Be("Eagles");
        declaredTeam.Total.Should().Be(1500m);
        declaredTeam.Placing.Should().Be(1);
        declaredTeam.PlacingSum.Should().Be(3);
        declaredTeam.BestIndividualPlacing.Should().Be(1);
        declaredTeam.Contributors.Should().Equal(teamResults[0].Contributors);

        competition = competition.Apply(result.Value);
        competition.Finalisations.Should().ContainSingle();
    }

    [Fact]
    public void Phase_scope_finalisations_carry_no_team_results()
    {
        // Phase-scope finalisation names who was PROMOTED, and never carries
        // team results in the MVP (teams-mvp.md §Finalisation). No decide emits
        // one today; the pin is the record's own default — a phase-scoped
        // Finalisation without the field set carries an empty array, never a
        // leftover from some competition-scope sibling.
        var phaseScope = new Finalisation
        {
            Scope = FinalisationScope.Phase,
            Revision = 1,
            By = "CD Jane",
            At = At,
            DeclaredResults = OneResult,
        };

        phaseScope.DeclaredTeamResults.Should().BeEmpty();
    }
}
