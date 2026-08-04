using System.Collections.Immutable;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for FlightSelector (LADR-0003: CsCheck, for
/// scoring-engine invariants). Covers the same rounding invariant as
/// NormalisationEnginePropertyTests, through this engine's own entry point
/// — the rounding block is duplicated between the two engines, so each is
/// tested through its own public surface rather than a shared helper.
/// </summary>
public class FlightSelectorPropertyTests
{
    private static readonly Gen<decimal> Score =
        Gen.Int[1, 100_000].Select(i => i / 100m);

    private static readonly Gen<RoundingMode> Mode =
        Gen.OneOfConst(RoundingMode.Truncate, RoundingMode.HalfUp, RoundingMode.Ceiling);

    private static readonly Gen<decimal> Precision = Gen.OneOfConst(1m, 0.1m, 0.01m, 0.001m);

    private static readonly Gen<Rounding> RoundingGen =
        from mode in Mode
        from precision in Precision
        select new Rounding(mode, precision);

    [Fact]
    public void Rounded_raw_score_is_exact_multiple_of_precision()
    {
        (from score in Score
         from rounding in RoundingGen
         select (score, rounding))
        .Sample(t =>
        {
            var task = MakeTask(t.rounding);
            var flight = new InterpretedFlight(
                new FlightResult(FlightResultState.Valid, new ResolvedMeasurements(new Dictionary<string, MeasuredValue>())),
                t.score,
                new Dictionary<int, TermContribution>());

            var result = FlightSelector.SelectAndScore(
                entry: null,
                task: task,
                parameterBindings: EmptyBindings,
                interpretedFlights: ImmutableArray.Create(flight));

            return result.State == TaskResultState.Valid
                && result.RawScore % t.rounding.Precision == 0m;
        });
    }

    // ------------------------------------------------------------- helpers

    private static ResolvedTask MakeTask(Rounding rounding) => new(
        Code: "T", Name: "Test",
        Metrics: ImmutableArray<MetricDefinition>.Empty,
        Flights: new AllFlights(),
        Timing: new ResolvedTiming(WorkingTimeKind.Fixed, null, null, null),
        Group: null,
        Normalise: null,
        ValidWhen: null,
        FlightValidWhen: null,
        RawScore: rounding,
        Reflight: null,
        Score: ImmutableArray<ScoreTerm>.Empty,
        ScoreNormalised: ImmutableArray<ScoreTerm>.Empty
    );

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();
}
