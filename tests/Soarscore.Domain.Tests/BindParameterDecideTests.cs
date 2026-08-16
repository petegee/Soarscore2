using System.Linq;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.BindParameter"/> —
/// kanban/completed/bind-parameter-steel-thread-plan.md WI-1. Mirrors
/// PhaseDrawnDecideTests's style: real seed-corpus ClassDefinitions
/// (Soarscore.SeedData) wherever the corpus already has the shape a case
/// needs, a hand-built fixture only for the one shape it does not — a class
/// with both a CompetitionSetup and a BeforeFlying parameter that can also
/// be drawn, needed to tell the freeze check's two branches apart.
/// </summary>
public class BindParameterDecideTests
{
    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var at = DateTimeOffset.UtcNow;
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = at,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Bind Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, at);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), at);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    /// <summary>
    /// F3J plus one synthetic BeforeFlying parameter ("windSpeed"), so a
    /// single drawable definition carries both binding points — F3J's own
    /// Parameters are all CompetitionSetup (SeedF3J.cs), and F5K/NZ-N/F3F
    /// (the corpus's BeforeFlying examples) cannot be drawn by today's draw
    /// (catalogue choice) or carry no CompetitionSetup parameter to contrast
    /// against.
    /// </summary>
    private static ClassDefinition WithBeforeFlyingParameter(ClassDefinition definition) =>
        definition with
        {
            Parameters = definition.Parameters.Add(
                new Parameter { Name = "windSpeed", Kind = MeasuredKind.Number, BoundAt = ParameterBindingPoint.BeforeFlying }),
        };

    /// <summary>
    /// Real corpus F3K (Soarscore.SeedData.SeedF3K), drawn 5 distinct
    /// catalogue tasks A-E, one per round — the round-scope tests' fixture:
    /// round 1's task (A) references workingTime.A, round 2's task (B) does
    /// not (PhaseDrawnDecideTests.cs's own real-corpus draw pattern).
    /// </summary>
    private static Competition DrawnF3K()
    {
        var competition = CompetitionAdopting(SeedF3K.Definition, 10);
        var drawn = competition.DrawPhase(5, ["A", "B", "C", "D", "E"], DateTimeOffset.UtcNow);
        drawn.IsSuccess.Should().BeTrue();
        return competition.Apply(drawn.Value);
    }

    [Fact]
    public void BindParameter_against_an_undeclared_name_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);

        var result = competition.BindParameter("notAThing", MeasuredValue.Of(5m), "CD", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.notDeclared");
    }

    [Fact]
    public void BindParameter_with_the_wrong_MeasuredKind_fails_with_a_stable_code()
    {
        // F3J's carryPenalties is a Flag parameter (SeedF3J.cs).
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);

        var result = competition.BindParameter("carryPenalties", MeasuredValue.Of(5m), "CD", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.kindMismatch");
    }

    [Fact]
    public void BindParameter_with_a_value_outside_AllowedValues_fails_with_a_stable_code()
    {
        // F5K's nlh: allowed [60, 70] (SeedF5K.cs, 5.5.10.3).
        var competition = CompetitionAdopting(SeedF5K.Definition, 6);

        var result = competition.BindParameter("nlh", MeasuredValue.Of(65m), "CD", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.valueNotAllowed");
    }

    [Fact]
    public void BindParameter_a_CompetitionSetup_parameter_after_a_phase_is_drawn_fails_with_a_stable_code()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 12);
        var drawn = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);
        competition = competition.Apply(drawn.Value);

        // F3J's flyoffMinRounds: CompetitionSetup, no default (SeedF3J.cs).
        var result = competition.BindParameter("flyoffMinRounds", MeasuredValue.Of(3m), "CD", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.frozen");
    }

    [Fact]
    public void BindParameter_succeeds_and_carries_the_bound_value_through()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);
        var at = DateTimeOffset.UtcNow;

        var result = competition.BindParameter("flyoffMinRounds", MeasuredValue.Of(3m), "CD Jane", at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Binding.ParameterName.Should().Be("flyoffMinRounds");
        result.Value.Binding.BoundValue.Should().Be(MeasuredValue.Of(3m));
        result.Value.Binding.By.Should().Be("CD Jane");
        result.Value.Binding.At.Should().Be(at);
    }

    [Fact]
    public void BindParameter_rebinding_before_the_draw_succeeds_and_Apply_folds_both_bindings()
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);

        var first = competition.BindParameter("flyoffMinRounds", MeasuredValue.Of(3m), "CD", DateTimeOffset.UtcNow);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        var second = competition.BindParameter("flyoffMinRounds", MeasuredValue.Of(5m), "CD", DateTimeOffset.UtcNow);
        second.IsSuccess.Should().BeTrue();
        competition = competition.Apply(second.Value);

        competition.ParameterBindings.Length.Should().Be(2);
        competition.ParameterBindings.Select(b => b.BoundValue).Should().Equal(MeasuredValue.Of(3m), MeasuredValue.Of(5m));
    }

    [Fact]
    public void BindParameter_a_BeforeFlying_parameter_after_a_phase_is_drawn_succeeds_while_a_CompetitionSetup_one_fails()
    {
        var definition = WithBeforeFlyingParameter(SeedF3J.Definition);
        var competition = CompetitionAdopting(definition, 12);
        var drawn = competition.DrawPhase(1, [], DateTimeOffset.UtcNow);
        competition = competition.Apply(drawn.Value);

        var beforeFlying = competition.BindParameter("windSpeed", MeasuredValue.Of(4.5m), "CD", DateTimeOffset.UtcNow);
        beforeFlying.IsSuccess.Should().BeTrue();

        var competitionSetup = competition.BindParameter("flyoffMinRounds", MeasuredValue.Of(3m), "CD", DateTimeOffset.UtcNow);
        competitionSetup.IsFailure.Should().BeTrue();
        competitionSetup.Code.Should().Be("competition.parameter.frozen");
    }

    // Round-scope tests — kanban/completed/per-round-parameter-bindings-plan.md.

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, 1)]
    public void BindParameter_round_scoped_with_only_one_of_phase_or_round_given_fails_with_a_stable_code(int? phaseOrdinal, int? roundOrdinal)
    {
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);

        var result = competition.BindParameter(
            "flyoffMinRounds", MeasuredValue.Of(3m), "CD", DateTimeOffset.UtcNow, phaseOrdinal, roundOrdinal);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.roundScopeIncomplete");
    }

    [Fact]
    public void BindParameter_round_scoped_against_a_non_PerRound_parameter_fails_with_a_stable_code_even_when_no_phase_is_drawn()
    {
        // flyoffMinRounds is CompetitionSetup (SeedF3J.cs) — the BoundAt check
        // fires before the round-exists check, so no draw is needed to prove it.
        var competition = CompetitionAdopting(SeedF3J.Definition, 6);

        var result = competition.BindParameter(
            "flyoffMinRounds", MeasuredValue.Of(3m), "CD", DateTimeOffset.UtcNow, phaseOrdinal: 0, roundOrdinal: 1);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.roundScopeNotPermitted");
    }

    [Fact]
    public void BindParameter_round_scoped_against_a_round_that_was_never_drawn_fails_with_a_stable_code()
    {
        var competition = DrawnF3K();

        var result = competition.BindParameter(
            "workingTime.A", MeasuredValue.Of(420m), "CD", DateTimeOffset.UtcNow, phaseOrdinal: 0, roundOrdinal: 99);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.roundNotFound");
    }

    [Fact]
    public void BindParameter_round_scoped_against_a_round_whose_task_does_not_consume_the_parameter_fails_with_a_stable_code()
    {
        // Round 2's task is B (F3K.11.2), which references workingTime.B, not workingTime.A.
        var competition = DrawnF3K();

        var result = competition.BindParameter(
            "workingTime.A", MeasuredValue.Of(420m), "CD", DateTimeOffset.UtcNow, phaseOrdinal: 0, roundOrdinal: 2);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.notConsumedByTask");
    }

    [Fact]
    public void BindParameter_round_scoped_against_a_round_that_has_left_Drawn_fails_with_a_stable_code()
    {
        var competition = DrawnF3K();
        competition = competition.Apply(new TaskRoundCompleted(
            PhaseOrdinal: 0, RoundOrdinal: 1, TaskRoundOrdinal: 1, DateTimeOffset.UtcNow));

        var result = competition.BindParameter(
            "workingTime.A", MeasuredValue.Of(420m), "CD", DateTimeOffset.UtcNow, phaseOrdinal: 0, roundOrdinal: 1);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.parameter.roundFrozen");
    }

    [Fact]
    public void BindParameter_round_scoped_against_the_round_whose_task_consumes_it_succeeds_and_carries_the_scope_through()
    {
        // Round 1's task is A (F3K.11.1), which references workingTime.A.
        var competition = DrawnF3K();
        var at = DateTimeOffset.UtcNow;

        var result = competition.BindParameter(
            "workingTime.A", MeasuredValue.Of(420m), "CD Jane", at, phaseOrdinal: 0, roundOrdinal: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Binding.ParameterName.Should().Be("workingTime.A");
        result.Value.Binding.BoundValue.Should().Be(MeasuredValue.Of(420m));
        result.Value.Binding.PhaseOrdinal.Should().Be(0);
        result.Value.Binding.RoundOrdinal.Should().Be(1);
    }

    [Fact]
    public void A_round_scoped_binding_wins_for_its_own_round_and_the_unscoped_binding_still_governs_elsewhere()
    {
        // Round 1's task is A; round 5's task is E — both consume a distinct
        // workingTime.* parameter, so binding workingTime.A unscoped and then
        // again scoped to round 1 lets one flattening query at round 1 prove
        // the round-scoped value wins there, while a same-named query is
        // meaningless at round 5 (E doesn't reference workingTime.A at all) —
        // instead, resolve E's own PerRound parameter (workingTime.E) at round
        // 5 with no round-scoped binding for IT, and see the unscoped default
        // still stand: round-scoping one parameter for round 1 has zero effect
        // on a different parameter's resolution at a different round.
        var competition = DrawnF3K();

        var unscoped = competition.BindParameter("workingTime.A", MeasuredValue.Of(420m), "CD", DateTimeOffset.UtcNow);
        unscoped.IsSuccess.Should().BeTrue();
        competition = competition.Apply(unscoped.Value);

        var roundScoped = competition.BindParameter(
            "workingTime.A", MeasuredValue.Of(600m), "CD", DateTimeOffset.UtcNow, phaseOrdinal: 0, roundOrdinal: 1);
        roundScoped.IsSuccess.Should().BeTrue();
        competition = competition.Apply(roundScoped.Value);

        var atRound1 = ScoringService.FlattenParameterBindings(competition.ParameterBindings, phaseOrdinal: 0, roundOrdinal: 1);
        atRound1["workingTime.A"].Number.Should().Be(600m);

        // workingTime.E has no binding at all here — round 5's flatten falls
        // straight through to the class's declared default (600, SeedF3K.cs),
        // proving round 1's binding of a DIFFERENT parameter left it untouched.
        var atRound5 = ScoringService.FlattenParameterBindings(competition.ParameterBindings, phaseOrdinal: 0, roundOrdinal: 5);
        atRound5.Should().NotContainKey("workingTime.E");
    }
}
