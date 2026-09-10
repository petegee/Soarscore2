using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-2 of kanban/in-progress/operational-tie-break-resolution.md: the ranking
/// engine consumes recorded tie-break outcomes (D4). A halted group matching
/// an outcome (directive kind + exact competitor set, last wins) is ordered by
/// the recorded placing with dormant comparator rungs separating residual
/// recorded ties, and surfaces no PendingTieBreak; non-matching outcomes are
/// inert — today's behaviour verbatim.
/// </summary>
public class RankingEngineTieBreakOutcomeTests
{
    private static FinalCompetitorScore Score(
        string ref_, decimal score, decimal preDrop, decimal bestDropped = 0m, bool disqualified = false) =>
        new(ref_, score, preDrop, bestDropped, disqualified);

    private static ResolvedTieBreakOutcome Outcome(
        TieBreakDirective directive, params (string Ref, int Place)[] placings) =>
        new(directive, placings.ToImmutableDictionary(p => p.Ref, p => p.Place));

    private static TieBreakContext Context(
        ImmutableArray<TieBreakDirective> directives,
        ImmutableArray<ResolvedTieBreakOutcome> outcomes) =>
        new(directives, ImmutableDictionary<string, int>.Empty, outcomes);

    [Fact]
    public void Matching_outcome_reorders_the_group_and_clears_pending()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = Context(
            [new AdditionalFullRound()],
            [Outcome(new AdditionalFullRound(), ("B", 1), ("A", 2))]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["B"].Should().Be(1);
        result.Placings["A"].Should().Be(2);
        result.Placings["C"].Should().Be(3);
        result.PendingTieBreaks.Should().BeEmpty();
    }

    [Fact]
    public void Recorded_ties_are_separated_by_the_dormant_comparator()
    {
        // F3F.1.13's shape: classificationRounds first, bestDroppedScore
        // dormant behind it. The outcome leaves A and B tied; the dormant
        // rung orders them — A's higher dropped cell wins.
        var scores = new[]
        {
            Score("A", 1000m, 1050m, bestDropped: 100m),
            Score("B", 1000m, 1050m, bestDropped: 80m),
        }.ToImmutableArray();

        var context = Context(
            [new ClassificationRounds(), new BestDroppedScore()],
            [Outcome(new ClassificationRounds(), ("A", 1), ("B", 1))]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(2);
        result.PendingTieBreaks.Should().BeEmpty();
    }

    [Fact]
    public void Recorded_ties_share_when_no_dormant_rungs_exist()
    {
        // F3K's shape: tiebreakFlyoff last, nothing dormant behind it. A and
        // B reach the fly-off tied on Score AND dropped cell; the outcome
        // leaves them tied, so they share the skipped place.
        var scores = new[]
        {
            Score("A", 1000m, 1100m, bestDropped: 100m),
            Score("B", 1000m, 1050m, bestDropped: 100m),
            Score("C", 1000m, 1050m, bestDropped: 80m),
            Score("D", 900m, 900m),
        }.ToImmutableArray();

        var context = Context(
            [new BestDroppedScore(), new TieBreakFlyoff()],
            [Outcome(new TieBreakFlyoff(), ("A", 1), ("B", 1))]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);
        result.Placings["D"].Should().Be(4);
        result.PendingTieBreaks.Should().BeEmpty();
    }

    [Fact]
    public void Supersession_two_matching_outcomes_last_wins()
    {
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = Context(
            [new AdditionalFullRound()],
            [
                Outcome(new AdditionalFullRound(), ("A", 1), ("B", 2)),
                Outcome(new AdditionalFullRound(), ("B", 1), ("A", 2)),
            ]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["B"].Should().Be(1);
        result.Placings["A"].Should().Be(2);
        result.Placings["C"].Should().Be(3);
        result.PendingTieBreaks.Should().BeEmpty();
    }

    [Fact]
    public void Set_mismatch_is_inert()
    {
        // The outcome names {A, C} but the halted group is {A, B} (trap 3:
        // an outcome spanning the union matches neither group).
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = Context(
            [new AdditionalFullRound()],
            [Outcome(new AdditionalFullRound(), ("A", 1), ("C", 2))]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);
        result.PendingTieBreaks.Length.Should().Be(1);
        result.PendingTieBreaks[0].CompetitorRefs.Should().BeEquivalentTo("A", "B");
    }

    [Fact]
    public void Directive_mismatch_is_inert()
    {
        // A RulesAmendment-style context: the recorded fly-off outcome's kind
        // no longer matches the halt rung, so the tie surfaces as pending.
        var scores = new[]
        {
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 900m, 900m),
        }.ToImmutableArray();

        var context = Context(
            [new AdditionalFullRound()],
            [Outcome(new TieBreakFlyoff(), ("A", 1), ("B", 2))]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["A"].Should().Be(1);
        result.Placings["B"].Should().Be(1);
        result.Placings["C"].Should().Be(3);
        result.PendingTieBreaks.Length.Should().Be(1);
        result.PendingTieBreaks[0].Directive.Should().BeOfType<AdditionalFullRound>();
    }

    [Fact]
    public void Group_span_and_outsiders_untouched()
    {
        // The tied group sits mid-field: the leader above and the trailer
        // below keep their places while the group resolves within its span.
        var scores = new[]
        {
            Score("Leader", 1100m, 1100m),
            Score("A", 1000m, 1100m),
            Score("B", 1000m, 1050m),
            Score("C", 1000m, 1040m),
            Score("Trailer", 800m, 800m),
        }.ToImmutableArray();

        var context = Context(
            [new AdditionalFullRound()],
            [Outcome(new AdditionalFullRound(), ("C", 1), ("B", 2), ("A", 3))]);

        var result = RankingEngine.Rank(scores, null, null, context);

        result.Placings["Leader"].Should().Be(1);
        result.Placings["C"].Should().Be(2);
        result.Placings["B"].Should().Be(3);
        result.Placings["A"].Should().Be(4);
        result.Placings["Trailer"].Should().Be(5);
        result.PendingTieBreaks.Should().BeEmpty();
    }
}
