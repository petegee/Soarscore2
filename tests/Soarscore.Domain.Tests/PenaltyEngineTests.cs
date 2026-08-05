using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for PenaltyEngine (WI-6).
/// Tests raw-score penalties (ZeroFlight, ZeroRound, ZeroTask), aggregate
/// penalties (DeductPoints, Disqualify), exclusion-group suppression,
/// accrual, and single-pass semantics.
/// </summary>
public class PenaltyEngineTests
{
    // ------------------------------------------------------ ZeroFlight

    [Fact]
    public void ZeroFlight_penalty_zeroes_result()
    {
        var penalties = new[] { new RecordedPenalty("motorRestart", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "motorRestart",
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.ZeroFlight) }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions);

        applied.State.Should().Be(TaskResultState.NoResult);
        applied.RawScore.Should().Be(0m);
        applied.Selection.Should().BeNull();
    }

    // ------------------------------------------------------ No raw penalties

    [Fact]
    public void No_raw_penalties_returns_result_unchanged()
    {
        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(
            result, ImmutableArray<RecordedPenalty>.Empty,
            ImmutableArray<PenaltyDefinition>.Empty);

        applied.State.Should().Be(TaskResultState.Valid);
        applied.RawScore.Should().Be(500m);
    }

    // ------------------------------------------------------ DeductPoints

    [Fact]
    public void DeductPoints_reduces_aggregate_score()
    {
        var penalties = new[] { new RecordedPenalty("safetyViolation", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "safetyViolation",
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 300) }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);

        result.Deduction.Should().Be(300m);
        result.Disqualified.Should().BeFalse();
    }

    // ------------------------------------------------------ Disqualify

    [Fact]
    public void Disqualify_effect_flags_competitor()
    {
        var penalties = new[] { new RecordedPenalty("grossMisconduct", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "grossMisconduct",
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.Disqualify) }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);

        result.Disqualified.Should().BeTrue();
        result.Deduction.Should().Be(0m);
    }

    // ------------------------------------------------------ PerOccurrence accrual

    [Fact]
    public void PerOccurrence_multiplies_by_occurrence_count()
    {
        var penalties = new[] { new RecordedPenalty("crossing", OccurrenceCount: 2) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "crossing",
                Accrual = PenaltyAccrual.PerOccurrence,
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100) }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var result = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);

        result.Deduction.Should().Be(200m); // 2 × 100
    }

    // ------------------------------------------------------ Exclusion group suppression

    [Fact]
    public void Exclusion_group_only_largest_survives()
    {
        // safetyGroup: contact(100) and personContact(300) → only 300 applied
        var penalties = new[]
        {
            new RecordedPenalty("objectContact", 1),
            new RecordedPenalty("personContact", 1),
        }.ToImmutableArray();

        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "objectContact",
                ExclusionGroups = new[] { "safetyGroup" }.ToImmutableArray(),
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100) }.ToImmutableArray(),
            },
            new PenaltyDefinition
            {
                InfractionType = "personContact",
                ExclusionGroups = new[] { "safetyGroup" }.ToImmutableArray(),
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 300) }.ToImmutableArray(),
            },
        }.ToImmutableArray();

        var result = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);

        result.Deduction.Should().Be(300m);
    }

    // ------------------------------------------------------ Single-pass suppression

    [Fact]
    public void Single_pass_suppression_is_order_independent()
    {
        // A in [X], B in [X, Y], C in [Y]. A=500, B=300, C=400.
        // X group: A(500) > B(300) → B suppressed in X
        // Y group: B(suppressed) vs C(400) → C wins.
        // Survivors: A(500) + C(400) = 900
        var penalties = new[]
        {
            new RecordedPenalty("a", 1),
            new RecordedPenalty("b", 1),
            new RecordedPenalty("c", 1),
        }.ToImmutableArray();

        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "a",
                ExclusionGroups = new[] { "X" }.ToImmutableArray(),
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 500) }.ToImmutableArray(),
            },
            new PenaltyDefinition
            {
                InfractionType = "b",
                ExclusionGroups = new[] { "X", "Y" }.ToImmutableArray(),
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 300) }.ToImmutableArray(),
            },
            new PenaltyDefinition
            {
                InfractionType = "c",
                ExclusionGroups = new[] { "Y" }.ToImmutableArray(),
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 400) }.ToImmutableArray(),
            },
        }.ToImmutableArray();

        var result = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);

        // A=500 (survives), C=400 (survives, B suppressed in Y), B=300 (suppressed)
        result.Deduction.Should().Be(900m);
    }

    // ------------------------------------------------------ Both effects in one penalty

    [Fact]
    public void Penalty_with_both_raw_and_aggregate_effects_applies_both()
    {
        // F3B.2.2 p: nonConformingWinch → ZeroFlight AND DeductPoints 1000
        var penalties = new[] { new RecordedPenalty("nonConformingWinch", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "nonConformingWinch",
                Effects = new[]
                {
                    new PenaltyEffectSpec(PenaltyEffect.ZeroFlight),
                    new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 1000),
                }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        // Raw stage
        var taskResult = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var raw = PenaltyEngine.ApplyRawPenalties(taskResult, penalties, definitions);
        raw.State.Should().Be(TaskResultState.NoResult);
        raw.RawScore.Should().Be(0m);

        // Aggregate stage
        var agg = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);
        agg.Deduction.Should().Be(1000m);
        agg.Disqualified.Should().BeFalse();
    }

    // ------------------------------------------------------ Untracked penalty

    [Fact]
    public void Penalty_not_in_definitions_is_ignored()
    {
        var penalties = new[] { new RecordedPenalty("unknown", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "known",
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100) }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = PenaltyEngine.ApplyAggregatePenalties(1000m, penalties, definitions);

        result.Deduction.Should().Be(0m);
        result.Disqualified.Should().BeFalse();
    }
}
