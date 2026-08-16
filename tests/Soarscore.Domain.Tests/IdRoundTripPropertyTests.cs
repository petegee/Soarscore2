using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-5 invariant 6 (kanban/completed/scoring-steel-thread-plan.md, finding 3):
/// stringify-then-parse is the identity for <see cref="CompetitorId"/> and
/// <see cref="GroupId"/> — the property that lets the scoring engine keep
/// speaking <c>string</c> (the Guid's "D" form) rather than retyping
/// ScoringResultTypes against Soarscore.Domain.Competitions, since the
/// Application-layer mapping back to typed ids (finding 3's decision) is
/// total and lossless in both directions only if this holds.
/// </summary>
public class IdRoundTripPropertyTests
{
    [Fact]
    public void CompetitorId_stringify_then_parse_is_the_identity()
    {
        Gen.Guid.Sample(g =>
        {
            var id = new CompetitorId(g);
            var roundTripped = CompetitorId.Parse(id.ToString(), null);
            roundTripped.Should().Be(id);
        });
    }

    [Fact]
    public void GroupId_stringify_then_parse_is_the_identity()
    {
        Gen.Guid.Sample(g =>
        {
            var id = new GroupId(g);
            var roundTripped = GroupId.Parse(id.ToString(), null);
            roundTripped.Should().Be(id);
        });
    }
}
