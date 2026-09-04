using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for RankingEngine (WI-8; tie-break ladder per
/// kanban/in-progress/tie-break-policy-in-class-definition.md WI-1).
/// Tests simple ranking, ties, disqualified exclusion, and empty input on the
/// absent-policy display ladder, plus the stated-policy ladder: comparator
/// rungs, operational surfacing, silence, dormancy and exhaustion.
/// </summary>
public class RankingEngineTests
{
    private static FinalCompetitorScore Score(
        string ref_, decimal score, decimal preDrop, decimal bestDropped = 0m, bool disqualified = false) =>
        new(ref_, score, preDrop, bestDropped, disqualified);

    // ------------------------------------------------------ Simple ranking

    [Fact]
    public void Simple_ranking_scores_1000_900_800_give_1_2_3()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1000m),
            Score("B", 900m, 900m),
            Score("C", 800m, 800m),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

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
            Score("A", 1000m, 1000m),
            Score("B", 900m, 900m),
            Score("C", 900m, 900m),
            Score("D", 800m, 800m),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

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
            Score("A", 1000m, 1000m),
            Score("B", 900m, 900m, disqualified: true),
            Score("C", 800m, 800m),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

        result.Placings["A"].Should().Be(1);
        result.Placings.ContainsKey("B").Should().BeFalse();
        result.Placings["C"].Should().Be(2);
    }

    // ------------------------------------------------------ Empty input

    [Fact]
    public void Empty_input_produces_empty_output()
    {
        var result = RankingEngine.Rank(
            ImmutableArray<FinalCompetitorScore>.Empty, null, null, TieBreakContext.Display);

        result.Placings.Should().BeEmpty();
        result.PendingTieBreaks.Should().BeEmpty();
    }

    // ------------------------------------------------------ All disqualified

    [Fact]
    public void All_disqualified_produces_empty_placings()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1000m, disqualified: true),
            Score("B", 900m, 900m, disqualified: true),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

        result.Placings.Should().BeEmpty();
    }

    // ------------------------------------------------------ PreDropScore ladder

    [Fact]
    public void Score_tie_breaks_on_higher_PreDropScore()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1000m),
            Score("B", 1000m, 1100m),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

        result.Placings["B"].Should().Be(1);
        result.Placings["A"].Should().Be(2);
    }

    [Fact]
    public void Full_tie_on_both_keys_shares_place_and_skips()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1000m),
            Score("B", 1000m, 1000m),
            Score("C", 900m, 950m),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);
    }

    [Fact]
    public void Differing_Scores_ignore_PreDropScore()
    {
        var scores = new[]
        {
            Score("A", 1000m, 500m),
            Score("B", 900m, 5000m),
        }.ToImmutableArray();

        var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(2);
    }

    // ------------------------------------------------------ BestDroppedScore comparator

    [Fact]
    public void BestDroppedScore_is_the_max_dropped_cell_not_the_sum()
    {
        // Two-drop witness (D4): A dropped {100, 50} (sum 150, max 100),
        // B dropped {80, 80} (sum 160, max 80). The PreDropScore countback
        // would order B first (160 > 150); the stated rung orders A first
        // (100 > 80). Same Score, same policy — the keys diverge exactly
        // where drops are plural.
        var scores = new[]
        {
            Score("A", 900m, 1050m, bestDropped: 100m),
            Score("B", 900m, 1060m, bestDropped: 80m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new BestDroppedScore()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(2);

        // And the fallback ladder orders them the other way round — the
        // divergence is the point of the witness.
        var fallback = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);
        fallback.Placings["B"].Should().Be(1);
        fallback.Placings["A"].Should().Be(2);
    }

    // ------------------------------------------------------ QualifyingPosition comparator

    [Fact]
    public void QualifyingPosition_higher_prior_placing_wins()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1000m),
            Score("B", 1000m, 1000m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new QualifyingPosition { SourcePhaseOrdinal = 1 }],
            ImmutableDictionary<string, int>.Empty.Add("A", 2).Add("B", 1));

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["B"].Should().Be(1); // prior placing 1 beats prior placing 2
        result.Placings["A"].Should().Be(2);
    }

    [Fact]
    public void Equal_qualifying_position_shares_with_no_pending_entry()
    {
        // F3J's fly-off: equal aggregate AND equal qualifying place share the
        // final place — the rulebook states nothing further, so the exhausted
        // comparator ladder is a settled shared place, NOT a surfaced rung.
        var scores = new[]
        {
            Score("A", 1000m, 1000m),
            Score("B", 1000m, 1000m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new QualifyingPosition { SourcePhaseOrdinal = 1 }],
            ImmutableDictionary<string, int>.Empty.Add("A", 1).Add("B", 1).Add("C", 2));

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);
        result.PendingTieBreaks.Should().BeEmpty();
    }

    // ------------------------------------------------------ Stated silence (D8)

    [Fact]
    public void UndefinedRequiresRuling_shares_and_suppresses_the_countback()
    {
        // B's higher PreDropScore would win the display countback; the stated
        // silence means nothing has ruled, so the software must not decide:
        // shared places, and the ruling requirement surfaces.
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new UndefinedRequiresRuling()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);

        result.PendingTieBreaks.Length.Should().Be(1);
        result.PendingTieBreaks[0].CompetitorRefs.Should().BeEquivalentTo("A", "B");
        result.PendingTieBreaks[0].Directive.Should().BeOfType<UndefinedRequiresRuling>();
    }

    // --------------------------------------- Stated settlement (Pete's 2026-09-04 NZ ruling)

    [Fact]
    public void EqualPlaces_shares_and_settles_nothing_pending()
    {
        // Pete's 2026-09-04 NZ ruling: ties are never broken — "1st equal"
        // is the outcome at every placing. A's higher PreDropScore would win
        // the display countback; the stated settlement refuses it, and unlike
        // UndefinedRequiresRuling nothing is surfaced: there is no ruling to
        // make and nobody to fly.
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new EqualPlaces()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);

        result.PendingTieBreaks.Should().BeEmpty();
    }

    [Fact]
    public void EqualPlaces_halts_evaluation_rungs_after_it_stay_dormant()
    {
        // Adoption-invalid (check 20 refuses the mix) but engine-visible: the
        // first rung settles the group, so a later operational rung must not
        // surface through it — the same dormancy the operational rungs get.
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new EqualPlaces(), new TieBreakFlyoff()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);

        result.PendingTieBreaks.Should().BeEmpty();
    }

    // ------------------------------------------------------ Operational rungs (D5)

    [Fact]
    public void AdditionalFullRound_surfaces_and_never_separates()
    {
        // F3B's shape: a Score tie's answer is fly, not count back — the
        // stated list supersedes rung 2, so A's higher PreDropScore decides
        // nothing here.
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new AdditionalFullRound()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);

        result.PendingTieBreaks.Length.Should().Be(1);
        result.PendingTieBreaks[0].CompetitorRefs.Should().BeEquivalentTo("A", "B");
        result.PendingTieBreaks[0].Directive.Should().BeOfType<AdditionalFullRound>();
    }

    [Fact]
    public void ClassificationRounds_first_hides_the_comparator_fallback_behind_it()
    {
        // F3F.1.13's shape: operational first, comparator as the "if this is
        // not possible" fallback. The rungs after the halt stay dormant —
        // even a differing BestDroppedScore cannot separate the tie, because
        // the engine never evaluates past the halt.
        var scores = new[]
        {
            Score("A", 1000m, 1050m, bestDropped: 100m),
            Score("B", 1000m, 1050m, bestDropped: 80m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new ClassificationRounds(), new BestDroppedScore()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);

        result.PendingTieBreaks.Length.Should().Be(1);
        result.PendingTieBreaks[0].Directive.Should().BeOfType<ClassificationRounds>();
    }

    [Fact]
    public void TieBreakFlyoff_after_a_comparator_halts_only_what_the_comparator_left_tied()
    {
        // F3K's preliminary: bestDroppedScore narrows, and a group it cannot
        // separate goes to the one-task tie-break fly-off.
        var scores = new[]
        {
            Score("A", 1000m, 1100m, bestDropped: 100m),
            Score("B", 1000m, 1050m, bestDropped: 100m),
            Score("C", 1000m, 1050m, bestDropped: 80m),
            Score("D", 900m, 900m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new BestDroppedScore(), new TieBreakFlyoff()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1); // best-dropped tie → halts at the fly-off
        result.Placings["C"].Should().Be(3); // separated by the comparator
        result.Placings["D"].Should().Be(4);

        result.PendingTieBreaks.Length.Should().Be(1);
        result.PendingTieBreaks[0].CompetitorRefs.Should().BeEquivalentTo("A", "B");
        result.PendingTieBreaks[0].Directive.Should().BeOfType<TieBreakFlyoff>();
    }

    // ------------------------------------------------------ Disqualified under a policy

    [Fact]
    public void Disqualified_excluded_even_when_the_policy_would_surface_a_rung()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1000m),
            Score("B", 1000m, 1000m, disqualified: true),
            Score("C", 800m, 800m),
        }.ToImmutableArray();

        var context = new TieBreakContext(
            [new AdditionalFullRound()],
            ImmutableDictionary<string, int>.Empty);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings.ContainsKey("B").Should().BeFalse();
        result.Placings["C"].Should().Be(2);
        // A is alone after the exclusion — no tie group, nothing pending.
        result.PendingTieBreaks.Should().BeEmpty();
    }
}
