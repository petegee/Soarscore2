using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Invariant T (kanban/in-progress/tie-break-policy-in-class-definition.md,
/// verbatim): "For any field of active competitors and any phase policy: (1)
/// placings realise the total preorder induced by the full ladder — Score
/// DESC, then each stated comparator rung in order — for any two active
/// competitors a, b: ladder(a) &gt;lex ladder(b) ⇒ place(a) &lt; place(b); equal full
/// ladders ⇒ equal place; placings drawn from 1..n with standard skip-ahead
/// numbering; (2) regression clause — a phase whose TieBreaks is empty
/// produces placings identical to the two-rung display ladder (Score DESC,
/// PreDropScore DESC) and an empty PendingTieBreaks; (3) comparator rungs only
/// refine Score ties — they never invert a Score ordering; (4) operational and
/// ruling rungs never separate a tie and surface exactly one PendingTieBreak
/// per halted group; an EqualPlaces rung is the stated settlement — it
/// separates nothing and surfaces nothing."
///
/// "Full ladder" is the evaluated ladder: Score, then the stated comparators
/// before the first non-comparator rung — rungs after a
/// halt stay dormant, so the preorder they leave is equality (shared places),
/// which is what clause 4 pins. Generated entries have
/// PreDropScore − Score = Σ dropped cells ≥ 0 and BestDroppedScore = max
/// dropped cell, the engine's real input class; generator cells run 1..2 so
/// both the single-drop (equivalence: max = sum) and multi-drop (divergence)
/// input classes are covered. Keeps the Invariant R lineage
/// (kanban/completed/ranking-secondary-rawscore-key.md), which it extends from
/// the two-key ladder to the stated-policy ladder. RankingEngineTests already
/// covers example cases (ties, disqualification, empty input); this is the
/// general property CsCheck checks across generated inputs, alongside it
/// rather than in it.
/// </summary>
public class RankingEnginePropertyTests
{
    private static readonly Gen<decimal> ScoreValue = Gen.Int[-100_000, 100_000].Select(i => i / 100m);

    private static readonly Gen<decimal> DroppedValue = Gen.Int[0, 100_000].Select(i => i / 100m);

    private sealed record Entry(
        string Ref, decimal Score, decimal PreDrop, decimal BestDropped, int Position, bool Disqualified);

    private static readonly Gen<(decimal Score, decimal[] Cells, int Position, bool Disq)> RawEntry =
        from score in ScoreValue
        from cells in DroppedValue.Array[1, 2]
        from position in Gen.Int[1, 20]
        from disqualified in Gen.Bool
        select (score, cells, position, disqualified);

    private static readonly Gen<TieBreakDirective> DirectiveGen =
        Gen.Int[0, 6].Select(k => k switch
        {
            0 => (TieBreakDirective)new BestDroppedScore(),
            1 => new QualifyingPosition { SourcePhaseOrdinal = 0 },
            2 => new AdditionalFullRound(),
            3 => new TieBreakFlyoff(),
            4 => new ClassificationRounds(),
            5 => new UndefinedRequiresRuling(),
            _ => new EqualPlaces(),
        });

    private static readonly Gen<ImmutableArray<TieBreakDirective>> PolicyGen =
        from stated in Gen.Bool
        from directives in DirectiveGen.Array[1, 4]
        select stated ? directives.ToImmutableArray() : ImmutableArray<TieBreakDirective>.Empty;

    private static ImmutableArray<Entry> Build(IList<(decimal Score, decimal[] Cells, int Position, bool Disq)> raw)
    {
        var builder = ImmutableArray.CreateBuilder<Entry>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var e = raw[i];
            builder.Add(new Entry(
                $"C{i}", e.Score, e.Score + e.Cells.Sum(), e.Cells.Max(), e.Position, e.Disq));
        }
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// The expected preorder, computed independently of the engine: rung 1
    /// Score DESC, then each evaluated comparator rung in order
    /// (BestDroppedScore DESC — higher is better; QualifyingPosition ASC — a
    /// better prior placing is the lower number). 0 means equal.
    /// </summary>
    private static int CompareExpected(Entry a, Entry b, ImmutableArray<TieBreakDirective> policy)
    {
        var c = b.Score.CompareTo(a.Score);
        if (c != 0) return c;

        foreach (var rung in policy)
        {
            switch (rung)
            {
                case BestDroppedScore:
                    c = b.BestDropped.CompareTo(a.BestDropped);
                    if (c != 0) return c;
                    break;
                case QualifyingPosition:
                    c = a.Position.CompareTo(b.Position);
                    if (c != 0) return c;
                    break;
            }
        }

        return 0;
    }

    /// <summary>Standard skip-ahead numbering over the expected preorder.</summary>
    private static ImmutableDictionary<string, int> ExpectedPlaces(
        ImmutableArray<Entry> entries, ImmutableArray<TieBreakDirective> policy)
    {
        var active = entries.Where(e => !e.Disqualified)
            .OrderBy(s => s, Comparer<Entry>.Create((a, b) => CompareExpected(a, b, policy)))
            .ToList();

        var placings = new Dictionary<string, int>();
        var place = 1;
        var i = 0;
        while (i < active.Count)
        {
            var j = i + 1;
            while (j < active.Count && CompareExpected(active[i], active[j], policy) == 0)
                j++;

            for (var k = i; k < j; k++)
                placings[active[k].Ref] = place;

            place += j - i;
            i = j;
        }

        return placings.ToImmutableDictionary();
    }

    /// <summary>Maximal same-ladder runs of size &gt; 1, in expected order.</summary>
    private static IReadOnlyList<IReadOnlyList<Entry>> ExpectedTieGroups(
        ImmutableArray<Entry> entries, ImmutableArray<TieBreakDirective> policy)
    {
        var active = entries.Where(e => !e.Disqualified)
            .OrderBy(s => s, Comparer<Entry>.Create((a, b) => CompareExpected(a, b, policy)))
            .ToList();

        var groups = new List<IReadOnlyList<Entry>>();
        var i = 0;
        while (i < active.Count)
        {
            var j = i + 1;
            while (j < active.Count && CompareExpected(active[i], active[j], policy) == 0)
                j++;

            if (j - i > 1)
                groups.Add(active.Skip(i).Take(j - i).ToList());

            i = j;
        }

        return groups;
    }

    // ------------------------------------------------ Invariant T, clauses 1, 3, 4

    [Fact]
    public void Placings_realise_the_stated_policy_ladder()
    {
        var combined =
            from raw in RawEntry.Array[1, 20]
            from policy in PolicyGen
            select (raw, policy);

        combined.Sample(testCase =>
        {
            var (raw, policy) = testCase;
            var entries = Build(raw);
            var scores = entries
                .Select(e => new FinalCompetitorScore(e.Ref, e.Score, e.PreDrop, e.BestDropped, e.Disqualified))
                .ToImmutableArray();
            var positions = entries.ToImmutableDictionary(e => e.Ref, e => e.Position);
            var context = new TieBreakContext(policy, positions);

            var result = RankingEngine.Rank(scores, null, null, context);

            var active = entries.Where(e => !e.Disqualified).ToImmutableArray();
            var expected = ExpectedPlaces(entries, policy);

            result.Placings.Keys.Should().BeEquivalentTo(active.Select(e => e.Ref));
            // (1) drawn from 1..n with standard skip-ahead numbering.
            result.Placings.Should().Equal(expected);

            // (1) and (3), pairwise: the ladder refines, never inverts.
            foreach (var a in active)
            {
                foreach (var b in active)
                {
                    var c = CompareExpected(a, b, policy);
                    if (c < 0)
                    {
                        result.Placings[a.Ref].Should().BeLessThan(result.Placings[b.Ref]);
                        if (a.Score > b.Score)
                            result.Placings[a.Ref].Should().BeLessThan(result.Placings[b.Ref]); // (3)
                    }
                    else if (c > 0)
                    {
                        result.Placings[a.Ref].Should().BeGreaterThan(result.Placings[b.Ref]);
                    }
                    else
                    {
                        // (1) equal full ladders ⇒ equal place; and (4) where
                        // the group halted, nothing separated it.
                        result.Placings[a.Ref].Should().Be(result.Placings[b.Ref]);
                    }
                }
            }

            // (4) operational and ruling rungs never separate a tie, and
            // exactly one PendingTieBreak per halted group — the group
            // members' places are shared and the surfaced directive is the
            // rung the engine halted at (the first non-comparator in the
            // stated list, or none when the ladder is comparator-only).
            var groups = ExpectedTieGroups(entries, policy);
            var halt = policy.FirstOrDefault(d => d is not BestDroppedScore and not QualifyingPosition);
            // EqualPlaces halts settled — the stated outcome, nothing pending.
            var halted = halt is null or EqualPlaces ? [] : groups;

            result.PendingTieBreaks.Length.Should().Be(halted.Count);
            foreach (var group in result.PendingTieBreaks)
            {
                halt.Should().NotBeNull();
                group.Directive.Should().Be(halt);
                group.CompetitorRefs.Should().BeEquivalentTo(
                    groups.First(g => g.Select(m => m.Ref).OrderBy(r => r).SequenceEqual(
                            group.CompetitorRefs.OrderBy(r => r)))
                        .Select(m => m.Ref));
                group.CompetitorRefs.Select(r => result.Placings[r]).Distinct().Should().HaveCount(1);
            }
        });
    }

    // -------------------------------------------------- Invariant T, clause 2

    [Fact]
    public void Empty_policy_is_byte_identical_to_the_display_ladder()
    {
        RawEntry.Array[1, 20].Sample(raw =>
        {
            var entries = Build(raw);
            var scores = entries
                .Select(e => new FinalCompetitorScore(e.Ref, e.Score, e.PreDrop, e.BestDropped, e.Disqualified))
                .ToImmutableArray();

            var result = RankingEngine.Rank(scores, null, null, TieBreakContext.Display);

            var active = entries.Where(e => !e.Disqualified).ToImmutableArray();
            result.Placings.Keys.Should().BeEquivalentTo(active.Select(e => e.Ref));

            // The two-rung display ladder, walked independently: Score DESC,
            // PreDropScore DESC, standard skip-ahead — and nothing pending.
            var expected = new Dictionary<string, int>();
            var ordered = active
                .OrderByDescending(e => e.Score)
                .ThenByDescending(e => e.PreDrop)
                .ToList();
            var place = 1;
            var i = 0;
            while (i < ordered.Count)
            {
                var j = i + 1;
                while (j < ordered.Count
                       && ordered[j].Score == ordered[i].Score
                       && ordered[j].PreDrop == ordered[i].PreDrop)
                    j++;

                for (var k = i; k < j; k++)
                    expected[ordered[k].Ref] = place;

                place += j - i;
                i = j;
            }

            result.Placings.Should().Equal(expected.ToImmutableDictionary());
            result.PendingTieBreaks.Should().BeEmpty();
        });
    }
}
