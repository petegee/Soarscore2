using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;
using Predicate = Soarscore.Domain.PublishedClassDefinition.Predicate;
using ScoreTerm = Soarscore.Domain.PublishedClassDefinition.ScoreTerm;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-6 of kanban/completed/landing-zero-and-flyaway-encoding.md: the pins
/// for the two paper-convention encodings, driven through the real seed
/// definitions in the NzNdcSeedArithmeticTests black-box style (seed
/// TaskDefinitions resolved through ParameterResolver, evaluated by
/// FlightInterpreter).
///
/// (i) *zero row*: an entered 0 on landing scores zero landing points — the
/// paper "beyond the tape" convention, reserving the physically-impossible
/// exact-0 reading. A blank cell stays "no result" (unchanged).
/// (ii) *horn*: a stopwatch reading that reaches the working time (600
/// prelim / 900 fly-off / 540 F5L) means the model flew away — the flight
/// caps at W−1 and earns no landing, so the flyaway never out-scores a
/// landed W−1.
///
/// Grain note: classes whose landing bonus lives in Score (every class but
/// 80-nz-m-ales200) are pinned at the flight grain through
/// <see cref="FlightInterpreter.Interpret"/> term contributions; 80 lands its
/// bonus in ScoreNormalised (F24), so its pin goes through
/// <see cref="ScoringService.ScoreGroup"/>.
/// </summary>
public class LandingZeroAndFlyawayTests
{
    // ------------------------------------------------------------ helpers

    private static decimal DirectAward(IReadOnlyList<LookupRow> rows, decimal d)
    {
        foreach (var row in rows)
            if (row.UpTo is null || d <= row.UpTo.Value)
                return row.Points;
        throw new InvalidOperationException("Lookup rows cover every distance; unreachable.");
    }

    private sealed record LandingTable(string FileName, string TaskCode, Predicate? When, LookupTerm Lookup);

    /// <summary>
    /// Every landing lookup in the corpus, extracted from
    /// <see cref="Corpus.All"/> (never re-transcribed) — Score and
    /// ScoreNormalised stages alike.
    /// </summary>
    private static ImmutableArray<LandingTable> LandingTables()
    {
        var builder = ImmutableArray.CreateBuilder<LandingTable>();
        foreach (var seed in Corpus.All)
            foreach (var phase in seed.Definition.Phases)
                foreach (var task in phase.Tasks)
                {
                    Collect(task.Code, task.Score, builder, seed.FileName);
                    Collect(task.Code, task.ScoreNormalised, builder, seed.FileName);
                }
        return builder.ToImmutable();

        static void Collect(string taskCode, ImmutableArray<ScoreTerm> terms,
            ImmutableArray<LandingTable>.Builder builder, string fileName)
        {
            foreach (var term in terms)
                CollectTerm(taskCode, term, When: null, builder, fileName);
        }

        static void CollectTerm(string taskCode, ScoreTerm term, Predicate? When,
            ImmutableArray<LandingTable>.Builder builder, string fileName)
        {
            switch (term)
            {
                case LookupTerm lookup when lookup.MetricRef == "landingDistance":
                    builder.Add(new LandingTable(fileName, taskCode, When, lookup));
                    break;
                case ConditionalTerm conditional:
                    CollectTerm(taskCode, conditional.Then, conditional.When, builder, fileName);
                    if (conditional.Else is not null)
                        CollectTerm(taskCode, conditional.Else, When, builder, fileName);
                    break;
            }
        }
    }

    private static int LandingTermIndex(ResolvedTask task)
    {
        for (var i = 0; i < task.Score.Length; i++)
        {
            if (task.Score[i] is LookupTerm lookup && lookup.MetricRef == "landingDistance")
                return i;
            if (task.Score[i] is ConditionalTerm conditional
                && conditional.Then is LookupTerm then && then.MetricRef == "landingDistance")
                return i;
        }
        throw new InvalidOperationException($"Task {task.Code} scores no landingDistance term.");
    }

    private static int FlightTermIndex(ResolvedTask task)
    {
        for (var i = 0; i < task.Score.Length; i++)
        {
            var term = task.Score[i] is ConditionalTerm conditional ? conditional.Then : task.Score[i];
            if (term is RateTerm rate && rate.MetricRef == "flightTime")
                return i;
            if (term is PiecewiseTerm piecewise && piecewise.MetricRef == "flightTime")
                return i;
        }
        throw new InvalidOperationException($"Task {task.Code} scores no flightTime term.");
    }

    private static decimal LandingPoints(ResolvedTask task, Dictionary<string, MeasuredValue> metrics) =>
        FlightInterpreter.Interpret(task, 1, metrics).TermContributions[LandingTermIndex(task)].Points;

    private static decimal Total(ResolvedTask task, Dictionary<string, MeasuredValue> metrics) =>
        FlightInterpreter.Interpret(task, 1, metrics).Score;

    // ------------------------------------------------------------ zero-row unit pins

    /// <summary>
    /// One compliant flight per Score-stage landing table: a mid-range flight
    /// with every gate passing, varying only the landing distance.
    /// </summary>
    private static (ResolvedTask Task, Func<decimal, Dictionary<string, MeasuredValue>> Metrics) LandingCase(string fileName)
    {
        Dictionary<string, MeasuredValue> F5J(decimal landing) => new()
        {
            ["flightTime"] = MeasuredValue.Of(500m),
            ["startHeight"] = MeasuredValue.Of(0m),
            ["landingDistance"] = MeasuredValue.Of(landing),
            ["overflySeconds"] = MeasuredValue.Of(0m),
            ["touchedByCompetitor"] = MeasuredValue.Of(false),
            ["landedWithin75m"] = MeasuredValue.Of(true),
        };
        return fileName switch
        {
            "85c-nz-f5j-ndc" => (
                ParameterResolver.ResolveTask(
                    SeedF5jNdc.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
                    new Dictionary<string, MeasuredValue>(), []),
                F5J),
            "30-f5j" => (
                ParameterResolver.ResolveTask(
                    SeedF5J.Definition.Phases[0].Tasks[0],
                    new Dictionary<string, MeasuredValue>(), []),
                F5J),
            "50-f3j" => (
                ParameterResolver.ResolveTask(
                    SeedF3J.Definition.Phases[0].Tasks[0],
                    new Dictionary<string, MeasuredValue>(), []),
                (decimal landing) => new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(500m),
                    ["landingDistance"] = MeasuredValue.Of(landing),
                    ["overflySeconds"] = MeasuredValue.Of(0m),
                    ["touchedByCompetitor"] = MeasuredValue.Of(false),
                    ["restedWithin75m"] = MeasuredValue.Of(true),
                }),
            "20-f3b" => (
                ParameterResolver.ResolveTask(
                    SeedF3B.Definition.Phases[0].Tasks.Single(t => t.Code == "A"),
                    new Dictionary<string, MeasuredValue>(), []),
                (decimal landing) => new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(540m),
                    ["landedInDefinedArea"] = MeasuredValue.Of(true),
                    ["landingDistance"] = MeasuredValue.Of(landing),
                    ["atRestBy12Min"] = MeasuredValue.Of(true),
                    ["touchedByCompetitor"] = MeasuredValue.Of(false),
                }),
            "60-f5l" => (
                ParameterResolver.ResolveTask(
                    SeedF5L.Definition.Phases[0].Tasks[0],
                    new Dictionary<string, MeasuredValue> { ["groupSize"] = MeasuredValue.Of(10m) },
                    SeedF5L.Definition.Parameters),
                (decimal landing) => new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(300m),
                    ["landingDistance"] = MeasuredValue.Of(landing),
                    ["overflySeconds"] = MeasuredValue.Of(0m),
                    ["landedInLandingArea"] = MeasuredValue.Of(true),
                    ["lostPart"] = MeasuredValue.Of(false),
                    ["touchedByCompetitor"] = MeasuredValue.Of(false),
                    ["touchedBeforeMeasuring"] = MeasuredValue.Of(false),
                    ["amrtPresetsCorrect"] = MeasuredValue.Of(true),
                    ["timingDeviationInFavour"] = MeasuredValue.Of(false),
                }),
            "81-nz-m-ndc" => (
                ParameterResolver.ResolveTask(
                    SeedNzMNdc.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
                    new Dictionary<string, MeasuredValue>(), []),
                (decimal landing) => new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(300m),
                    ["landingDistance"] = MeasuredValue.Of(landing),
                    ["damagedAndNotSafelyFlyable"] = MeasuredValue.Of(false),
                    ["touchedByCompetitor"] = MeasuredValue.Of(false),
                }),
            "83-nz-n-ales123" => (
                ParameterResolver.ResolveTask(
                    SeedNzNAles123.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
                    new Dictionary<string, MeasuredValue> { ["roundDuration"] = MeasuredValue.Of(360m) },
                    SeedNzNAles123.Definition.Parameters),
                AlesLanding),
            "85-nz-p-radian" => (
                ParameterResolver.ResolveTask(
                    SeedNzPRadian.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
                    new Dictionary<string, MeasuredValue> { ["roundDuration"] = MeasuredValue.Of(420m) },
                    SeedNzPRadian.Definition.Parameters),
                AlesLanding),
            "86-nz-x5j" => (
                ParameterResolver.ResolveTask(
                    SeedX5j.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
                    new Dictionary<string, MeasuredValue> { ["minNewGroup"] = MeasuredValue.Of(4m) }, []),
                (decimal landing) => new Dictionary<string, MeasuredValue>
                {
                    ["glideTime"] = MeasuredValue.Of(300m),
                    ["motorRestartRunTime"] = MeasuredValue.Of(0m),
                    ["motorRestarted"] = MeasuredValue.Of(false),
                    ["airborneAtRoundEnd"] = MeasuredValue.Of(false),
                    ["landedWithin75m"] = MeasuredValue.Of(true),
                    ["landingDistance"] = MeasuredValue.Of(landing),
                }),
            "87-nz-h-thermal-2m" => (
                ParameterResolver.ResolveTask(
                    SeedNzHThermal2m.Definition.Phases[0].Tasks.Single(t => t.Code == "3"),
                    new Dictionary<string, MeasuredValue>(), []),
                (decimal landing) => new Dictionary<string, MeasuredValue>
                {
                    ["flightTime"] = MeasuredValue.Of(100m),
                    ["landingDistance"] = MeasuredValue.Of(landing),
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "No Score-stage landing table."),
        };

        static Dictionary<string, MeasuredValue> AlesLanding(decimal landing) => new()
        {
            ["flightTime"] = MeasuredValue.Of(200m),
            ["landingDistance"] = MeasuredValue.Of(landing),
            ["motorRestarted"] = MeasuredValue.Of(false),
            ["airborneAtRoundEnd"] = MeasuredValue.Of(false),
        };
    }

    [Theory]
    [InlineData("85c-nz-f5j-ndc", 1, 50)]
    [InlineData("81-nz-m-ndc", 1, 50)]
    [InlineData("30-f5j", 1, 50)]
    [InlineData("50-f3j", 0.2, 100)]
    [InlineData("20-f3b", 1, 100)]
    [InlineData("60-f5l", 0.2, 100)]
    [InlineData("83-nz-n-ales123", 1, 50)]
    [InlineData("85-nz-p-radian", 1, 50)]
    [InlineData("86-nz-x5j", 1, 50)]
    [InlineData("87-nz-h-thermal-2m", 1, 50)]
    public void Entered_zero_on_landing_scores_zero_while_the_top_award_is_unchanged(
        string fileName, decimal topDistance, decimal topAward)
    {
        // The exact-0 reading is physically impossible, so the paper
        // beyond-the-tape convention reserves it: distance 0 earns exactly 0
        // landing points, while the first band above zero keeps its legacy
        // award (50, or 100 on the F3J/F5L tenth-metre tables).
        var (task, metrics) = LandingCase(fileName);

        LandingPoints(task, metrics(0m)).Should().Be(0m,
            $"{fileName}: an entered 0 is beyond the tape, not a spot landing");
        LandingPoints(task, metrics(topDistance)).Should().Be(topAward,
            $"{fileName}: the first band above zero keeps its legacy award");
    }

    [Fact]
    public void Entered_zero_on_landing_scores_zero_on_the_normalised_ales200_table()
    {
        // 80-nz-m-ales200 lands its bonus in ScoreNormalised (F24) — past the
        // flight grain FlightInterpreter scores — so this pins it through
        // ScoreGroup instead: two identical 300 s flights (raw 300 each,
        // normalised 1000 each) differing only in landing. 0 m must score
        // exactly as 100 m (beyond the tape: Rest 0); 1 m must beat 100 m by
        // exactly the 50-point top award.
        var definition = SeedNzMAles200.Definition;
        var task = definition.Phases[0].Tasks.Single(t => t.Code == "D");
        var bindings = new Dictionary<string, MeasuredValue> { ["groupSize"] = MeasuredValue.Of(10m) };

        Entry Flight(decimal landing)
        {
            var entry = MetricAbsenceFixtures.OpenEntry(1);
            foreach (var (name, value) in new Dictionary<string, MeasuredValue>
            {
                ["flightTime"] = MeasuredValue.Of(300m),
                ["landingDistance"] = MeasuredValue.Of(landing),
                ["damagedAndNotSafelyFlyable"] = MeasuredValue.Of(false),
                ["touchedByCompetitor"] = MeasuredValue.Of(false),
                ["landedWithin75m"] = MeasuredValue.Of(true),
            })
                entry = MetricAbsenceFixtures.Capture(entry, 1, name, value, task.Metrics);
            return entry;
        }

        decimal ScoreOf(Entry first, Entry second, string key)
        {
            var entries = ImmutableDictionary<string, Entry>.Empty
                .Add("a", first).Add("b", second);
            return ScoringService.ScoreGroup("group", task, definition, entries, bindings).Results[key].RawScore;
        }

        ScoreOf(Flight(0m), Flight(100m), "a").Should().Be(
            ScoreOf(Flight(0m), Flight(100m), "b"),
            "an entered 0 is beyond the tape: zero landing points, like 100 m");
        (ScoreOf(Flight(1m), Flight(100m), "a") - ScoreOf(Flight(1m), Flight(100m), "b"))
            .Should().Be(50m, "the first band above zero keeps its legacy award");
    }

    [Fact]
    public void Blank_landing_still_yields_no_result()
    {
        // The accepted half of the convention: a blank cell is not a zero, it
        // is an absence. At the flight grain the flight pends awaiting
        // landingDistance; at the task-round grain the row is NoResult —
        // unchanged by the zero-row edit.
        var definition = SeedF5jNdc.Definition;
        var declared = definition.Phases[0].Tasks.Single(t => t.Code == "D");
        var task = ParameterResolver.ResolveTask(declared, new Dictionary<string, MeasuredValue>(), []);

        var entry = MetricAbsenceFixtures.OpenEntry(1);
        foreach (var (name, value) in new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(500m),
            ["startHeight"] = MeasuredValue.Of(0m),
            ["overflySeconds"] = MeasuredValue.Of(0m),
            ["touchedByCompetitor"] = MeasuredValue.Of(false),
            ["landedWithin75m"] = MeasuredValue.Of(true),
        })
            entry = MetricAbsenceFixtures.Capture(entry, 1, name, value, declared.Metrics);

        var flight = FlightMetricResolution.InterpretAllFlights(entry, task).Single();
        flight.Result.State.Should().Be(FlightResultState.Pending);
        flight.Result.Awaited.Should().NotBeNull();
        flight.Result.Awaited!.AwaitedMetric.Should().Be("landingDistance");

        var row = MetricAbsenceFixtures.Score(declared, definition, entry).Results[MetricAbsenceFixtures.RowKey(0)];
        row.State.Should().Be(TaskResultState.NoResult);
        row.AwaitingCapture.Should().Equal(new PendingFlightDiagnostic(1, "landingDistance"));
    }

    // ------------------------------------------------------------ horn unit pins

    private static Dictionary<string, MeasuredValue> F5JHorn(decimal flightTime, decimal landing) => new()
    {
        ["flightTime"] = MeasuredValue.Of(flightTime),
        ["startHeight"] = MeasuredValue.Of(0m),
        ["landingDistance"] = MeasuredValue.Of(landing),
        ["overflySeconds"] = MeasuredValue.Of(0m),
        ["touchedByCompetitor"] = MeasuredValue.Of(false),
        ["landedWithin75m"] = MeasuredValue.Of(true),
    };

    private static Dictionary<string, MeasuredValue> F3JHorn(decimal flightTime, decimal landing) => new()
    {
        ["flightTime"] = MeasuredValue.Of(flightTime),
        ["landingDistance"] = MeasuredValue.Of(landing),
        ["overflySeconds"] = MeasuredValue.Of(0m),
        ["touchedByCompetitor"] = MeasuredValue.Of(false),
        ["restedWithin75m"] = MeasuredValue.Of(true),
    };

    private static Dictionary<string, MeasuredValue> F5LHorn(decimal flightTime, decimal landing) => new()
    {
        ["flightTime"] = MeasuredValue.Of(flightTime),
        ["landingDistance"] = MeasuredValue.Of(landing),
        ["overflySeconds"] = MeasuredValue.Of(0m),
        ["landedInLandingArea"] = MeasuredValue.Of(true),
        ["lostPart"] = MeasuredValue.Of(false),
        ["touchedByCompetitor"] = MeasuredValue.Of(false),
        ["touchedBeforeMeasuring"] = MeasuredValue.Of(false),
        ["amrtPresetsCorrect"] = MeasuredValue.Of(true),
        ["timingDeviationInFavour"] = MeasuredValue.Of(false),
    };

    private static ResolvedTask ResolveF5LTask(int phaseIndex) =>
        ParameterResolver.ResolveTask(
            SeedF5L.Definition.Phases[phaseIndex].Tasks[0],
            new Dictionary<string, MeasuredValue> { ["groupSize"] = MeasuredValue.Of(10m) },
            SeedF5L.Definition.Parameters);

    [Fact]
    public void Horn_85c_prelim_600_is_a_flyaway()
    {
        // W−1 keeps the landing: 599 flight + 50 landing = 649. W is a
        // flyaway: flight capped at 599, no landing — 599, which never
        // out-scores the landed 599. W+1 pins the cap from above.
        var task = ParameterResolver.ResolveTask(
            SeedF5jNdc.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
            new Dictionary<string, MeasuredValue>(), []);

        Total(task, F5JHorn(599m, 1m)).Should().Be(649m);
        LandingPoints(task, F5JHorn(599m, 1m)).Should().Be(50m);
        Total(task, F5JHorn(600m, 1m)).Should().Be(599m);
        LandingPoints(task, F5JHorn(600m, 1m)).Should().Be(0m);
        Total(task, F5JHorn(601m, 1m)).Should().Be(599m);
    }

    [Fact]
    public void Horn_30_f5j_prelim_600_is_a_flyaway()
    {
        // Same arithmetic as 85c through the canonical task: 649 landed at
        // 599, 599 flyaway at 600, cap holding at 601.
        var task = ParameterResolver.ResolveTask(
            SeedF5J.Definition.Phases[0].Tasks[0],
            new Dictionary<string, MeasuredValue>(), []);

        Total(task, F5JHorn(599m, 1m)).Should().Be(649m);
        Total(task, F5JHorn(600m, 1m)).Should().Be(599m);
        LandingPoints(task, F5JHorn(600m, 1m)).Should().Be(0m);
        Total(task, F5JHorn(601m, 1m)).Should().Be(599m);
    }

    [Fact]
    public void Horn_30_f5j_flyoff_900_is_a_flyaway()
    {
        // The fly-off horn is 900 with the 899 cap: 899 + 50 = 949 landed at
        // 899; 899 flyaway at 900; cap holding at 901.
        var task = ParameterResolver.ResolveTask(
            SeedF5J.Definition.Phases[1].Tasks[0],
            new Dictionary<string, MeasuredValue>(), []);

        Total(task, F5JHorn(899m, 1m)).Should().Be(949m);
        LandingPoints(task, F5JHorn(899m, 1m)).Should().Be(50m);
        Total(task, F5JHorn(900m, 1m)).Should().Be(899m);
        LandingPoints(task, F5JHorn(900m, 1m)).Should().Be(0m);
        Total(task, F5JHorn(901m, 1m)).Should().Be(899m);
    }

    [Fact]
    public void Horn_50_f3j_prelim_600_is_a_flyaway()
    {
        // The tenth-metre table pays 100: 599 + 100 = 699 landed at 599; 599
        // flyaway at 600; cap holding at 601.
        var task = ParameterResolver.ResolveTask(
            SeedF3J.Definition.Phases[0].Tasks[0],
            new Dictionary<string, MeasuredValue>(), []);

        Total(task, F3JHorn(599m, 0.2m)).Should().Be(699m);
        Total(task, F3JHorn(600m, 0.2m)).Should().Be(599m);
        LandingPoints(task, F3JHorn(600m, 0.2m)).Should().Be(0m);
        Total(task, F3JHorn(601m, 0.2m)).Should().Be(599m);
    }

    [Fact]
    public void Horn_50_f3j_flyoff_900_is_a_flyaway()
    {
        // 899 + 100 = 999 landed at 899; 899 flyaway at 900; cap at 901.
        var task = ParameterResolver.ResolveTask(
            SeedF3J.Definition.Phases[1].Tasks[0],
            new Dictionary<string, MeasuredValue>(), []);

        Total(task, F3JHorn(899m, 0.2m)).Should().Be(999m);
        Total(task, F3JHorn(900m, 0.2m)).Should().Be(899m);
        LandingPoints(task, F3JHorn(900m, 0.2m)).Should().Be(0m);
        Total(task, F3JHorn(901m, 0.2m)).Should().Be(899m);
    }

    [Theory]
    [InlineData(0, "preliminary")]
    [InlineData(1, "fly-off")]
    public void Horn_60_f5l_540_is_a_flyaway(int phaseIndex, string _)
    {
        // The 540 s horn on the 2 pt/s piecewise (780 − 2(s−390) past 390 s):
        // 539 lands 482 + 100 = 582; 540 flies 480 with no landing; 541
        // declines to 478 — the flyaway never out-scores the landed 539.
        // The fly-off restates the same task (SeedF5L FlyoffTaskD), so both
        // phases pin the same numbers.
        var task = ResolveF5LTask(phaseIndex);

        Total(task, F5LHorn(539m, 0.2m)).Should().Be(582m);
        LandingPoints(task, F5LHorn(539m, 0.2m)).Should().Be(100m);
        Total(task, F5LHorn(540m, 0.2m)).Should().Be(480m);
        LandingPoints(task, F5LHorn(540m, 0.2m)).Should().Be(0m);
        Total(task, F5LHorn(541m, 0.2m)).Should().Be(478m);
    }

    [Fact]
    public void Sub_second_overfly_lost_to_truncation_still_fails_the_horn_test()
    {
        // 5.5.11.12 b truncates the flight to the second at capture: a 600.4 s
        // stopwatch reading captures flightTime 600 — inside the [600, 601)
        // defect band the story names — and scores the flyaway end to end:
        // 599 flight, no landing, − 100 at a 200 m launch = 499, even though
        // a 1 m landing was entered.
        var definition = SeedF5jNdc.Definition;
        var task = definition.Phases[0].Tasks.Single(t => t.Code == "D");

        var entry = MetricAbsenceFixtures.OpenEntry(1);
        foreach (var (name, value) in new Dictionary<string, MeasuredValue>
        {
            ["flightTime"] = MeasuredValue.Of(600.4m),
            ["startHeight"] = MeasuredValue.Of(200m),
            ["landingDistance"] = MeasuredValue.Of(1m),
            ["overflySeconds"] = MeasuredValue.Of(0m),
            ["touchedByCompetitor"] = MeasuredValue.Of(false),
            ["landedWithin75m"] = MeasuredValue.Of(true),
        })
            entry = MetricAbsenceFixtures.Capture(entry, 1, name, value, task.Metrics);

        var row = MetricAbsenceFixtures.Score(task, definition, entry).Results[MetricAbsenceFixtures.RowKey(0)];
        row.State.Should().Be(TaskResultState.Valid);
        row.RawScore.Should().Be(499m);
    }

    // ------------------------------------------------------------ named invariants (CsCheck)

    // Invariant (i) *zero row*: for any distance d > 0 the award equals the
    // legacy table's award — new(d) == old(d) — and new(0) == 0. The legacy
    // table is the seed's own rows without the leading row, compared through
    // the same inclusive-upper-bound walk the engine uses; the leading row
    // itself is pinned inline as {UpTo: 0, Points: 0}.
    [Fact]
    public void Zero_row_leaves_every_positive_distance_untouched_and_zeroes_exact_zero()
    {
        var tables = LandingTables();
        tables.Should().NotBeEmpty("the corpus has landing tables to pin");

        foreach (var table in tables)
            table.Lookup.Rows[0].Should().Be(
                new LookupRow(0, 0),
                $"{table.FileName}/{table.TaskCode}: the leading row is the paper-zero convention");

        (from cents in Gen.Int[1, 2500]
         select cents / 100m)
        .Sample(d =>
        {
            foreach (var table in tables)
            {
                var legacy = table.Lookup.Rows.RemoveAt(0);
                DirectAward(table.Lookup.Rows, d).Should().Be(
                    DirectAward(legacy, d),
                    $"{table.FileName}/{table.TaskCode}: d={d} must award what the legacy table awarded");
                DirectAward(table.Lookup.Rows, 0m).Should().Be(
                    0m,
                    $"{table.FileName}/{table.TaskCode}: exact 0 is beyond the tape");
            }
        });
    }

    private sealed record HornCase(
        string Label, ResolvedTask Task, int W,
        Func<decimal, Dictionary<string, MeasuredValue>> Metrics, decimal TopLanding,
        Func<decimal, decimal> ExpectedFlight,
        // The story's "flight term never exceeds 599 × rate" half: true where
        // the flight term is rate-capped (85c/30/50). F5L scores the flight
        // piecewise — declining past 390 s by rule, so a below-horn flight
        // term can exceed the W−1 term there; its never-out-scores guarantee
        // holds at the total level (the lost 100-point landing dominates).
        bool FlightNeverExceedsLandedTerm);

    private static ImmutableArray<HornCase> HornCases() =>
    [
        new("85c-nz-f5j-ndc/prelim",
            ParameterResolver.ResolveTask(
                SeedF5jNdc.Definition.Phases[0].Tasks.Single(t => t.Code == "D"),
                new Dictionary<string, MeasuredValue>(), []),
            600, s => F5JHorn(s, 1m), 50,
            s => Math.Min(s, 599m), true),
        new("30-f5j/prelim",
            ParameterResolver.ResolveTask(
                SeedF5J.Definition.Phases[0].Tasks[0],
                new Dictionary<string, MeasuredValue>(), []),
            600, s => F5JHorn(s, 1m), 50,
            s => Math.Min(s, 599m), true),
        new("30-f5j/fly-off",
            ParameterResolver.ResolveTask(
                SeedF5J.Definition.Phases[1].Tasks[0],
                new Dictionary<string, MeasuredValue>(), []),
            900, s => F5JHorn(s, 1m), 50,
            s => Math.Min(s, 899m), true),
        new("50-f3j/prelim",
            ParameterResolver.ResolveTask(
                SeedF3J.Definition.Phases[0].Tasks[0],
                new Dictionary<string, MeasuredValue>(), []),
            600, s => F3JHorn(s, 0.2m), 100,
            s => Math.Min(s, 599m), true),
        new("50-f3j/fly-off",
            ParameterResolver.ResolveTask(
                SeedF3J.Definition.Phases[1].Tasks[0],
                new Dictionary<string, MeasuredValue>(), []),
            900, s => F3JHorn(s, 0.2m), 100,
            s => Math.Min(s, 899m), true),
        // F5L scores the flight piecewise (2 pt/s to 390 s, −2 past it), so
        // the flight term is stated, not capped: 780 − 2(s−390) across the
        // swept band, which never exceeds the landed 539's 482.
        new("60-f5l/prelim",
            ResolveF5LTask(0),
            540, s => F5LHorn(s, 0.2m), 100,
            s => 780m - 2m * (s - 390m), false),
        new("60-f5l/fly-off",
            ResolveF5LTask(1),
            540, s => F5LHorn(s, 0.2m), 100,
            s => 780m - 2m * (s - 390m), false),
    ];

    // Invariant (ii) *horn*: for stopwatch readings across [W−2, W+1] a
    // reading ≥ W earns no landing, its flight term never exceeds the W−1
    // flight term, and the flyaway total never out-scores the landed W−1.
    // The sweep is integer seconds: every stopwatch-pair metric in the corpus
    // declares integer-second precision (Truncate/1 on the 85c/30/60 flight
    // times; F3J records flightTime at 0.1 s but integer readings are exact),
    // so the integer sweep meets the horn boundary faithfully. The
    // sub-second truncation band is pinned separately above, at capture.
    [Fact]
    public void Horn_readings_earn_no_landing_and_never_outscore_a_landed_W_minus_1()
    {
        foreach (var horn in HornCases())
        {
            (from s in Gen.Int[horn.W - 2, horn.W + 1]
             select (decimal)s)
            .Sample(s =>
            {
                var interpreted = FlightInterpreter.Interpret(horn.Task, 1, horn.Metrics(s));
                var landing = interpreted.TermContributions[LandingTermIndex(horn.Task)].Points;
                var flight = interpreted.TermContributions[FlightTermIndex(horn.Task)].Points;

                if (s >= horn.W)
                    landing.Should().Be(0m, $"{horn.Label}: a horn reading s={s} earns no landing");
                else
                    landing.Should().Be(horn.TopLanding, $"{horn.Label}: s={s} below the horn keeps the landing");
                flight.Should().Be(horn.ExpectedFlight(s), $"{horn.Label}: s={s} flight term");
                if (horn.FlightNeverExceedsLandedTerm)
                    flight.Should().BeLessThanOrEqualTo(
                        horn.ExpectedFlight(horn.W - 1),
                        $"{horn.Label}: s={s} flight term never exceeds the landed W−1 term");
            });

            Total(horn.Task, horn.Metrics(horn.W)).Should().BeLessThanOrEqualTo(
                Total(horn.Task, horn.Metrics(horn.W - 1)),
                $"{horn.Label}: the flyaway never out-scores a landed W−1");
        }
    }
}
