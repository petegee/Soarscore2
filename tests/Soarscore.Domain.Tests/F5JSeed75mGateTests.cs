using AwesomeAssertions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-1 of kanban/in-progress/f5j-christchurch-parallel-run-witness.md
/// (decision 3): canonical F5J encodes 5.5.11.7 d ("nose not at rest within
/// 75 m of the designated landing spot" → flight = 0, docs/rules/f5j.md:37)
/// via a landedWithin75m Flag plus a flightValidWhen gate on both tasks,
/// copied from the in-tree precedent SeedF5jNdc.cs:57/:107.
/// The absence resolves compliant (whenNotRecorded: true — the
/// exception-recording policy P1, mirroring GS's own LandingOver75m column),
/// so the fix is numerically inert on flights that record no exception.
/// Black-box style follows NzNdcSeedArithmeticTests: seed TaskDefinitions
/// resolved through ParameterResolver, evaluated by FlightInterpreter.
/// </summary>
public class F5JSeed75mGateTests
{
    [Fact]
    public void Both_tasks_declare_landedWithin75m_as_an_assumed_true_flag()
    {
        var tasks = SeedF5J.Definition.Phases.SelectMany(p => p.Tasks).ToArray();
        tasks.Should().HaveCount(2, "the preliminary and the fly-off tasks carry the same flight metrics");

        foreach (var task in tasks)
        {
            var metric = task.Metrics.Single(m => m.Name == "landedWithin75m");
            metric.Kind.Should().Be(MeasuredKind.Flag);
            metric.WhenNotRecorded.Should().Be(
                MeasuredValue.Of(true),
                "an unrecorded 75 m flag is compliance, never a pending flight");
        }
    }

    [Theory]
    [InlineData(0, "preliminary")]
    [InlineData(1, "fly-off")]
    public void Landing_beyond_75m_zeroes_the_flight(int phaseIndex, string _)
    {
        // 5.5.11.7 d via FlightValidWhen — otherwise a clean 550 round
        // (600 flight + 50 landing − 100 start-height deduction at 200 m).
        var task = ResolveF5JTask(phaseIndex);

        var result = FlightInterpreter.Interpret(task, 1, F5JMetrics(within75m: false));

        result.Score.Should().Be(0m);
    }

    [Theory]
    [InlineData(0, "preliminary")]
    [InlineData(1, "fly-off")]
    public void Landing_within_75m_keeps_the_full_round(int phaseIndex, string _)
    {
        var task = ResolveF5JTask(phaseIndex);

        var result = FlightInterpreter.Interpret(task, 1, F5JMetrics(within75m: true));

        result.Score.Should().Be(550m);
    }

    [Fact]
    public void An_unrecorded_75m_flag_scores_exactly_as_an_explicit_within_capture()
    {
        // P1 exception-recording: absence is the compliant value, so a flight
        // that records no 75 m exception must score, never pend.
        var definition = SeedF5J.Definition;
        var task = definition.Phases[0].Tasks[0];

        var bare = MetricAbsenceFixtures.OpenEntry(flightCount: 1);
        bare = CaptureAllBut75m(bare, task);
        var explicitCompliant = MetricAbsenceFixtures.Capture(
            CaptureAllBut75m(MetricAbsenceFixtures.OpenEntry(flightCount: 1), task),
            1, "landedWithin75m", MeasuredValue.Of(true), task.Metrics);

        var bareScore = MetricAbsenceFixtures.Score(task, definition, bare);
        var explicitScore = MetricAbsenceFixtures.Score(task, definition, explicitCompliant);

        var bareRow = bareScore.Results[MetricAbsenceFixtures.RowKey(0)];
        var explicitRow = explicitScore.Results[MetricAbsenceFixtures.RowKey(0)];
        bareRow.State.Should().Be(explicitRow.State);
        bareScore.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)].Should().Be(550m);
        bareScore.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)].Should().Be(
            explicitScore.PreNormalisationScores[MetricAbsenceFixtures.RowKey(0)]);
        bareRow.RawScore.Should().Be(explicitRow.RawScore);
    }

    // ------------------------------------------------------ helpers

    private static Entry CaptureAllBut75m(Entry entry, TaskDefinition task)
    {
        entry = MetricAbsenceFixtures.Capture(entry, 1, "flightTime", MeasuredValue.Of(600m), task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "startHeight", MeasuredValue.Of(200m), task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "startHeightRecorded", MeasuredValue.Of(true), task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "landingDistance", MeasuredValue.Of(1m), task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "overflySeconds", MeasuredValue.Of(0m), task.Metrics);
        entry = MetricAbsenceFixtures.Capture(entry, 1, "touchedByCompetitor", MeasuredValue.Of(false), task.Metrics);
        return entry;
    }

    private static ResolvedTask ResolveF5JTask(int phaseIndex)
    {
        var task = SeedF5J.Definition.Phases[phaseIndex].Tasks[0];
        return ParameterResolver.ResolveTask(task, new Dictionary<string, MeasuredValue>(), []);
    }

    private static Dictionary<string, MeasuredValue> F5JMetrics(bool within75m) => new()
    {
        ["flightTime"] = MeasuredValue.Of(600m),
        ["startHeight"] = MeasuredValue.Of(200m),
        ["startHeightRecorded"] = MeasuredValue.Of(true),
        ["landingDistance"] = MeasuredValue.Of(1m),
        ["overflySeconds"] = MeasuredValue.Of(0m),
        ["touchedByCompetitor"] = MeasuredValue.Of(false),
        ["landedWithin75m"] = MeasuredValue.Of(within75m),
    };
}
