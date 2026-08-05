using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for RankingEngine (WI-8).
/// Tests simple ranking, ties, disqualified exclusion, and empty input.
/// </summary>
public class RankingEngineTests
{
    // ------------------------------------------------------ Simple ranking

    [Fact]
    public void Simple_ranking_scores_1000_900_800_give_1_2_3()
    {
        var scores = new[]
        {
            new FinalCompetitorScore("A", 1000m, false),
            new FinalCompetitorScore("B", 900m, false),
            new FinalCompetitorScore("C", 800m, false),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(2);
        result.Placings["C"].Should().Be(3);
    }

    // ------------------------------------------------------ Ties

    [Fact]
    public void Ties_share_place_and_skip()
    {
        var scores = new[]
        {
            new FinalCompetitorScore("A", 1000m, false),
            new FinalCompetitorScore("B", 900m, false),
            new FinalCompetitorScore("C", 900m, false),
            new FinalCompetitorScore("D", 800m, false),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(2);
        result.Placings["C"].Should().Be(2);
        result.Placings["D"].Should().Be(4); // skips 3
    }

    // ------------------------------------------------------ Disqualified

    [Fact]
    public void Disqualified_excluded_from_placings()
    {
        var scores = new[]
        {
            new FinalCompetitorScore("A", 1000m, false),
            new FinalCompetitorScore("B", 900m, true),  // disqualified
            new FinalCompetitorScore("C", 800m, false),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null);

        result.Placings["A"].Should().Be(1);
        result.Placings.ContainsKey("B").Should().BeFalse();
        result.Placings["C"].Should().Be(2);
    }

    // ------------------------------------------------------ Empty input

    [Fact]
    public void Empty_input_produces_empty_output()
    {
        var result = RankingEngine.Rank(
            ImmutableArray<FinalCompetitorScore>.Empty, null, null);

        result.Placings.Should().BeEmpty();
    }

    // ------------------------------------------------------ All disqualified

    [Fact]
    public void All_disqualified_produces_empty_placings()
    {
        var scores = new[]
        {
            new FinalCompetitorScore("A", 1000m, true),
            new FinalCompetitorScore("B", 900m, true),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null);

        result.Placings.Should().BeEmpty();
    }
}
