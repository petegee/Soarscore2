using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-5 invariant 5 (kanban/completed/scoring-steel-thread-plan.md): for any set of
/// FinalCompetitorScore, RankingEngine.Rank's placings are drawn from 1..n, a
/// higher score never receives a worse placing, and equal scores receive
/// equal placings. RankingEngineTests already covers example cases (ties,
/// disqualification, empty input); this is the general property CsCheck
/// checks across generated inputs, alongside it rather than in it.
/// </summary>
public class RankingEnginePropertyTests
{
    private static readonly Gen<decimal> ScoreValue = Gen.Int[-100_000, 100_000].Select(i => i / 100m);

    private static readonly Gen<(decimal Score, bool Disqualified)> ScoreEntry =
        from score in ScoreValue
        from disqualified in Gen.Bool
        select (score, disqualified);

    [Fact]
    public void Placings_are_a_consistent_total_order()
    {
        ScoreEntry.Array[1, 20].Sample(entries =>
        {
            var scores = entries
                .Select((e, i) => new FinalCompetitorScore($"C{i}", e.Score, e.Disqualified))
                .ToImmutableArray();

            var result = RankingEngine.Rank(scores, null, null);

            var active = scores.Where(s => !s.Disqualified).ToImmutableArray();

            // Every non-disqualified competitor is placed; no disqualified
            // competitor is.
            result.Placings.Keys.Should().BeEquivalentTo(active.Select(s => s.CompetitorRef));

            if (active.IsEmpty)
            {
                return;
            }

            // Drawn from 1..n (n = the number placed): a placing never
            // exceeds the field actually ranked, however many tie groups
            // precede it.
            result.Placings.Values.Should().OnlyContain(p => p >= 1 && p <= active.Length);

            // A higher score never receives a numerically worse (larger)
            // placing than a lower one; equal scores receive equal placings —
            // together, a consistent total order.
            foreach (var a in active)
            {
                foreach (var b in active)
                {
                    if (a.Score > b.Score)
                    {
                        result.Placings[a.CompetitorRef].Should().BeLessThan(result.Placings[b.CompetitorRef]);
                    }
                    else if (a.Score == b.Score)
                    {
                        result.Placings[a.CompetitorRef].Should().Be(result.Placings[b.CompetitorRef]);
                    }
                }
            }
        });
    }
}
