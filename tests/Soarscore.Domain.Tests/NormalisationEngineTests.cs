using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box sociable tests for NormalisationEngine (WI-5).
/// Tests HigherIsBetter, LowerIsBetter, pass-through, NoResult exclusion,
/// ScoreNormalised terms, group annulment, and rounding.
/// </summary>
public class NormalisationEngineTests
{
    // ------------------------------------------------------ HigherIsBetter

    [Fact]
    public void HigherIsBetter_winner_1000_correct_scaling()
    {
        // A=600, B=500 → A=1000, B=833 (1000×500/600)
        var task = MakeNormalisedTask(HigherIsBetter(1000));
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(600m),
            ["B"] = ValidResult(500m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.WinnerRef.Should().Be("A");
        group.ValidCount.Should().Be(2);
        group.Results["A"].RawScore.Should().Be(1000m);
        Math.Floor(group.Results["B"].RawScore).Should().Be(833m);
    }

    // ------------------------------------------------------ LowerIsBetter

    [Fact]
    public void LowerIsBetter_winner_1000_correct_scaling()
    {
        // A=15s, B=20s → A=1000, B=750 (1000×15/20)
        var task = MakeNormalisedTask(LowerIsBetter(1000));
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(15m),
            ["B"] = ValidResult(20m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.WinnerRef.Should().Be("A");
        group.ValidCount.Should().Be(2);
        group.Results["A"].RawScore.Should().Be(1000m);
        group.Results["B"].RawScore.Should().Be(750m);
    }

    // A LowerIsBetter raw score of exactly zero must not divide-by-zero.
    // For any metric this pipeline currently scores (courseTime, always
    // non-negative), a captured zero is itself the smallest possible value,
    // so MinBy always crowns IT the winner and the pre-existing
    // `winnerRaw == 0m` branch above already zeroes the whole group before
    // this guard is reached (see NormalisationEngineTests' sibling test and
    // SeedF3F.cs/SeedF3B.cs's ValidWhen, which now reject a captured zero
    // before it ever reaches here). The guard below is only reachable if a
    // future LowerIsBetter metric's raw score can go negative — a raw of
    // -5 stands in for that, giving a nonzero (negative) winnerRaw while a
    // different, non-winning competitor sits at exactly zero.
    [Fact]
    public void LowerIsBetter_non_winning_zero_raw_score_scores_zero_without_dividing_by_zero()
    {
        var task = MakeNormalisedTask(LowerIsBetter(1000));
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(-5m),
            ["B"] = ValidResult(0m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.WinnerRef.Should().Be("A");
        group.Results["A"].RawScore.Should().Be(1000m);
        group.Results["B"].RawScore.Should().Be(0m);
    }

    // ------------------------------------------------------ No normalisation pass-through

    [Fact]
    public void No_normalisation_passes_raw_scores_through()
    {
        var task = MakeUnnormalisedTask();
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(600m),
            ["B"] = ValidResult(500m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.WinnerRef.Should().BeNull();
        group.ValidCount.Should().Be(2);
        group.Results["A"].RawScore.Should().Be(600m);
        group.Results["B"].RawScore.Should().Be(500m);
    }

    // ------------------------------------------------------ NoResult exclusion

    [Fact]
    public void NoResult_competitor_excluded_from_winner_finding()
    {
        var task = MakeNormalisedTask(HigherIsBetter(1000));
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(600m),
            ["B"] = ValidResult(500m),
            ["C"] = NoResultResult(),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.WinnerRef.Should().Be("A");
        group.ValidCount.Should().Be(2);
        group.Results["C"].RawScore.Should().Be(0m);
    }

    // ------------------------------------------------------ All NoResult

    [Fact]
    public void All_NoResult_returns_null_winner_and_zero_scores()
    {
        var task = MakeNormalisedTask(HigherIsBetter(1000));
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = NoResultResult(),
            ["B"] = NoResultResult(),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.WinnerRef.Should().BeNull();
        group.ValidCount.Should().Be(0);
        group.Results["A"].RawScore.Should().Be(0m);
        group.Results["B"].RawScore.Should().Be(0m);
    }

    // ------------------------------------------------------ Rounding

    [Fact]
    public void Normalised_score_rounding_applied()
    {
        // F3K style: HigherIsBetter, winner 1000, round HalfUp to 0.1
        var normalise = new Normalisation
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new Rounding(RoundingMode.HalfUp, 0.1m),
        };

        var task = MakeNormalisedTask(normalise);
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(400m),
            ["B"] = ValidResult(333m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        // B = 1000 × 333 / 400 = 832.5 → HalfUp to 0.1 = 832.5
        group.Results["B"].RawScore.Should().Be(832.5m);
    }

    // ------------------------------------------------------ Lower clamp

    // Witness cell: f5j-nz-south-island comp 121, R3/G3, pilot 99 — raw
    // −2026 against the group basis 502.5 normalises to −4031.8 (HalfUp 0.1)
    // before the clamp; GS records 0.0 (F5J 5.5.11.12 f, Scoring_MOD.vb:310).
    // The pre-clamp value must never be observable.
    [Fact]
    public void Negative_normalised_score_is_floored_at_zero()
    {
        var normalise = new Normalisation
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new Rounding(RoundingMode.HalfUp, 0.1m),
        };

        var task = MakeNormalisedTask(normalise);
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(502.5m),
            ["B"] = ValidResult(-2026m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.Results["A"].RawScore.Should().Be(1000m);
        group.Results["B"].RawScore.Should().Be(0m);
    }

    // The clamp sits AFTER the ScoreNormalised terms (GS floors the option-2
    // sum after landing, Scoring_MOD.vb:367), so a terms rescue survives:
    // −100 + 130 scores 30 — not max(0, −100) + 130 = 40 (clamp pre-empting
    // terms) and not 0 (terms floored away).
    [Fact]
    public void Clamp_applies_after_score_normalised_terms_so_a_rescue_survives()
    {
        var task = MakeNormalisedTask(HigherIsBetter(1000)) with
        {
            ScoreNormalised = ImmutableArray.Create<ScoreTerm>(
                new ConstantTerm { Value = 130m }),
        };

        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResultWithFlight(100m),
            ["B"] = ValidResultWithFlight(-10m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.Results["A"].RawScore.Should().Be(1130m);  // 1000 + 130
        group.Results["B"].RawScore.Should().Be(30m);    // −100 + 130, above the floor
    }

    // A value in (−0.05·precision, 0) rounds to decimal negative zero, which
    // serialises as "-0.0"; the clamp's `<=` guarantees a literal positive
    // zero. 1000·(−0.004)/100 = −0.04 → HalfUp 0.1 → −0.0 → clamped to 0m.
    [Fact]
    public void Negative_zero_boundary_clamps_to_literal_zero()
    {
        var normalise = new Normalisation
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new Rounding(RoundingMode.HalfUp, 0.1m),
        };

        var task = MakeNormalisedTask(normalise);
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(100m),
            ["B"] = ValidResult(-0.004m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.Results["B"].RawScore.Should().Be(0m);
        group.Results["B"].RawScore
            .ToString(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be("0");  // a surviving -0.0m would serialise as "-0.0"
    }

    // Pass-through asymmetry (story D4): with no Normalisation the raw passes
    // through unchanged, INCLUDING negative raws — GS's option-0 floor is
    // deliberately not replicated (no fixture witnesses it; flooring would
    // change raw-grain semantics the oracle pins).
    [Fact]
    public void No_normalisation_passes_negative_raw_through_unfloored()
    {
        var task = MakeUnnormalisedTask();
        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(600m),
            ["B"] = ValidResult(-2026m),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.Results["A"].RawScore.Should().Be(600m);
        group.Results["B"].RawScore.Should().Be(-2026m);
    }

    // ------------------------------------------------------ Group annulment

    [Fact]
    public void Group_annulled_when_valid_count_below_min()
    {
        var task = MakeNormalisedTask(HigherIsBetter(1000)) with
        {
            Group = new ResolvedGroupConstraint(MinPerGroup: 3, MinValidResults: 3),
        };

        var results = new Dictionary<string, TaskResult>
        {
            ["A"] = ValidResult(600m),
            ["B"] = ValidResult(500m),  // only 2 valid, min is 3
            ["C"] = NoResultResult(),
        };

        var group = NormalisationEngine.Normalise(
            "G1", results.ToImmutableDictionary(), task, EmptyBindings());

        group.IsAnnulled.Should().BeTrue();
        group.ValidCount.Should().Be(2);
    }

    // ------------------------------------------------------ helpers

    private static TaskResult ValidResult(decimal rawScore) => new(
        TaskResultState.Valid,
        new SelectedFlights(
            ImmutableArray<InterpretedFlight>.Empty,
            new Dictionary<int, decimal?>()),
        rawScore);

    // One valid result carrying a single selected flight with empty metrics —
    // enough for a ConstantTerm ScoreNormalised term to contribute once.
    private static TaskResult ValidResultWithFlight(decimal rawScore) => new(
        TaskResultState.Valid,
        new SelectedFlights(
            ImmutableArray.Create(new InterpretedFlight(
                new FlightResult(
                    FlightResultState.Valid,
                    new ResolvedMeasurements(new Dictionary<string, MeasuredValue>())),
                Score: 0m,
                TermContributions: new Dictionary<int, TermContribution>())),
            new Dictionary<int, decimal?>()),
        rawScore);

    private static TaskResult NoResultResult() => new(
        TaskResultState.NoResult, null, 0m);

    private static Normalisation HigherIsBetter(int winnerScore) => new()
    {
        Direction = NormalisationDirection.HigherIsBetter,
        WinnerScore = winnerScore,
    };

    private static Normalisation LowerIsBetter(int winnerScore) => new()
    {
        Direction = NormalisationDirection.LowerIsBetter,
        WinnerScore = winnerScore,
    };

    private static ResolvedTask MakeNormalisedTask(Normalisation norm) => new(
        Code: "T", Name: "Test",
        Metrics: ImmutableArray<MetricDefinition>.Empty,
        Flights: new AllFlights(),
        Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
        Group: null,
        Normalise: norm,
        ValidWhen: null,
        FlightValidWhen: null,
        RawScore: null,
        Reflight: null,
        Score: ImmutableArray<ScoreTerm>.Empty,
        ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
    );

    private static ResolvedTask MakeUnnormalisedTask() => new(
        Code: "T", Name: "Test",
        Metrics: ImmutableArray<MetricDefinition>.Empty,
        Flights: new AllFlights(),
        Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
        Group: null,
        Normalise: null,
        ValidWhen: null,
        FlightValidWhen: null,
        RawScore: null,
        Reflight: null,
        Score: ImmutableArray<ScoreTerm>.Empty,
        ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
    );

    private static IReadOnlyDictionary<string, MeasuredValue> EmptyBindings()
        => new Dictionary<string, MeasuredValue>();
}
