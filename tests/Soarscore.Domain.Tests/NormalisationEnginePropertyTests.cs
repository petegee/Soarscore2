using System.Collections.Immutable;
using CsCheck;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for NormalisationEngine (LADR-0003: CsCheck, for
/// scoring-engine invariants). Complements the example-based tests in
/// NormalisationEngineTests with invariants checked across generated inputs.
/// </summary>
public class NormalisationEnginePropertyTests
{
    private static readonly Gen<decimal> RawScore =
        Gen.Int[1, 100_000].Select(i => i / 100m);

    private static readonly Gen<int> WinnerScore = Gen.Int[1, 5000];

    private static readonly Gen<NormalisationDirection> Direction =
        Gen.OneOfConst(NormalisationDirection.HigherIsBetter, NormalisationDirection.LowerIsBetter);

    private static readonly Gen<RoundingMode> Mode =
        Gen.OneOfConst(RoundingMode.Truncate, RoundingMode.HalfUp, RoundingMode.Ceiling);

    private static readonly Gen<decimal> Precision = Gen.OneOfConst(1m, 0.1m, 0.01m, 0.001m);

    private static readonly Gen<Rounding> RoundingGen =
        from mode in Mode
        from precision in Precision
        select new Rounding(mode, precision);

    private static readonly Gen<TaskResultState> ResultState =
        Gen.OneOfConst(TaskResultState.Valid, TaskResultState.NoResult);

    private static readonly Gen<(TaskResultState state, decimal raw)> Entry =
        from state in ResultState
        from raw in RawScore
        select (state, raw);

    // -------------------------------------------------------------- winner

    [Fact]
    public void Winner_normalises_to_WinnerScore()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from raws in RawScore.Array[2, 5]
         select (direction, winnerScore, raws))
        .Sample(t =>
        {
            var task = MakeTask(new Normalisation { Direction = t.direction, WinnerScore = t.winnerScore });

            var results = t.raws
                .Select((r, i) => ($"C{i}", ValidResult(r)))
                .ToDictionary(x => x.Item1, x => x.Item2);

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            // The entry whose raw score equals winnerRaw always normalises to
            // exactly WinnerScore, regardless of direction — because for that
            // entry raw == winnerRaw, so the ratio is 1.
            return group.WinnerRef is not null
                && group.Results[group.WinnerRef].RawScore == t.winnerScore;
        });
    }

    // ------------------------------------------------------------ monotonic

    [Fact]
    public void Normalised_score_is_monotonic_in_raw_score()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from lo in RawScore
         from deltaCents in Gen.Int[1, 50_000]
         select (direction, winnerScore, lo, hi: lo + deltaCents / 100m))
        .Sample(t =>
        {
            var task = MakeTask(new Normalisation { Direction = t.direction, WinnerScore = t.winnerScore });

            var results = new Dictionary<string, TaskResult>
            {
                ["Lo"] = ValidResult(t.lo),
                ["Hi"] = ValidResult(t.hi),
            };

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            var loScore = group.Results["Lo"].RawScore;
            var hiScore = group.Results["Hi"].RawScore;

            return t.direction == NormalisationDirection.HigherIsBetter
                ? loScore <= hiScore
                : loScore >= hiScore;
        });
    }

    // ------------------------------------------------------------ NoResult

    [Fact]
    public void NoResult_entries_always_score_zero()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from entries in Entry.Array[1, 6]
         select (direction, winnerScore, entries))
        .Sample(t =>
        {
            var task = MakeTask(new Normalisation { Direction = t.direction, WinnerScore = t.winnerScore });

            var keyed = t.entries
                .Select((e, i) => (Key: $"C{i}", e.state, e.raw))
                .ToList();

            var results = keyed.ToDictionary(
                k => k.Key,
                k => k.state == TaskResultState.Valid ? ValidResult(k.raw) : NoResultResult());

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            return keyed
                .Where(k => k.state == TaskResultState.NoResult)
                .All(k => group.Results[k.Key].RawScore == 0m);
        });
    }

    // ------------------------------------------------------------ pass-through

    [Fact]
    public void No_normalisation_is_identity_on_raw_score()
    {
        Entry.Array[1, 6].Sample(entries =>
        {
            var task = MakeUnnormalisedTask();

            var keyed = entries
                .Select((e, i) => (Key: $"C{i}", e.state, e.raw))
                .ToList();

            var results = keyed.ToDictionary(
                k => k.Key,
                k => k.state == TaskResultState.Valid ? ValidResult(k.raw) : NoResultResult());

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            return keyed.All(k => group.Results[k.Key].RawScore == results[k.Key].RawScore);
        });
    }

    // ------------------------------------------------------------ rounding

    [Fact]
    public void Rounded_normalised_score_is_exact_multiple_of_precision()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from rounding in RoundingGen
         from entries in Entry.Array[1, 6]
         select (direction, winnerScore, rounding, entries))
        .Sample(t =>
        {
            var task = MakeTask(new Normalisation
            {
                Direction = t.direction,
                WinnerScore = t.winnerScore,
                Round = t.rounding,
            });

            var keyed = t.entries
                .Select((e, i) => (Key: $"C{i}", e.state, e.raw))
                .ToList();

            var results = keyed.ToDictionary(
                k => k.Key,
                k => k.state == TaskResultState.Valid ? ValidResult(k.raw) : NoResultResult());

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            return keyed.All(k => group.Results[k.Key].RawScore % t.rounding.Precision == 0m);
        });
    }

    // ------------------------------------------------------------- helpers

    private static TaskResult ValidResult(decimal rawScore) => new(
        TaskResultState.Valid,
        new SelectedFlights(
            ImmutableArray<InterpretedFlight>.Empty,
            new Dictionary<int, decimal?>()),
        rawScore);

    private static TaskResult NoResultResult() => new(
        TaskResultState.NoResult, null, 0m);

    private static ResolvedTask MakeTask(Normalisation? norm) => new(
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

    private static ResolvedTask MakeUnnormalisedTask() => MakeTask(null);

    private static readonly IReadOnlyDictionary<string, MeasuredValue> EmptyBindings =
        new Dictionary<string, MeasuredValue>();
}
