// WI-9 (docs/plans/command-side-steel-thread-plan.md) — the four store-backed
// tests that carry real weight, run against a real PostgreSQL via
// Testcontainers rather than the FakeEventStore/FakePeopleQuery doubles WI-6's
// handler tests use. One shared container (PostgresFixture) for the whole
// class; each test uses its own email/stream id so they stay independent.
//
// Trait("Category", "Storage") lets these be filtered out of a fast local
// loop (`dotnet test --filter Category!=Storage`) — they need Docker and take
// longer than the in-memory suites.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

[Trait("Category", "Storage")]
public sealed class MartenEventStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset At = new(2026, 8, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Append_then_read_round_trips_order_and_payload()
    {
        var id = PersonId.New();
        var registered = new PersonRegistered(
            id, "Ada Lovelace", new ContactDetails { Email = "ada@round-trip.test", Phone = "021", HomeCity = "Wellington" },
            new ClubAffiliation { ClubName = "Wellington MAC" }, At);
        var renamed = new PersonRenamed("Ada King", At.AddMinutes(1));

        var append1 = await fixture.EventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, [registered], TestContext.Current.CancellationToken);
        append1.IsSuccess.Should().BeTrue();

        var append2 = await fixture.EventStore.AppendAsync(id.Value, ExpectedVersion.Exact(1), [renamed], TestContext.Current.CancellationToken);
        append2.IsSuccess.Should().BeTrue();

        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, TestContext.Current.CancellationToken);

        read.IsSuccess.Should().BeTrue();
        read.Value.Should().Equal(registered, renamed);
    }

    [Fact]
    public async Task Stale_expected_version_is_rejected_and_the_earlier_append_survives()
    {
        var id = PersonId.New();
        var registered = new PersonRegistered(
            id, "Grace Hopper", new ContactDetails { Email = "grace@stale-version.test" }, null, At);
        var renamed = new PersonRenamed("Grace Murray Hopper", At.AddMinutes(1));

        await fixture.EventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, [registered], TestContext.Current.CancellationToken);
        var rename = await fixture.EventStore.AppendAsync(id.Value, ExpectedVersion.Exact(1), [renamed], TestContext.Current.CancellationToken);
        rename.IsSuccess.Should().BeTrue();

        // Stale: acts as though the rename above was never read.
        var staleChange = new ContactDetailsChanged(new ContactDetails { Email = "grace@stale-version.test" }, At.AddMinutes(2));
        var stale = await fixture.EventStore.AppendAsync(id.Value, ExpectedVersion.Exact(1), [staleChange], TestContext.Current.CancellationToken);

        stale.IsFailure.Should().BeTrue();
        stale.Code.Should().Be("eventStore.concurrencyConflict");

        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, TestContext.Current.CancellationToken);
        read.Value.Should().Equal(registered, renamed);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_in_the_append_transaction_and_the_first_registration_survives()
    {
        const string email = "shared@duplicate-email.test";
        var firstId = PersonId.New();
        var secondId = PersonId.New();
        var first = new PersonRegistered(firstId, "First Person", new ContactDetails { Email = email }, null, At);
        var second = new PersonRegistered(secondId, "Second Person", new ContactDetails { Email = email }, null, At);

        var firstAppend = await fixture.EventStore.AppendAsync(firstId.Value, ExpectedVersion.NoStream, [first], TestContext.Current.CancellationToken);
        firstAppend.IsSuccess.Should().BeTrue();

        var secondAppend = await fixture.EventStore.AppendAsync(secondId.Value, ExpectedVersion.NoStream, [second], TestContext.Current.CancellationToken);

        secondAppend.IsFailure.Should().BeTrue();
        secondAppend.Code.Should().Be("eventStore.uniqueConstraintViolation");

        // The whole transaction rolled back — the second stream was never appended.
        var secondRead = await fixture.EventStore.ReadStreamAsync(secondId.Value, 0, TestContext.Current.CancellationToken);
        secondRead.Value.Should().BeEmpty();

        var summary = await fixture.PeopleQuery.FindByEmailAsync(email, TestContext.Current.CancellationToken);
        summary.Should().NotBeNull();
        summary!.Id.Should().Be(firstId);
        summary.Name.Should().Be("First Person");
    }

    [Fact]
    public async Task Read_model_dropped_and_fully_replayed_lands_identical()
    {
        var id = PersonId.New();
        var registered = new PersonRegistered(
            id, "Katherine Johnson", new ContactDetails { Email = "katherine@replay.test", HomeCity = "Hampton" },
            new ClubAffiliation { ClubName = "Hampton Roads" }, At);
        await fixture.EventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, [registered], TestContext.Current.CancellationToken);

        var before = await fixture.PeopleQuery.FindByEmailAsync("katherine@replay.test", TestContext.Current.CancellationToken);
        before.Should().NotBeNull();

        // Drop the read model's data only — the event log is untouched (§4.10:
        // read models are dropped and replayed, never migrated).
        await fixture.DocumentStore.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(Application.People.PersonSummary), TestContext.Current.CancellationToken);

        var afterDrop = await fixture.PeopleQuery.FindByEmailAsync("katherine@replay.test", TestContext.Current.CancellationToken);
        afterDrop.Should().BeNull();

        // Replay the whole log through the same Inline projection, on demand —
        // never the continuously-running async daemon (LADR-0001 §2).
        using var daemon = await fixture.DocumentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync("PersonSummaryProjection", TestContext.Current.CancellationToken);

        var afterRebuild = await fixture.PeopleQuery.FindByEmailAsync("katherine@replay.test", TestContext.Current.CancellationToken);
        afterRebuild.Should().Be(before);
    }
}
