// authentication-and-authorisation.md WI-8 — the store-backed tests for the
// identity rows of the `people` read model, the (Provider, Subject) unique
// arbiter, the roles fold and the capture policy on the `competitions`
// summary. Written once against IStoreFixture and run unchanged against every
// backend Soarscore supports — Marten/PostgreSQL and Fisher/SQLite — one
// concrete subclass per backend at the foot of the file. Only the Postgres
// subclass keeps Trait("Category", "Storage"); EventStoreTests.cs's header
// says why.
//
// Two of these tests carry particular weight:
//
//  - the arbiter test (D5) is the LADR-0001 §8 lesson applied: the compound
//    unique index is declared through each store's own fluent API, and a
//    shared interface proving nothing, the only evidence that Fisher's
//    expression-array overload builds ONE compound index over both columns —
//    rather than two single-column indexes, which would not arbitrate the
//    pair — is this test passing on both stores.
//  - the same-stream duplicate test pins the fold tolerance WI-6 built:
//    a replayed IdentityLinked must upsert its row, never violate.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class AuthenticationEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly DateTimeOffset At = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    // F5K: the same richest-payload choice CompetitionEventStoreTests makes —
    // the capture-policy test needs a real competition stream to fold a
    // summary from.
    private static readonly ClassDefinition RichestDefinition = Corpus.All.Single(c => c.FileName == "40-f5k").Definition;

    private static PersonRegistered Registered(PersonId id, string email) =>
        new(id, $"Person {email}", new ContactDetails { Email = email }, null, At);

    private static IdentityLinked Linked(string provider, string subject) =>
        new(provider, subject, At);

    // ---- 1. Identity rows round-trip and FindIdentityAsync returns the join -

    [Fact]
    public async Task IdentityRow_round_trips_and_FindIdentityAsync_joins_it_to_the_person()
    {
        var id = PersonId.New();
        var append = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.NoStream,
            [Registered(id, "ada@identity.test"), Linked("google-oauth2", "sub-roundtrip-a")],
            TestContext.Current.CancellationToken);
        append.IsSuccess.Should().BeTrue();

        // A second link on the same person — several links per person is the
        // glossary's shape ("a Person may hold several identity links"), and
        // it is why the row document's key is person-scoped (see
        // PersonIdentityRowDocument).
        var secondLink = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.Exact(2),
            [Linked("microsoft", "sub-roundtrip-a2")],
            TestContext.Current.CancellationToken);
        secondLink.IsSuccess.Should().BeTrue();

        var match = await fixture.PeopleQuery.FindIdentityAsync("google-oauth2", "sub-roundtrip-a", TestContext.Current.CancellationToken);
        match.Should().NotBeNull();
        match!.PersonId.Should().Be(id);
        match.Roles.Should().BeEmpty("the person holds no roles yet");

        var second = await fixture.PeopleQuery.FindIdentityAsync("microsoft", "sub-roundtrip-a2", TestContext.Current.CancellationToken);
        second.Should().NotBeNull();
        second!.PersonId.Should().Be(id);

        // No row minted for the pair — a miss, not a join with nulls.
        var miss = await fixture.PeopleQuery.FindIdentityAsync("google-oauth2", "no-such-subject", TestContext.Current.CancellationToken);
        miss.Should().BeNull();
    }

    // ---- 2. The (Provider, Subject) arbiter — D5's race loser is rejected ---

    [Fact]
    public async Task Duplicate_identity_link_on_a_different_persons_stream_is_rejected_by_the_unique_index()
    {
        const string provider = "auth0";
        const string subject = "sub-arbiter";

        var winnerId = PersonId.New();
        var winnerAppend = await fixture.EventStore.AppendAsync(
            winnerId.Value, ExpectedVersion.NoStream,
            [Registered(winnerId, "winner@arbiter.test"), Linked(provider, subject)],
            TestContext.Current.CancellationToken);
        winnerAppend.IsSuccess.Should().BeTrue();

        var loserId = PersonId.New();
        var loserRegistered = await fixture.EventStore.AppendAsync(
            loserId.Value, ExpectedVersion.NoStream,
            [Registered(loserId, "loser@arbiter.test")],
            TestContext.Current.CancellationToken);
        loserRegistered.IsSuccess.Should().BeTrue();

        // A different person's stream claiming the same identity: the row the
        // inline projection mints is a NEW document (the key is person-scoped,
        // PersonIdentityRowDocument), so the (Provider, Subject) index — not
        // the document key — is what rejects it, inside the append
        // transaction. This is the exact failure LinkSignIn's bounded retry
        // resolves on (D5); EndpointRouteBuilderExtensions.cs maps the code to
        // 409.
        var loserLink = await fixture.EventStore.AppendAsync(
            loserId.Value, ExpectedVersion.Exact(1),
            [Linked(provider, subject)],
            TestContext.Current.CancellationToken);

        loserLink.IsFailure.Should().BeTrue();
        loserLink.Code.Should().Be("eventStore.uniqueConstraintViolation");

        // The whole transaction rolled back — the losing stream was never extended.
        var loserRead = await fixture.EventStore.ReadStreamAsync(loserId.Value, 0, TestContext.Current.CancellationToken);
        loserRead.Value.Should().ContainSingle().Which.Should().BeOfType<PersonRegistered>();

        // The winner keeps the link — the index decided, not the pre-reads.
        var match = await fixture.PeopleQuery.FindIdentityAsync(provider, subject, TestContext.Current.CancellationToken);
        match.Should().NotBeNull();
        match!.PersonId.Should().Be(winnerId);
    }

    // ---- 3. A duplicated IdentityLinked on the SAME stream folds tolerantly -

    [Fact]
    public async Task Duplicate_identity_link_on_the_same_stream_folds_tolerantly()
    {
        var id = PersonId.New();
        var append = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.NoStream,
            [Registered(id, "tolerant@replay.test"), Linked("github", "sub-tolerant")],
            TestContext.Current.CancellationToken);
        append.IsSuccess.Should().BeTrue();

        // The identical event appended again to the same stream — what a
        // replayed or retried append produces. The fold mints an identical
        // row (WI-6's fold tolerance), the document key derivation is
        // deterministic, so the second Store updates the same row and the
        // index has nothing to arbitrate.
        var duplicate = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.Exact(2),
            [Linked("github", "sub-tolerant")],
            TestContext.Current.CancellationToken);
        duplicate.IsSuccess.Should().BeTrue();

        var match = await fixture.PeopleQuery.FindIdentityAsync("github", "sub-tolerant", TestContext.Current.CancellationToken);
        match.Should().NotBeNull();
        match!.PersonId.Should().Be(id);
    }

    // ---- 4. Concurrent first-links: the expected version decides ------------

    [Fact]
    public async Task Two_sign_ins_linking_a_pre_registered_person_at_the_same_expected_version_allow_only_the_first()
    {
        // secure-automatic-identity-linking.md: two identities matched the
        // same pre-registered person while it held no identity links — both
        // read version 1 and both decide to link. The first append commits;
        // the second's Exact(1) misses the now-version-2 stream and fails
        // rather than silently attaching a second identity. The caller's
        // retry then resolves through LinkSignIn's guard (the person now has
        // identities → auth.signIn.explicitLinkRequired).
        var id = PersonId.New();
        var registered = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.NoStream,
            [Registered(id, "race@identity.test")],
            TestContext.Current.CancellationToken);
        registered.IsSuccess.Should().BeTrue();

        var first = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.Exact(1),
            [Linked("google-oauth2", "sub-race-a")],
            TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        var second = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.Exact(1),
            [Linked("microsoft", "sub-race-b")],
            TestContext.Current.CancellationToken);
        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("eventStore.concurrencyConflict");

        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, TestContext.Current.CancellationToken);
        read.Value.Should().HaveCount(2);
        read.Value.OfType<IdentityLinked>().Should().ContainSingle();
    }

    // ---- 5. Roles fold onto the summary and are countable and joinable -----

    [Fact]
    public async Task Roles_fold_onto_the_summary_are_counted_and_ride_the_identity_join()
    {
        // Relative counts, not absolutes: the fixture is one instance per test
        // class, so other tests' people are in the same read model. What this
        // test pins is that each grant/revoke moves the count by exactly one.
        var competitorsBefore = await fixture.PeopleQuery.CountByRoleAsync(PersonRole.Competitor, TestContext.Current.CancellationToken);
        var organisersBefore = await fixture.PeopleQuery.CountByRoleAsync(PersonRole.Organiser, TestContext.Current.CancellationToken);

        var id = PersonId.New();
        var append = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.NoStream,
            [Registered(id, "roles@fold.test"), Linked("auth0", "sub-roles"), new RoleGranted(PersonRole.Competitor, At)],
            TestContext.Current.CancellationToken);
        append.IsSuccess.Should().BeTrue();

        (await fixture.PeopleQuery.CountByRoleAsync(PersonRole.Competitor, TestContext.Current.CancellationToken))
            .Should().Be(competitorsBefore + 1);
        (await fixture.PeopleQuery.CountByRoleAsync(PersonRole.Organiser, TestContext.Current.CancellationToken))
            .Should().Be(organisersBefore);

        var grant = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.Exact(3),
            [new RoleGranted(PersonRole.Organiser, At.AddMinutes(1))],
            TestContext.Current.CancellationToken);
        grant.IsSuccess.Should().BeTrue();

        var revoke = await fixture.EventStore.AppendAsync(
            id.Value, ExpectedVersion.Exact(4),
            [new RoleRevoked(PersonRole.Competitor, At.AddMinutes(2))],
            TestContext.Current.CancellationToken);
        revoke.IsSuccess.Should().BeTrue();

        (await fixture.PeopleQuery.CountByRoleAsync(PersonRole.Competitor, TestContext.Current.CancellationToken))
            .Should().Be(competitorsBefore, "the revoke returned the count to where it started");
        (await fixture.PeopleQuery.CountByRoleAsync(PersonRole.Organiser, TestContext.Current.CancellationToken))
            .Should().Be(organisersBefore + 1);

        var summary = await fixture.PeopleQuery.FindByEmailAsync("roles@fold.test", TestContext.Current.CancellationToken);
        summary.Should().NotBeNull();
        summary!.Roles.Should().BeEquivalentTo([PersonRole.Organiser]);

        // D2 end to end: the join carries the person AND the roles a
        // per-request lookup resolves — the read WI-9's middleware makes.
        var match = await fixture.PeopleQuery.FindIdentityAsync("auth0", "sub-roles", TestContext.Current.CancellationToken);
        match.Should().NotBeNull();
        match!.PersonId.Should().Be(id);
        match.Roles.Should().BeEquivalentTo([PersonRole.Organiser]);
    }

    // ---- 6. The capture policy round-trips on the competitions summary ------

    [Fact]
    public async Task CapturePolicyConfigured_round_trips_on_the_competitions_summary_and_reconfiguration_wins()
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(RichestDefinition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition("Capture Policy Round Trip", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        // Before any CapturePolicyConfigured: null on the summary — D10's
        // consumers evaluate that as OrganisersOnly, the safe default.
        var before = await fixture.CompetitionsQuery.FindCapturePolicyAsync(created.Value, TestContext.Current.CancellationToken);
        before.Should().BeNull();

        var firstPerson = PersonId.New();
        var secondPerson = PersonId.New();
        var configure = await fixture.EventStore.AppendAsync(
            created.Value.Value, ExpectedVersion.Exact(1),
            [new CapturePolicyConfigured(
                new CapturePolicy(CapturePolicyMode.AllowList, [firstPerson, secondPerson]), At)],
            TestContext.Current.CancellationToken);
        configure.IsSuccess.Should().BeTrue();

        var allowList = await fixture.CompetitionsQuery.FindCapturePolicyAsync(created.Value, TestContext.Current.CancellationToken);
        allowList.Should().NotBeNull();
        allowList!.Mode.Should().Be(CapturePolicyMode.AllowList);
        // PersonIds round-trip as values, not just as a mode flag — the
        // allow-list is the D10 capture-policy policy's working data.
        allowList.Capturers.Should().BeEquivalentTo([firstPerson, secondPerson]);

        // Reconfiguration mid-contest is allowed and latest-wins on the fold
        // (D10) — it changes who may enter, never what has been entered (NFR-4).
        var reconfigure = await fixture.EventStore.AppendAsync(
            created.Value.Value, ExpectedVersion.Exact(2),
            [new CapturePolicyConfigured(new CapturePolicy(CapturePolicyMode.AnyRegisteredPerson, []), At.AddMinutes(1))],
            TestContext.Current.CancellationToken);
        reconfigure.IsSuccess.Should().BeTrue();

        var widened = await fixture.CompetitionsQuery.FindCapturePolicyAsync(created.Value, TestContext.Current.CancellationToken);
        widened.Should().NotBeNull();
        widened!.Mode.Should().Be(CapturePolicyMode.AnyRegisteredPerson);
        widened.Capturers.Should().BeEmpty();
    }

    // ---- 7. The entryRef filter — the capture-policy policy's one lookup ----

    [Fact]
    public async Task EntryQuery_entryRef_filter_is_the_complete_key_and_ignores_the_competition_filter()
    {
        var competitionA = CompetitionId.New();
        var competitionB = CompetitionId.New();

        EntryOpened Opened(EntryId entryId, CompetitionId competitionRef) =>
            new(entryId, competitionRef, 1, 1, 1, GroupId.New(), CompetitorId.New(), ReflightRole.Original, At);

        var entryA = EntryId.New();
        var entryB = EntryId.New();
        foreach (var (entryId, competitionRef) in new[] { (entryA, competitionA), (entryB, competitionB) })
        {
            var append = await fixture.EventStore.AppendAsync(
                entryId.Value, ExpectedVersion.NoStream, [Opened(entryId, competitionRef)],
                TestContext.Current.CancellationToken);
            append.IsSuccess.Should().BeTrue();
        }

        // The CapturePolicyPolicy's call shape (IEntryQuery.cs): the pipeline
        // does not yet know the entry's competition, so it passes default and
        // entryRef alone resolves the row.
        var found = await fixture.EntryQuery.FindAsync(
            default, null, null, null, null, null, TestContext.Current.CancellationToken, entryRef: entryA);
        found.Select(e => e.Id).Should().BeEquivalentTo([entryA]);
        found.Single().CompetitionRef.Should().Be(competitionA);

        // entryRef IS the complete key — a non-matching competitionRef is not
        // applied when it is supplied.
        var overriding = await fixture.EntryQuery.FindAsync(
            competitionB, null, null, null, null, null, TestContext.Current.CancellationToken, entryRef: entryA);
        overriding.Select(e => e.Id).Should().BeEquivalentTo([entryA]);

        // An unknown EntryId — an entry that was never opened — finds nothing.
        var missing = await fixture.EntryQuery.FindAsync(
            default, null, null, null, null, null, TestContext.Current.CancellationToken, entryRef: EntryId.New());
        missing.Should().BeEmpty();
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresAuthenticationEventStoreTests(PostgresFixture fixture) : AuthenticationEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteAuthenticationEventStoreTests(SqliteFixture fixture) : AuthenticationEventStoreTests<SqliteFixture>(fixture);
