// kanban/completed/capture-a-score-steel-thread-plan.md WI-6. Mirrors
// Competitions/GetCompetitionHandlerTests.cs's found/not-found style, since
// EntryLoader has no handler of its own yet (WI-8) to exercise it through —
// the loader is tested directly, the way PersonLoader/CompetitionLoader are
// exercised indirectly through GetPerson/GetCompetition today.

using AwesomeAssertions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Xunit;

namespace Soarscore.Application.Tests.Shared.Entries;

public class EntryLoaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeWindow SampleWorkingTime = new()
    {
        Start = Now,
        End = Now.AddMinutes(10),
    };

    [Fact]
    public async Task LoadAsync_for_an_existing_stream_returns_the_folded_entry_and_its_version()
    {
        var id = EntryId.New();
        var store = new FakeEventStore();
        var opened = new EntryOpened(
            id, SampleWorkingTime, CompetitionId.New(), 1, 1, 1, GroupId.New(), CompetitorId.New(), ReflightRole.Original, Now);
        var flightOpened = new FlightOpened(1, Now.AddMinutes(1), Now.AddMinutes(1));
        await store.AppendAsync(id.Value, ExpectedVersion.NoStream, [opened], TestContext.Current.CancellationToken);
        await store.AppendAsync(id.Value, ExpectedVersion.Exact(1), [flightOpened], TestContext.Current.CancellationToken);

        var result = await EntryLoader.LoadAsync(store, id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(2);
        result.Value.Entry.Id.Should().Be(id);
        result.Value.Entry.Flights.Should().ContainSingle().Which.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_for_an_unknown_id_fails_with_entry_notFound()
    {
        var store = new FakeEventStore();

        var result = await EntryLoader.LoadAsync(store, EntryId.New(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("entry.notFound");
    }
}
