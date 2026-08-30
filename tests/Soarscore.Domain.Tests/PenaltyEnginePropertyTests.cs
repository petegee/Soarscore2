using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property tests for PenaltyEngine — the WI-3 named invariants
/// (kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-3),
/// complementing the example-based shapes in <see cref="PenaltyEngineTests"/>.
///
/// Generator design: a small synthetic vocabulary of pure-deduct definitions —
/// PerOccurrence/OncePerAttempt mix, exclusion groups "gA"/"gB" with overlapping
/// membership (gA: pB vs pC; gB: pC vs oB — occurrence counts flip the group
/// winners, exercising ResolveExclusion across many combinations), groupless
/// baselines (pA, oC), and one deliberately undefined infraction type ("uX")
/// for the unmatched-record skip (D6). An optional Zero*-carrying co-definition
/// ("zeroDef", F3B nonConformingWinch-shaped) is asserted by a separate
/// zero-branch fact, keeping the pure-deduct lockstep equality uncontaminated
/// by the zeroing early-out.
/// </summary>
public class PenaltyEnginePropertyTests
{
    private const decimal OrderProbeRawScore = 2000m;

    private static PenaltyDefinition Deduct(
        string infractionType, decimal points, PenaltyAccrual accrual, string[] exclusionGroups) => new()
    {
        InfractionType = infractionType,
        Accrual = accrual,
        ExclusionGroups = [.. exclusionGroups],
        Effects = [new PenaltyEffectSpec(PenaltyEffect.DeductPoints, points)],
    };

    private static readonly ImmutableArray<PenaltyDefinition> PureDeductDefinitions =
    [
        Deduct("pA", 50m, PenaltyAccrual.PerOccurrence, []),
        Deduct("pB", 180m, PenaltyAccrual.PerOccurrence, ["gA"]),
        Deduct("pC", 500m, PenaltyAccrual.PerOccurrence, ["gA", "gB"]),
        Deduct("oB", 320m, PenaltyAccrual.OncePerAttempt, ["gB"]),
        Deduct("oC", 125m, PenaltyAccrual.OncePerAttempt, []),
    ];

    private static readonly ImmutableArray<PenaltyDefinition> DefinitionsWithZeroCoDef =
    [
        .. PureDeductDefinitions,
        new PenaltyDefinition
        {
            InfractionType = "zeroDef",
            Effects =
            [
                new PenaltyEffectSpec(PenaltyEffect.ZeroFlight),
                new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 1000m),
            ],
        },
    ];

    private static readonly Gen<(string Type, int Count)> RecordedFact =
        from type in Gen.OneOfConst("pA", "pB", "pC", "oB", "oC", "uX")
        from count in Gen.Int[0, 3]
        select (type, count);

    private static readonly Gen<ImmutableArray<RecordedPenalty>> RecordedSet =
        RecordedFact.Array[0, 5].Select(facts =>
            facts.Select(f => new RecordedPenalty(f.Type, f.Count)).ToImmutableArray());

    private static readonly Gen<decimal> StartingRaw =
        Gen.Int[0, 100_000].Select(i => i / 100m);

    private static TaskResult ValidResult(decimal rawScore) =>
        new(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: rawScore);

    // ============================================================ P-RawSymmetry

    /// <summary>
    /// P-RawSymmetry
    /// (kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-3):
    /// "for the same recorded set and definitions, ApplyRawPenalties's
    /// surviving-deduction total equals ApplyAggregatePenalties.Deduction
    /// modulo the ≥0 floor". Because both stages run the one shared
    /// <c>Accrue</c> + <c>ResolveExclusion</c> path, they stay provably in
    /// lockstep — this is the property that keeps the two stages provably
    /// in lockstep as future rules land. The only sanctioned divergence is
    /// the D4 floor: the raw stage clamps at zero where the aggregate stage
    /// reports the undamped deduction. Sampled here WITHOUT the Zero*
    /// co-definition, so the zeroing early-out cannot mask the equality.
    /// </summary>
    [Fact]
    public void P_RawSymmetry_raw_stage_deduction_matches_aggregate_modulo_floor()
    {
        (from raw in StartingRaw
         from penalties in RecordedSet
         select (raw, penalties))
        .Sample(t =>
        {
            var (raw, penalties) = t;

            var expectedDeduction = PenaltyEngine.ApplyAggregatePenalties(
                raw, penalties, PureDeductDefinitions).Deduction;

            var actual = PenaltyEngine.ApplyRawPenalties(
                ValidResult(raw), penalties, PureDeductDefinitions).Result;

            actual.State.Should().Be(TaskResultState.Valid);
            actual.RawScore.Should().Be(Math.Max(0m, raw - expectedDeduction));
            actual.Selection.Should().NotBeNull();
        });
    }

    /// <summary>
    /// P-RawSymmetry, zero branch (D3): when a matched definition carries a
    /// Zero* effect alongside DeductPoints (adoption rules forbid it joining
    /// exclusion groups, so suppression cannot touch it), the raw stage
    /// zeroes instead of deducting — NoResult, RawScore 0, Selection null —
    /// while the aggregate stage still accrues that definition's contribution
    /// through the shared <c>Accrue</c> path. asserted separately from the
    /// pure-deduct equality above so the two branches are each pinned cleanly.
    /// </summary>
    [Fact]
    public void P_RawSymmetry_zero_carrying_definition_yields_no_result_while_aggregate_still_accrues()
    {
        (from raw in StartingRaw
         from extras in RecordedFact.Array[0, 4]
         from zeroOccurrences in Gen.Int[1, 3]
         select (raw,
             extras.Select(f => new RecordedPenalty(f.Type, f.Count))
                 .Append(new RecordedPenalty("zeroDef", zeroOccurrences))
                 .ToImmutableArray()))
        .Sample(t =>
        {
            var (raw, penalties) = t;

            var rawApplied = PenaltyEngine.ApplyRawPenalties(
                ValidResult(raw), penalties, DefinitionsWithZeroCoDef).Result;

            rawApplied.State.Should().Be(TaskResultState.NoResult);
            rawApplied.RawScore.Should().Be(0m);
            rawApplied.Selection.Should().BeNull();

            var aggregate = PenaltyEngine.ApplyAggregatePenalties(
                raw, penalties, DefinitionsWithZeroCoDef);

            // zeroDef is groupless and OncePerAttempt, so it always survives
            // with exactly 1000; any further pure-deduct survivors only add.
            aggregate.Deduction.Should().BeGreaterThanOrEqualTo(1000m);
        });
    }

    // ==================================================== P-RawOrderIndependence

    /// <summary>
    /// P-RawOrderIndependence
    /// (kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-3):
    /// "the resulting TaskResult is invariant under any permutation of the
    /// recorded Penalty records on the Entry" — the single-pass guarantee
    /// (match all definitions, accrue all contributions, resolve exclusions
    /// from originals, then apply), mirroring the aggregate algorithm's
    /// documented order-independence (PenaltyEngine.cs header). One shared
    /// valid input instance feeds every ordering so Selection is the same
    /// object throughout, making full TaskResult record equality well-defined.
    /// </summary>
    [Fact]
    public void P_RawOrderIndependence_task_result_invariant_under_penalty_record_permutation()
    {
        (from penalties in RecordedSet
         from permutedA in Gen.Shuffle(penalties.ToArray())
         from permutedB in Gen.Shuffle(penalties.ToArray())
         select (penalties, permutedA, permutedB))
        .Sample(t =>
        {
            var (penalties, permutedA, permutedB) = t;

            var input = ValidResult(OrderProbeRawScore);
            var original = PenaltyEngine.ApplyRawPenalties(input, t.penalties, PureDeductDefinitions).Result;
            var reorderedA = PenaltyEngine.ApplyRawPenalties(input, t.permutedA.ToImmutableArray(), PureDeductDefinitions).Result;
            var reorderedB = PenaltyEngine.ApplyRawPenalties(input, t.permutedB.ToImmutableArray(), PureDeductDefinitions).Result;

            foreach (var reordered in new[] { reorderedA, reorderedB })
            {
                reordered.Should().Be(original);
                reordered.State.Should().Be(original.State);
                reordered.RawScore.Should().Be(original.RawScore);
                reordered.Selection.Should().Be(original.Selection);
            }
        });
    }
}
