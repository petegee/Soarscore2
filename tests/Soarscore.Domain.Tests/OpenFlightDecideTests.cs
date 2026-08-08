using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Entry.OpenFlight"/> —
/// docs/plans/capture-a-score-steel-thread-plan.md WI-3. One per failure
/// code, plus success, plus the sequence advancing across successive folds,
/// plus the finding-3 regression test: a launch outside the working time is
/// recorded, not refused.
/// </summary>
public class OpenFlightDecideTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 1, 10, 9, 10, 0, TimeSpan.Zero),
    };

    private static Entry OpenEntry() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleWorkingTime, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

    private static Entry AnnulledEntry() =>
        OpenEntry().Apply(new EntryAnnulled(
            new Annulment { Reason = "provisional re-flight ruling", By = "Jury", At = DateTimeOffset.UtcNow }));

    [Fact]
    public void OpenFlight_against_an_annulled_entry_fails_with_a_stable_code()
    {
        var entry = AnnulledEntry();

        var result = entry.OpenFlight(1, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.annulled");
    }

    [Fact]
    public void OpenFlight_with_a_sequence_that_is_not_next_fails_with_a_stable_code()
    {
        var entry = OpenEntry();

        var result = entry.OpenFlight(2, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openFlight.sequenceOutOfOrder");
    }

    [Fact]
    public void OpenFlight_beyond_maxLaunches_fails_with_a_stable_code()
    {
        var entry = OpenEntry();
        var first = entry.OpenFlight(1, DateTimeOffset.UtcNow, maxLaunches: 1, at: DateTimeOffset.UtcNow);
        first.IsSuccess.Should().BeTrue();
        entry = entry.Apply(first.Value);

        var second = entry.OpenFlight(2, DateTimeOffset.UtcNow, maxLaunches: 1, at: DateTimeOffset.UtcNow);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("openFlight.maxLaunchesExceeded");
    }

    [Fact]
    public void OpenFlight_succeeds_and_carries_sequence_and_launchAt_through()
    {
        var entry = OpenEntry();
        var launchAt = new DateTimeOffset(2026, 1, 10, 9, 3, 0, TimeSpan.Zero);
        var at = new DateTimeOffset(2026, 1, 10, 9, 3, 5, TimeSpan.Zero);

        var result = entry.OpenFlight(1, launchAt, maxLaunches: null, at: at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sequence.Should().Be(1);
        result.Value.LaunchAt.Should().Be(launchAt);
        result.Value.At.Should().Be(at);
    }

    [Fact]
    public void OpenFlight_with_no_maxLaunches_accepts_an_unbounded_run()
    {
        var entry = OpenEntry();

        for (var sequence = 1; sequence <= 6; sequence++)
        {
            var result = entry.OpenFlight(sequence, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);
            result.IsSuccess.Should().BeTrue();
            entry = entry.Apply(result.Value);
        }

        entry.Flights.Length.Should().Be(6);
    }

    [Fact]
    public void OpenFlight_sequence_advances_1_2_3_across_successive_folds()
    {
        var entry = OpenEntry();

        var first = entry.OpenFlight(1, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);
        first.IsSuccess.Should().BeTrue();
        first.Value.Sequence.Should().Be(1);
        entry = entry.Apply(first.Value);

        var second = entry.OpenFlight(2, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);
        second.IsSuccess.Should().BeTrue();
        second.Value.Sequence.Should().Be(2);
        entry = entry.Apply(second.Value);

        var third = entry.OpenFlight(3, DateTimeOffset.UtcNow, maxLaunches: null, at: DateTimeOffset.UtcNow);
        third.IsSuccess.Should().BeTrue();
        third.Value.Sequence.Should().Be(3);
        entry = entry.Apply(third.Value);

        entry.Flights.Select(f => f.Sequence).Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// The finding-3 regression test: F3K.7 scores a launch before the
    /// working time begins, it does not refuse it. OpenFlight must not gate
    /// on the working-time window — a launch minutes before Start (and one
    /// after End) both succeed here.
    /// </summary>
    [Fact]
    public void OpenFlight_with_a_launch_outside_the_working_time_succeeds_finding_3_regression()
    {
        var entry = OpenEntry();
        var beforeWorkingTime = SampleWorkingTime.Start.AddMinutes(-5);

        var early = entry.OpenFlight(1, beforeWorkingTime, maxLaunches: null, at: DateTimeOffset.UtcNow);

        early.IsSuccess.Should().BeTrue();
        early.Value.LaunchAt.Should().Be(beforeWorkingTime);
        entry = entry.Apply(early.Value);

        var afterWorkingTime = SampleWorkingTime.End!.Value.AddMinutes(5);

        var late = entry.OpenFlight(2, afterWorkingTime, maxLaunches: null, at: DateTimeOffset.UtcNow);

        late.IsSuccess.Should().BeTrue();
        late.Value.LaunchAt.Should().Be(afterWorkingTime);
    }
}
