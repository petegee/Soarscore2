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

    // Negative-capable raws for the lower-clamp properties — deliberately
    // separate from RawScore so the five pre-clamp properties keep their
    // positive-only domain (kanban/completed/normalisation-lower-clamp.md WI-3).
    private static readonly Gen<decimal> SignedRawScore =
        Gen.Int[-100_000, 100_000].Select(i => i / 100m);

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

    // Signed counterpart of Entry for the pre-normalisation-map properties:
    // RawScore (positive cents) never produces 0 or a negative, which would
    // leave the zero-winner guard and the LowerIsBetter zero-backstop
    // unreachable through generated inputs.
    private static readonly Gen<(TaskResultState state, decimal raw)> SignedEntry =
        from state in ResultState
        from raw in SignedRawScore
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

    // ------------------------------------------------------------ clamp

    // Invariant: with a Normalisation present, every emitted normalised value
    // is ≥ 0, whatever the raws. A positive basis raw coexists with signed
    // raws; the winner's own result is exactly WinnerScore whenever the
    // winning raw > 0 (a winnerRaw ≤ 0 group is zeroed wholesale before the
    // clamp is even reached).
    [Fact]
    public void No_normalised_cell_is_negative()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from basis in RawScore
         from others in SignedRawScore.Array[1, 4]
         select (direction, winnerScore, basis, others))
        .Sample(t =>
        {
            var task = MakeTask(new Normalisation { Direction = t.direction, WinnerScore = t.winnerScore });

            var raws = new[] { t.basis }.Concat(t.others).ToArray();
            var results = raws
                .Select((r, i) => ($"C{i}", ValidResult(r)))
                .ToDictionary(x => x.Item1, x => x.Item2);

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            if (group.Results.Values.Any(v => v.RawScore < 0m))
                return false;

            if (group.WinnerRef is not null)
            {
                var winnerRaw = t.direction == NormalisationDirection.HigherIsBetter
                    ? raws.Max()
                    : raws.Min();

                if (winnerRaw > 0m && group.Results[group.WinnerRef].RawScore != t.winnerScore)
                    return false;
            }

            return true;
        });
    }

    // Invariant: the clamp collapses but never inverts — for raws a ≥ b the
    // normalised pair satisfies n(a) ≥ n(b) (HigherIsBetter) / n(a) ≤ n(b)
    // (LowerIsBetter), signed raws included; strict order may collapse to
    // equality at the floor but never flips. Guarded on winnerRaw > 0: that is
    // the domain where the pre-clamp transform is order-preserving (a negative
    // winnerRaw inverts the ratio formula itself — unreachable for today's
    // metrics, whose raws cannot go negative below a positive winner, and out
    // of scope for the clamp per normalisation-lower-clamp.md D1).
    [Fact]
    public void Clamping_preserves_weak_order()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from basis in RawScore
         from lo in SignedRawScore
         from deltaCents in Gen.Int[0, 100_000]
         select (direction, winnerScore, basis, lo, hi: lo + deltaCents / 100m))
        .Sample(t =>
        {
            var task = MakeTask(new Normalisation { Direction = t.direction, WinnerScore = t.winnerScore });

            var raws = new[] { t.basis, t.lo, t.hi };
            var results = raws
                .Select((r, i) => ($"C{i}", ValidResult(r)))
                .ToDictionary(x => x.Item1, x => x.Item2);

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            var winnerRaw = t.direction == NormalisationDirection.HigherIsBetter
                ? raws.Max()
                : raws.Min();

            if (winnerRaw <= 0m)
                return true;  // zeroed wholesale, or the out-of-scope inverted domain

            var loScore = group.Results["C1"].RawScore;
            var hiScore = group.Results["C2"].RawScore;

            return t.direction == NormalisationDirection.HigherIsBetter
                ? loScore <= hiScore
                : loScore >= hiScore;
        });
    }

    // ------------------------------------------- pre-normalisation map (WI-1)

    // P1 — pre-normalisation preservation
    // (kanban/in-progress/pre-normalisation-score-view-field.md, "Property-based
    // testing"): for any group of arbitrary task results under any task
    // definition, Normalise preserves every row's received raw score —
    // ∀k: output.PreNormalisationScores[k] == input[k].RawScore — with the
    // map's key set EQUAL to the results' key set, while Results[k].RawScore
    // legitimately changes. TaskResults are built DIRECTLY from the generated
    // (state, raw) pairs so NoResult rows can carry non-zero raws (story trap
    // 1); raws come from the signed domain so 0 and negatives are reachable,
    // exercising all four branches (pass-through, valid-scale, zero-winner
    // guard, LowerIsBetter backstop). Signed raws are sound here: P1 pins
    // map-vs-input, orthogonal to the final-value arithmetic that the
    // negative-winner "inverted domain" caveat concerns.
    [Fact]
    public void PreNormalisationScores_preserve_every_received_raw_score()
    {
        (from direction in Direction
         from winnerScore in WinnerScore
         from rounding in RoundingGen
         from normalised in Gen.Bool
         from entries in SignedEntry.Array[1, 6]
         select (direction, winnerScore, rounding, normalised, entries))
        .Sample(t =>
        {
            var norm = new Normalisation
            {
                Direction = t.direction,
                WinnerScore = t.winnerScore,
                Round = t.rounding,
            };
            var task = t.normalised ? MakeTask(norm) : MakeUnnormalisedTask();

            var keyed = t.entries
                .Select((e, i) => (Key: $"C{i}", e.state, e.raw))
                .ToList();

            // Built DIRECTLY from (state, raw): a NoResult row keeps its
            // generated raw instead of collapsing to 0 (story trap 1).
            var results = keyed.ToDictionary(
                k => k.Key,
                k => new TaskResult(k.state, null, k.raw));

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            if (!results.Keys.ToHashSet()
                    .SetEquals(group.PreNormalisationScores.Keys))
                return false;

            return keyed.All(k =>
                group.PreNormalisationScores[k.Key] == results[k.Key].RawScore);
        });
    }

    // P2 — pass-through transparency: when task.Normalise is null the map
    // equals the final results — ∀k: PreNormalisationScores[k] ==
    // Results[k].RawScore; they are the same number there (story D1). Added
    // as a distinct invariant from P1 on explicit user request: P1 pins
    // map-vs-input, P2 pins map-vs-output for the identity branch.
    [Fact]
    public void Pass_through_map_equals_final_results()
    {
        SignedEntry.Array[1, 6].Sample(entries =>
        {
            var task = MakeUnnormalisedTask();

            var keyed = entries
                .Select((e, i) => (Key: $"C{i}", e.state, e.raw))
                .ToList();

            var results = keyed.ToDictionary(
                k => k.Key,
                k => new TaskResult(k.state, null, k.raw));

            var group = NormalisationEngine.Normalise(
                "G1", results.ToImmutableDictionary(), task, EmptyBindings);

            if (!results.Keys.ToHashSet()
                    .SetEquals(group.PreNormalisationScores.Keys))
                return false;

            return keyed.All(k =>
                group.PreNormalisationScores[k.Key] == group.Results[k.Key].RawScore);
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
