using System.Linq;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
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
        var drawn = competition.DrawPhase(1, DateTimeOffset.UtcNow);
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
        var drawn = competition.DrawPhase(1, DateTimeOffset.UtcNow);
        competition = competition.Apply(drawn.Value);

        var beforeFlying = competition.BindParameter("windSpeed", MeasuredValue.Of(4.5m), "CD", DateTimeOffset.UtcNow);
        beforeFlying.IsSuccess.Should().BeTrue();

        var competitionSetup = competition.BindParameter("flyoffMinRounds", MeasuredValue.Of(3m), "CD", DateTimeOffset.UtcNow);
        competitionSetup.IsFailure.Should().BeTrue();
        competitionSetup.Code.Should().Be("competition.parameter.frozen");
    }
}
