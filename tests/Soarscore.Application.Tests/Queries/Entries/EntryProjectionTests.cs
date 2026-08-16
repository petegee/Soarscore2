// docs/plans/capture-a-score-steel-thread-plan.md WI-7's own "Verify": feed
// EntryOpened and check the built summary, then feed one of the other five
// event types against a non-null summary and assert the second call is a
// no-op rather than a throw — EntryProjection.Apply's default arm is
// `_ => current`, mirroring Competitions/CompetitionProjectionTests.cs, and
// for the same reason: FlightOpened/MeasurementCaptured land on every Entry
// stream this thread's own commands create (WI-8), so a throwing default
// would crash the projection on ordinary use, not just future event types.

using AwesomeAssertions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Application.Tests.Queries.Entries;

public class EntryProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = Now,
        End = Now.AddMinutes(10),
    };

    private static EntryOpened SampleOpened() =>
        new(
            EntryId.New(),
            SampleWorkingTime,
            CompetitionId.New(),
            1,
            2,
            3,
            GroupId.New(),
            CompetitorId.New(),
            ReflightRole.Original,
            Now);

    [Fact]
    public void EntryOpened_builds_the_expected_summary()
    {
        var @event = SampleOpened();

        var summary = EntryProjection.Apply(null, @event);

        summary.Should().NotBeNull();
        summary!.Id.Should().Be(@event.Id);
        summary.CompetitionRef.Should().Be(@event.CompetitionRef);
        summary.PhaseOrdinal.Should().Be(@event.PhaseOrdinal);
        summary.RoundOrdinal.Should().Be(@event.RoundOrdinal);
        summary.TaskRoundOrdinal.Should().Be(@event.TaskRoundOrdinal);
        summary.GroupRef.Should().Be(@event.GroupRef);
        summary.CompetitorRef.Should().Be(@event.CompetitorRef);
        summary.Role.Should().Be(@event.Role);
    }

    [Fact]
    public void FlightOpened_against_a_non_null_summary_is_a_no_op()
    {
        var summary = EntryProjection.Apply(null, SampleOpened())!;

        var result = EntryProjection.Apply(summary, new FlightOpened(1, Now, Now));

        result.Should().BeSameAs(summary);
    }

    [Fact]
    public void MeasurementCaptured_against_a_non_null_summary_is_a_no_op()
    {
        var summary = EntryProjection.Apply(null, SampleOpened())!;
        var measurement = new Measurement
        {
            Metric = "flightTime",
            Value = MeasuredValue.Of(123.4m),
            CapturedAt = Now,
        };

        var result = EntryProjection.Apply(summary, new MeasurementCaptured(1, measurement));

        result.Should().BeSameAs(summary);
    }

    [Fact]
    public void An_out_of_scope_event_type_against_a_null_summary_is_also_a_no_op_rather_than_a_throw()
    {
        var result = EntryProjection.Apply(null, new EntryAnnulled(new Annulment { Reason = "duplicate", By = "CD", At = Now }));

        result.Should().BeNull();
    }
}
