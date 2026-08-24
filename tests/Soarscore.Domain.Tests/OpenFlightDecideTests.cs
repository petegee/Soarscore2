using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Entry.OpenFlight"/> —
/// kanban/completed/capture-a-score-steel-thread-plan.md WI-3, updated by
/// kanban/in-progress/out-of-order-flight-entry.md WI-4: contiguity is no
/// longer enforced, so the old sequenceOutOfOrder case is replaced by one per
/// surviving failure code (annulled, duplicateSequence, sequenceNotPositive,
/// maxLaunchesExceeded), plus success, out-of-order success, and the sequence
/// advancing across successive folds.
///
/// The finding-3 regression test — a launch outside the working time is
/// recorded, not refused — used to live here, asserting that OpenFlight passed
/// an out-of-window launchAt through untouched. It moved to
/// CaptureMeasurementDecideTests when kanban/completed/remove-flight-launchat.md
/// removed the timestamp: OpenFlight now receives no launch instant at all, so
/// it has nothing to gate on and the assertion had nothing left to say. The
/// rule it guarded (F3K.7) travels as the `launchedInWorkingTime` metric, and
/// the test guards it there.
/// </summary>
public class OpenFlightDecideTests
{
    private static readonly EntryId SampleId = EntryId.New();
    private static readonly CompetitionId SampleCompetition = CompetitionId.New();
    private static readonly GroupId SampleGroup = GroupId.New();
    private static readonly CompetitorId SampleCompetitor = CompetitorId.New();

    private static Entry OpenEntry() =>
        Entry.Create(new EntryOpened(
            SampleId, SampleCompetition, 1, 1, 1,
            SampleGroup, SampleCompetitor, ReflightRole.Original, DateTimeOffset.UtcNow));

    private static Entry AnnulledEntry() =>
        OpenEntry().Apply(new EntryAnnulled(
            new Annulment { Reason = "provisional re-flight ruling", By = "Jury", At = DateTimeOffset.UtcNow }));

    [Fact]
    public void OpenFlight_against_an_annulled_entry_fails_with_a_stable_code()
    {
        var entry = AnnulledEntry();

        var result = entry.OpenFlight(1, maxLaunches: null, at: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.annulled");
    }

    // WI-4 (kanban/in-progress/out-of-order-flight-entry.md): the contiguity
    // gate is gone — opening at 2 on an empty entry is this story's whole
    // point, and its failure codes are duplicateSequence / sequenceNotPositive.

    [Fact]
    public void OpenFlight_at_sequence_2_on_an_empty_entry_succeeds()
    {
        var entry = OpenEntry();

        var result = entry.OpenFlight(2, maxLaunches: null, at: DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sequence.Should().Be(2);
        entry = entry.Apply(result.Value);
        entry.Flights.Single().Sequence.Should().Be(2);
    }

    [Fact]
    public void OpenFlight_with_a_duplicate_sequence_fails_with_a_stable_code()
    {
        var entry = OpenEntry().Apply(new FlightOpened(1, DateTimeOffset.UtcNow));

        var result = entry.OpenFlight(1, maxLaunches: null, at: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openFlight.duplicateSequence");
    }

    [Fact]
    public void OpenFlight_with_a_non_positive_sequence_fails_with_a_stable_code()
    {
        var entry = OpenEntry();

        var result = entry.OpenFlight(0, maxLaunches: null, at: DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openFlight.sequenceNotPositive");
    }

    [Fact]
    public void OpenFlight_beyond_maxLaunches_fails_with_a_stable_code()
    {
        var entry = OpenEntry();
        var first = entry.OpenFlight(1, maxLaunches: 1, at: DateTimeOffset.UtcNow);
        first.IsSuccess.Should().BeTrue();
        entry = entry.Apply(first.Value);

        var second = entry.OpenFlight(2, maxLaunches: 1, at: DateTimeOffset.UtcNow);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("openFlight.maxLaunchesExceeded");
    }

    [Fact]
    public void OpenFlight_succeeds_and_carries_sequence_and_the_clock_instant_through()
    {
        var entry = OpenEntry();
        var at = new DateTimeOffset(2026, 1, 10, 9, 3, 5, TimeSpan.Zero);

        var result = entry.OpenFlight(1, maxLaunches: null, at: at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sequence.Should().Be(1);
        result.Value.At.Should().Be(at);
    }

    [Fact]
    public void OpenFlight_with_no_maxLaunches_accepts_an_unbounded_run()
    {
        var entry = OpenEntry();

        for (var sequence = 1; sequence <= 6; sequence++)
        {
            var result = entry.OpenFlight(sequence, maxLaunches: null, at: DateTimeOffset.UtcNow);
            result.IsSuccess.Should().BeTrue();
            entry = entry.Apply(result.Value);
        }

        entry.Flights.Length.Should().Be(6);
    }

    [Fact]
    public void OpenFlight_sequence_advances_1_2_3_across_successive_folds()
    {
        var entry = OpenEntry();

        var first = entry.OpenFlight(1, maxLaunches: null, at: DateTimeOffset.UtcNow);
        first.IsSuccess.Should().BeTrue();
        first.Value.Sequence.Should().Be(1);
        entry = entry.Apply(first.Value);

        var second = entry.OpenFlight(2, maxLaunches: null, at: DateTimeOffset.UtcNow);
        second.IsSuccess.Should().BeTrue();
        second.Value.Sequence.Should().Be(2);
        entry = entry.Apply(second.Value);

        var third = entry.OpenFlight(3, maxLaunches: null, at: DateTimeOffset.UtcNow);
        third.IsSuccess.Should().BeTrue();
        third.Value.Sequence.Should().Be(3);
        entry = entry.Apply(third.Value);

        entry.Flights.Select(f => f.Sequence).Should().Equal(1, 2, 3);
    }
}
