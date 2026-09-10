using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Invariant O (kanban/in-progress/operational-tie-break-resolution.md,
/// verbatim): "For any field of active competitors, any stated ladder
/// producing a pending group G sharing place P, and any outcome O recorded
/// over exactly G's members: (1) the multiset of places over all active
/// competitors is unchanged by applying O; (2) every member of G occupies a
/// place within [P, P+|G|−1] after applying O; (3) for any a ∉ G and b ∈ G:
/// place(a) &lt; P before ⇒ place(a) &lt; place(b) after, and place(a) &gt;
/// P+|G|−1 before ⇒ place(a) &gt; place(b) after — an outcome never moves
/// anyone across the group's span boundary; (4) G surfaces no
/// `PendingTieBreak` after applying O; (5) with no recorded outcomes the
/// ranking result — placings and `PendingTieBreaks` — is identical to the
/// outcome-less result (regression clause); (6) an outcome whose ref set or
/// directive does not match any halted group changes nothing."
///
/// On clause (1): its literal multiset reading is unsatisfiable the moment a
/// shared place splits — the split IS the resolution — so it is asserted as
/// its entailed content: every competitor outside G keeps their exact place
/// (the only redistribution is within G's span, clauses 2–3) and the
/// numbering past the group is preserved.
///
/// Generator guidance per the story: fields with one or more pending groups
/// (operational and `UndefinedRequiresRuling` halts — scores are quantised so
/// ties are dense), outcomes that are strict orderings and outcomes with
/// recorded ties, and outcome sets covering the matching, the superseding
/// (two outcomes, last wins) and the non-matching cases. Dormant comparator
/// rungs are generated behind the halt so residual recorded ties exercise
/// them. RankingEngineTieBreakOutcomeTests covers the example cases (F3F/F3K
/// shapes, span, inertness); this is the general property CsCheck checks
/// across generated inputs, alongside it rather than in it.
/// </summary>
public class RankingEngineOutcomePropertyTests
{
    private sealed record Entry(
        string Ref, decimal Score, decimal PreDrop, decimal BestDropped,
        int Position, bool Disqualified, int OrderKey, bool Cut);

    // Small score/cell ranges so Score ties (and hence pending groups) are
    // dense. PreDrop − Score = Σ dropped cells ≥ 0 and BestDropped = max
    // dropped cell, the engine's real input class.
    private static readonly Gen<(decimal Score, decimal[] Cells, int Position, bool Disq, int OrderKey, bool Cut)> RawEntry =
        from score in Gen.Int[0, 3].Select(i => (decimal)i)
        from cells in Gen.Int[0, 2].Array[1, 2].Select(a => a.Select(i => (decimal)i).ToArray())
        from position in Gen.Int[1, 6]
        from disqualified in Gen.Bool
        from orderKey in Gen.Int
        from cut in Gen.Bool
        select (score, cells, position, disqualified, orderKey, cut);

    private static readonly Gen<TieBreakDirective> ComparatorGen =
        Gen.Int[0, 1].Select(k => k == 0
            ? (TieBreakDirective)new BestDroppedScore()
            : new QualifyingPosition { SourcePhaseOrdinal = 0 });

    private static TieBreakDirective HaltKind(int k) => k switch
    {
        0 => new AdditionalFullRound(),
        1 => new TieBreakFlyoff(),
        2 => new ClassificationRounds(),
        _ => new UndefinedRequiresRuling(),
    };

    private static readonly Gen<(ImmutableArray<TieBreakDirective> Policy, TieBreakDirective Halt)> PolicyGen =
        from pre in ComparatorGen.Array[0, 2]
        from haltKind in Gen.Int[0, 3]
        from dormant in ComparatorGen.Array[0, 2]
        select (
            pre.ToImmutableArray().Add(HaltKind(haltKind)).AddRange(dormant.ToImmutableArray()),
            HaltKind(haltKind));

    private static ImmutableArray<Entry> Build(
        IList<(decimal Score, decimal[] Cells, int Position, bool Disq, int OrderKey, bool Cut)> raw)
    {
        var builder = ImmutableArray.CreateBuilder<Entry>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var e = raw[i];
            builder.Add(new Entry(
                $"C{i}", e.Score, e.Score + e.Cells.Sum(), e.Cells.Max(),
                e.Position, e.Disq, e.OrderKey, e.Cut));
        }
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<FinalCompetitorScore> ToScores(ImmutableArray<Entry> entries) =>
        entries.Select(e => new FinalCompetitorScore(e.Ref, e.Score, e.PreDrop, e.BestDropped, e.Disqualified))
            .ToImmutableArray();

    private static ImmutableDictionary<string, int> ToPositions(ImmutableArray<Entry> entries) =>
        entries.ToImmutableDictionary(e => e.Ref, e => e.Position);

    /// <summary>
    /// The group's permutation, derived from generated order keys
    /// (deterministic in the sample, no ambient randomness).
    /// </summary>
    private static List<Entry> Permute(IReadOnlyList<Entry> members) =>
        members.OrderBy(e => e.OrderKey).ThenBy(e => e.Ref).ToList();

    /// <summary>Strict recorded ordering: 1..n over the permutation.</summary>
    private static ResolvedTieBreakOutcome StrictOutcome(
        TieBreakDirective directive, IReadOnlyList<Entry> members)
    {
        var permuted = Permute(members);
        var map = ImmutableDictionary.CreateBuilder<string, int>();
        for (var i = 0; i < permuted.Count; i++)
            map[permuted[i].Ref] = i + 1;
        return new ResolvedTieBreakOutcome(directive, map.ToImmutable());
    }

    /// <summary>
    /// Recorded ties: the permutation cut into chunks at generated cut
    /// flags, dense skip-ahead numbering from 1.
    /// </summary>
    private static ResolvedTieBreakOutcome TiedOutcome(
        TieBreakDirective directive, IReadOnlyList<Entry> members)
    {
        var permuted = Permute(members);
        var map = ImmutableDictionary.CreateBuilder<string, int>();
        var place = 1;
        var chunkStart = 0;
        for (var i = 0; i <= permuted.Count; i++)
        {
            if (i == permuted.Count || (permuted[i].Cut && i > chunkStart))
            {
                for (var k = chunkStart; k < i; k++)
                    map[permuted[k].Ref] = place;
                place += i - chunkStart;
                chunkStart = i;
            }
        }
        // No cut ever fired: the whole group shares recorded place 1.
        if (chunkStart == 0)
        {
            foreach (var e in permuted)
                map[e.Ref] = 1;
        }
        return new ResolvedTieBreakOutcome(directive, map.ToImmutable());
    }

    private static readonly Gen<((decimal Score, decimal[] Cells, int Position, bool Disq, int OrderKey, bool Cut)[] Raw, ImmutableArray<TieBreakDirective> Policy, TieBreakDirective Halt, bool Strict)> CaseGen =
        from raw in RawEntry.Array[2, 8]
        from policy in PolicyGen
        from strict in Gen.Bool
        select (raw, policy.Policy, policy.Halt, strict);

    // ------------------------------------------- Clauses 1, 2, 3: span discipline

    [Fact]
    public void Resolved_groups_stay_within_span_and_outsiders_hold()
    {
        CaseGen.Sample(testCase =>
        {
            var (raw, policy, halt, strict) = testCase;
            var entries = Build(raw);
            var active = entries.Where(e => !e.Disqualified).ToList();
            var baseline = RankingEngine.Rank(
                ToScores(entries), null, null, new TieBreakContext(policy, ToPositions(entries)));

            if (baseline.PendingTieBreaks.IsEmpty)
                return;

            var outcomes = baseline.PendingTieBreaks
                .Select(g => strict
                    ? StrictOutcome(halt, g.CompetitorRefs.Select(r => entries.Single(e => e.Ref == r)).ToList())
                    : TiedOutcome(halt, g.CompetitorRefs.Select(r => entries.Single(e => e.Ref == r)).ToList()))
                .ToImmutableArray();

            var resolved = RankingEngine.Rank(
                ToScores(entries), null, null,
                new TieBreakContext(policy, ToPositions(entries), outcomes));

            // Members of OTHER pending groups move within their own spans,
            // so clauses (1) and (3) quantify only over competitors in no
            // pending group — the genuinely untouched outsiders.
            var grouped = baseline.PendingTieBreaks
                .SelectMany(g => g.CompetitorRefs).ToHashSet();
            var fixedOutsiders = active.Where(e => !grouped.Contains(e.Ref)).ToList();

            foreach (var group in baseline.PendingTieBreaks)
            {
                var refs = group.CompetitorRefs;
                var p = baseline.Placings[refs[0]];
                var spanEnd = p + refs.Length - 1;

                // (2) every member occupies a place within [P, P+|G|−1].
                foreach (var r in refs)
                {
                    resolved.Placings[r].Should().BeGreaterThanOrEqualTo(p);
                    resolved.Placings[r].Should().BeLessThanOrEqualTo(spanEnd);
                }

                // Strict recorded orderings map exactly onto the span.
                if (strict)
                {
                    var outcome = outcomes.Single(o =>
                        o.PlacesByCompetitor.Keys.ToHashSet().SetEquals(refs));
                    foreach (var r in refs)
                        resolved.Placings[r].Should().Be(p + outcome.PlacesByCompetitor[r] - 1);
                }

                // (1) every competitor outside G keeps their exact place —
                // the only redistribution is within the span — and (3) no
                // outsider crosses the span boundary.
                foreach (var a in fixedOutsiders)
                {
                    resolved.Placings[a.Ref].Should().Be(baseline.Placings[a.Ref]); // (1)
                    foreach (var b in refs)
                    {
                        if (baseline.Placings[a.Ref] < p)
                            resolved.Placings[a.Ref].Should().BeLessThan(resolved.Placings[b]); // (3)
                        if (baseline.Placings[a.Ref] > spanEnd)
                            resolved.Placings[a.Ref].Should().BeGreaterThan(resolved.Placings[b]); // (3)
                    }
                }
            }
        });
    }

    // --------------------------------------- Clause 4 + supersession (last wins)

    [Fact]
    public void Resolved_groups_clear_pending_and_last_outcome_wins()
    {
        CaseGen.Sample(testCase =>
        {
            var (raw, policy, halt, _) = testCase;
            var entries = Build(raw);
            var baseline = RankingEngine.Rank(
                ToScores(entries), null, null, new TieBreakContext(policy, ToPositions(entries)));

            if (baseline.PendingTieBreaks.IsEmpty)
                return;

            // Two matching outcomes per group, opposite strict orders —
            // the LAST one is effective (D4).
            var outcomes = baseline.PendingTieBreaks
                .SelectMany(g =>
                {
                    var members = g.CompetitorRefs.Select(r => entries.Single(e => e.Ref == r)).ToList();
                    var first = StrictOutcome(halt, members);
                    var reversed = members.AsEnumerable().Reverse().ToList();
                    var secondMap = ImmutableDictionary.CreateBuilder<string, int>();
                    for (var i = 0; i < reversed.Count; i++)
                        secondMap[reversed[i].Ref] = i + 1;
                    var second = new ResolvedTieBreakOutcome(halt, secondMap.ToImmutable());
                    return new[] { first, second };
                })
                .ToImmutableArray();

            var resolved = RankingEngine.Rank(
                ToScores(entries), null, null,
                new TieBreakContext(policy, ToPositions(entries), outcomes));

            // (4) no halted group surfaces a PendingTieBreak.
            resolved.PendingTieBreaks.Should().BeEmpty();

            foreach (var group in baseline.PendingTieBreaks)
            {
                var refs = group.CompetitorRefs;
                var p = baseline.Placings[refs[0]];
                var last = outcomes.Last(o =>
                    o.PlacesByCompetitor.Keys.ToHashSet().SetEquals(refs));
                foreach (var r in refs)
                    resolved.Placings[r].Should().Be(p + last.PlacesByCompetitor[r] - 1);
            }
        });
    }

    // ------------------------------------------------- Clauses 5, 6: regression

    [Fact]
    public void Empty_and_non_matching_outcomes_change_nothing()
    {
        CaseGen.Sample(testCase =>
        {
            var (raw, policy, halt, _) = testCase;
            var entries = Build(raw);
            var active = entries.Where(e => !e.Disqualified).ToList();
            var scores = ToScores(entries);
            var positions = ToPositions(entries);
            var baseline = RankingEngine.Rank(scores, null, null, new TieBreakContext(policy, positions));

            // (5) no recorded outcomes: identical placings and pendings.
            var empty = RankingEngine.Rank(
                scores, null, null,
                new TieBreakContext(policy, positions, ImmutableArray<ResolvedTieBreakOutcome>.Empty));
            empty.Placings.Should().Equal(baseline.Placings);
            empty.PendingTieBreaks.Should().BeEquivalentTo(baseline.PendingTieBreaks);

            if (baseline.PendingTieBreaks.IsEmpty)
                return;

            // (6) non-matching outcomes: a wrong directive kind over the
            // exact set, and the halt kind over a wrong set (one member
            // swapped for an outsider, or dropped when the group IS the
            // field). Neither can match any halted group: groups are
            // disjoint, so the wrong set equals no group's set, and there
            // is only one halt kind per ladder.
            var otherKind = new TieBreakDirective[]
                { new AdditionalFullRound(), new TieBreakFlyoff(), new ClassificationRounds(), new UndefinedRequiresRuling() }
                .First(d => d.GetType() != halt.GetType());

            var inert = baseline.PendingTieBreaks.Select(g =>
            {
                var refs = g.CompetitorRefs;
                var outsider = active.Select(e => e.Ref).FirstOrDefault(r => !refs.Contains(r));
                var wrongRefs = outsider is not null
                    ? refs.Skip(1).Append(outsider).ToList()
                    : refs.SkipLast(1).ToList();
                var map = ImmutableDictionary.CreateBuilder<string, int>();
                for (var i = 0; i < wrongRefs.Count; i++)
                    map[wrongRefs[i]] = i + 1;
                return new ResolvedTieBreakOutcome(otherKind, map.ToImmutable());
            }).ToImmutableArray();

            var untouched = RankingEngine.Rank(
                scores, null, null, new TieBreakContext(policy, positions, inert));
            untouched.Placings.Should().Equal(baseline.Placings);
            untouched.PendingTieBreaks.Should().BeEquivalentTo(baseline.PendingTieBreaks);
        });
    }
}
