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

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

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
            ImmutableArray<PenaltyDefinition>.Empty).Result;

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

        var raw = PenaltyEngine.ApplyRawPenalties(taskResult, penalties, definitions).Result;
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

    // --------------------- Entry-scoped DeductPoints at the raw stage
    //
    // WI-2 pins the D1 wiring landed by WI-1:
    // kanban/in-progress/entry-scoped-deduct-points-penalties-inert.md#wi-2.
    // Flight/Entry-scoped DeductPoints effects now act at the task-round stage,
    // with accrual and exclusion-group semantics identical to the aggregate
    // stage (shared Accrue path, D2).
    //
    // Behavioural-refactor guard (WI-2 last bullet): every aggregate-stage test
    // above stays green unmodified through the extraction — nothing here
    // replaces them.

    [Fact]
    public void DeductPoints_at_raw_stage_accrues_per_occurrence_per_record()
    {
        // Mirrors how GetEntryPenalties groups by infraction type: two records
        // of the same type, each OccurrenceCount 2, accruing through ONE
        // definition instance → 100×2 + 100×2 = 400.
        var penalties = new[]
        {
            new RecordedPenalty("lateLanding", OccurrenceCount: 2),
            new RecordedPenalty("lateLanding", OccurrenceCount: 2),
        }.ToImmutableArray();

        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "lateLanding",
                Accrual = PenaltyAccrual.PerOccurrence,
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100) }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.Valid);
        applied.RawScore.Should().Be(100m); // 500 − 400
    }

    [Fact]
    public void OncePerAttempt_at_raw_stage_ignores_occurrence_count()
    {
        var penalties = new[] { new RecordedPenalty("x", OccurrenceCount: 3) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "x",
                Accrual = PenaltyAccrual.OncePerAttempt,
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 50) }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.Valid);
        applied.RawScore.Should().Be(450m); // exactly 50, regardless of count 3
    }

    [Fact]
    public void Exclusion_group_only_largest_survives_at_raw_stage()
    {
        // Same shape as the aggregate test above: safetyGroup holds
        // objectContact(100) and personContact(300); only 300 is subtracted.
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

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.Valid);
        applied.RawScore.Should().Be(200m); // 500 − 300
    }

    [Fact]
    public void Zeroing_effect_wins_over_deduction_in_one_definition_at_raw_stage()
    {
        // D3: zero-dominance early-out beats any accrued deduction.
        var penalties = new[] { new RecordedPenalty("grossMisconduct", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "grossMisconduct",
                Effects = new[]
                {
                    new PenaltyEffectSpec(PenaltyEffect.ZeroFlight),
                    new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 1000),
                }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.NoResult);
        applied.RawScore.Should().Be(0m);
        applied.Selection.Should().BeNull();
    }

    [Fact]
    public void Deduction_pushing_raw_negative_floors_at_zero()
    {
        // D4: a deducted HigherIsBetter raw never goes below zero (FAI General
        // §6 / C.19 analogue) — state stays Valid, Selection untouched.
        var penalties = new[] { new RecordedPenalty("crossing", OccurrenceCount: 2) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "crossing",
                Accrual = PenaltyAccrual.PerOccurrence,
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 300) }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.Valid);
        applied.RawScore.Should().Be(0m); // max(0, 500 − 600)
        applied.Selection.Should().NotBeNull();
    }

    [Fact]
    public void Pure_deduct_penalty_keeps_state_valid_and_selection_intact()
    {
        var penalties = new[] { new RecordedPenalty("lateLanding", OccurrenceCount: 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "lateLanding",
                Accrual = PenaltyAccrual.PerOccurrence,
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100) }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var selection = new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
            new Dictionary<int, decimal?>());
        var result = new TaskResult(TaskResultState.Valid, selection, RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.Valid);
        applied.RawScore.Should().Be(400m);
        applied.Selection.Should().NotBeNull();
    }

    [Fact]
    public void NoResult_input_stays_untouched_even_with_matching_penalties()
    {
        // WI-1 guard: penalties have nothing valid left to act on when the
        // input already carries no result.
        var penalties = new[] { new RecordedPenalty("lateLanding", OccurrenceCount: 2) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition
            {
                InfractionType = "lateLanding",
                Accrual = PenaltyAccrual.PerOccurrence,
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 100) }.ToImmutableArray(),
            }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.NoResult,
            Selection: null,
            RawScore: 777m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions).Result;

        applied.State.Should().Be(TaskResultState.NoResult);
        applied.RawScore.Should().Be(777m); // returned unchanged, not floored
        applied.Selection.Should().BeNull();
    }

    // --------------------- Disqualify at the raw stage
    //
    // WI-3 pins the D-B1..D-B3 wiring landed by WI-2:
    // kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3.
    // Every declared effect now acts at the task-round stage: DeductPoints
    // subtracts (parent D1, already pinned above), Disqualify sets the
    // RawPenaltyApplication flag carried out of the walk (D-B1/D-B2), and the
    // flag survives the Zero*-dominance early-out (D-B3).
    //
    // D-B4 pin (no engine change): every definition below is deliberately
    // non-grouped. A Disqualify-carrying definition can never join an
    // exclusion group — adoption check 16 admits only all-DeductPoints
    // definitions (ClassDefinitionValidation.CheckExclusionGroupsAreDeductOnly,
    // defect class-definition.check-16.exclusion-group-non-deduct-effect) — so
    // ResolveExclusion's suppression can never hide the flag; these tests
    // exercise the un-suppressible shape directly.

    [Fact]
    public void Pure_disqualify_at_raw_stage_flags_without_touching_the_score()
    {
        // D-B2: the flag is flag-only — state stays Valid and RawScore is
        // untouched (aggregate-stage Disqualify changes no arithmetic, so
        // entry-scoped does not either). Non-grouped definition, D-B4 above.
        var penalties = new[] { new RecordedPenalty("grossMisconduct", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "grossMisconduct",
                Effects = new[] { new PenaltyEffectSpec(PenaltyEffect.Disqualify) }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions);

        applied.Disqualified.Should().BeTrue();
        applied.Result.State.Should().Be(TaskResultState.Valid);
        applied.Result.RawScore.Should().Be(500m);
        applied.Result.Selection.Should().NotBeNull();
    }

    [Fact]
    public void DeductPoints_and_Disqualify_in_one_definition_reduces_and_flags()
    {
        // Parent D1 + D-B1: every declared effect acts at the stage that owns
        // the record — the deduction subtracts pre-normalisation AND the flag
        // is carried out of the same call
        // (kanban/in-progress/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3).
        // Non-grouped definition, D-B4 above.
        var penalties = new[] { new RecordedPenalty("unsafeLaunch", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "unsafeLaunch",
                Effects = new[]
                {
                    new PenaltyEffectSpec(PenaltyEffect.DeductPoints, 200),
                    new PenaltyEffectSpec(PenaltyEffect.Disqualify),
                }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions);

        applied.Disqualified.Should().BeTrue();
        applied.Result.State.Should().Be(TaskResultState.Valid);
        applied.Result.RawScore.Should().Be(300m); // 500 − 200, flag on top
        applied.Result.Selection.Should().NotBeNull();
    }

    [Fact]
    public void Zero_effect_and_Disqualify_in_one_definition_zeroes_and_flags()
    {
        // D-B3: the Disqualify accrual is computed BEFORE the Zero*-dominance
        // scan, so the early-out carries the flag — a ZeroFlight + Disqualify
        // definition yields NoResult AND the flag: both declared effects
        // acted. Non-grouped definition, D-B4 above.
        var penalties = new[] { new RecordedPenalty("grossMisconduct", 1) }.ToImmutableArray();
        var definitions = new[]
        {
            new PenaltyDefinition { InfractionType = "grossMisconduct",
                Effects = new[]
                {
                    new PenaltyEffectSpec(PenaltyEffect.ZeroFlight),
                    new PenaltyEffectSpec(PenaltyEffect.Disqualify),
                }.ToImmutableArray() }
        }.ToImmutableArray();

        var result = new TaskResult(TaskResultState.Valid,
            new SelectedFlights(ImmutableArray<InterpretedFlight>.Empty,
                new Dictionary<int, decimal?>()),
            RawScore: 500m);

        var applied = PenaltyEngine.ApplyRawPenalties(result, penalties, definitions);

        applied.Disqualified.Should().BeTrue();
        applied.Result.State.Should().Be(TaskResultState.NoResult);
        applied.Result.RawScore.Should().Be(0m);
        applied.Result.Selection.Should().BeNull();
    }
}
