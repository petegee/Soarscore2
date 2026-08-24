using AwesomeAssertions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Pure facts for <see cref="ReflightSelector.Select"/> — kanban/in-progress/
/// reflight-groups.md WI-6a, one fact per ReflightSelection × role.
/// </summary>
public class ReflightSelectorTests
{
    private static ReflightRule Rule(ReflightSelection entitled, ReflightSelection others) => new()
    {
        EntitledScores = entitled,
        OthersScore = others,
    };

    [Fact]
    public void A_single_candidate_of_any_role_is_returned_unchanged()
    {
        var rule = Rule(ReflightSelection.Replacement, ReflightSelection.BetterOf);

        ReflightSelector.Select([(ReflightRole.Original, 100m)], rule).Value.Should().Be(100m);
        ReflightSelector.Select([(ReflightRole.Entitled, 200m)], rule).Value.Should().Be(200m);
        ReflightSelector.Select([(ReflightRole.Filler, 300m)], rule).Value.Should().Be(300m);
    }

    // ------------------------------------------------------- Entitled under the three selections

    [Fact]
    public void An_entitled_pair_under_Replacement_takes_the_re_flight_unchanged()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Entitled, 200m)],
            Rule(ReflightSelection.Replacement, ReflightSelection.BetterOf));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(200m);
    }

    [Fact]
    public void An_entitled_pair_under_BetterOf_takes_the_better_of_the_two()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 500m), (ReflightRole.Entitled, 200m)],
            Rule(ReflightSelection.BetterOf, ReflightSelection.BetterOf));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(500m);
    }

    [Fact]
    public void An_entitled_pair_under_NotPermitted_fails_with_score_refLightNotPermitted()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 100m), (ReflightRole.Entitled, 200m)],
            Rule(ReflightSelection.NotPermitted, ReflightSelection.NotPermitted));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightNotPermitted");
    }

    [Fact]
    public void An_entitled_pair_under_UndefinedRequiresRuling_fails_with_score_reflightRequiresRuling()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 100m), (ReflightRole.Entitled, 200m)],
            Rule(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightRequiresRuling");
    }

    // ---------------------------------------------------------- Filler under the three selections

    [Fact]
    public void A_filler_pair_under_Replacement_takes_the_re_flight_unchanged()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Filler, 300m)],
            Rule(ReflightSelection.Replacement, ReflightSelection.Replacement));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(300m);
    }

    [Fact]
    public void A_filler_pair_under_BetterOf_takes_the_better_of_the_two()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 400m), (ReflightRole.Filler, 600m)],
            Rule(ReflightSelection.Replacement, ReflightSelection.BetterOf));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(600m);
    }

    [Fact]
    public void A_filler_pair_under_NotPermitted_fails_with_score_refLightNotPermitted()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 100m), (ReflightRole.Filler, 200m)],
            Rule(ReflightSelection.Replacement, ReflightSelection.NotPermitted));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightNotPermitted");
    }

    [Fact]
    public void A_filler_pair_under_UndefinedRequiresRuling_fails_with_score_reflightRequiresRuling()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 100m), (ReflightRole.Filler, 200m)],
            Rule(ReflightSelection.Replacement, ReflightSelection.UndefinedRequiresRuling));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightRequiresRuling");
    }

    // --------------------------------------------- ruled selections (RR1/RR2′)
    // reflight-scoring-rulings.md WI-3a: a ruling fills silences only.

    [Fact]
    public void A_ruled_Replacement_for_an_entitled_pair_over_a_silent_rule_takes_the_re_flight()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Entitled, 200m)],
            Rule(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling),
            ReflightSelection.Replacement);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(200m);
    }

    [Fact]
    public void A_ruled_BetterOf_for_an_entitled_pair_over_a_silent_rule_takes_the_better()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Entitled, 200m)],
            Rule(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling),
            ReflightSelection.BetterOf);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1000m);
    }

    [Fact]
    public void A_ruled_Replacement_for_a_filler_pair_over_a_silent_rule_takes_the_re_flight()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Filler, 300m)],
            Rule(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling),
            ReflightSelection.Replacement);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(300m);
    }

    [Fact]
    public void A_ruled_BetterOf_for_a_filler_pair_over_a_silent_rule_takes_the_better()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Filler, 300m)],
            Rule(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling),
            ReflightSelection.BetterOf);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1000m);
    }

    [Fact]
    public void A_ruling_is_ignored_where_the_role_applicable_slot_is_defined()
    {
        // RR1: F3F's shape — OthersScore silent, EntitledScores Replacement.
        // A ruled BetterOf against an ENTITLED pair changes nothing: the
        // rulebook always beats the CD.
        var rule = Rule(ReflightSelection.Replacement, ReflightSelection.UndefinedRequiresRuling);

        var withoutRuling = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Entitled, 200m)], rule);
        var withRuling = ReflightSelector.Select(
            [(ReflightRole.Original, 1000m), (ReflightRole.Entitled, 200m)], rule, ReflightSelection.BetterOf);

        withRuling.IsSuccess.Should().BeTrue();
        withoutRuling.Value.Should().Be(200m);
        withRuling.Value.Should().Be(withoutRuling.Value);
    }

    [Fact]
    public void A_ruling_cannot_resurrect_a_NotPermitted_class()
    {
        var result = ReflightSelector.Select(
            [(ReflightRole.Original, 100m), (ReflightRole.Filler, 200m)],
            Rule(ReflightSelection.NotPermitted, ReflightSelection.NotPermitted),
            ReflightSelection.BetterOf);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightNotPermitted");
    }

    [Fact]
    public void Omitting_or_passing_null_for_the_ruled_selection_behaves_as_before()
    {
        // The default-parameter calls are byte-identical to the pre-ruling
        // behaviour — every existing fact above already exercises the omitted
        // form; this pins the explicit-null form too.
        var candidates = new List<(ReflightRole, decimal)> { (ReflightRole.Original, 500m), (ReflightRole.Filler, 700m) };
        var rule = Rule(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.BetterOf);

        ReflightSelector.Select(candidates, rule).Value.Should().Be(700m);
        ReflightSelector.Select(candidates, rule, null).Value.Should().Be(700m);
        ReflightSelector.Select(candidates, rule, null).Code.Should().Be(ReflightSelector.Select(candidates, rule).Code);
    }

    // ------------------------------------------------------- shape law

    [Theory]
    // Two same-role candidates — a corruption.
    [InlineData(ReflightRole.Original, ReflightRole.Original, false)]
    [InlineData(ReflightRole.Entitled, ReflightRole.Entitled, false)]
    [InlineData(ReflightRole.Filler, ReflightRole.Filler, false)]
    // The legal shapes.
    [InlineData(ReflightRole.Original, ReflightRole.Entitled, true)]
    [InlineData(ReflightRole.Original, ReflightRole.Filler, true)]
    public void ShapePermits_grants_only_the_two_legal_pairings(ReflightRole first, ReflightRole second, bool expected)
    {
        ReflightSelector.ShapePermits([first, second]).Should().Be(expected);
    }

    [Fact]
    public void ShapePermits_accepts_a_single_entry_and_rejects_three()
    {
        ReflightSelector.ShapePermits([ReflightRole.Original]).Should().BeTrue();
        ReflightSelector.ShapePermits([
            ReflightRole.Original, ReflightRole.Entitled, ReflightRole.Filler]).Should().BeFalse();
    }
}