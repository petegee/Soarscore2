// docs/plans/register-competitor-steel-thread-plan.md WI-7 — the store-backed
// tests for RegisterCompetitor/WithdrawCompetitor, against real PostgreSQL via
// Testcontainers rather than the FakeEventStore double the Application-layer
// handler tests use. Same style as CompetitionEventStoreTests.cs: calls the
// real handlers directly against fixture.EventStore, no dispatcher needed for
// a store-level test.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Application.Competitions;
using Soarscore.Application.People;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

[Trait("Category", "Storage")]
public sealed class CompetitorEventStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Any adoptable definition does here — unlike CompetitionEventStoreTests,
    // nothing in this class inspects AdoptedRules content, so richness doesn't
    // matter; a distinct FileName per test only avoids ClassDefinition content
    // hash collisions across the shared fixture.
    private static readonly ClassDefinition F3JDefinition = Corpus.All.Single(c => c.FileName == "50-f3j").Definition;
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;
    private static readonly ClassDefinition F5LDefinition = Corpus.All.Single(c => c.FileName == "60-f5l").Definition;
    private static readonly ClassDefinition F3FDefinition = Corpus.All.Single(c => c.FileName == "70-f3f").Definition;

    private static async Task<CompetitionId> CreateCompetitionAsync(PostgresFixture fixture, ClassDefinition definition, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(definition), TestContext.Current.CancellationToken);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            TestContext.Current.CancellationToken);
        created.IsSuccess.Should().BeTrue();

        return created.Value;
    }

    private static async Task<PersonId> RegisterPersonAsync(PostgresFixture fixture, string email)
    {
        var registerHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var registered = await registerHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null),
            TestContext.Current.CancellationToken);
        registered.IsSuccess.Should().BeTrue();

        return registered.Value;
    }

    [Fact]
    public async Task RegisterCompetitor_three_times_folds_to_a_field_of_three_numbered_in_order()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3JDefinition, "Field of Three");
        var personA = await RegisterPersonAsync(fixture, "pilot-a@example.com");
        var personB = await RegisterPersonAsync(fixture, "pilot-b@example.com");
        var personC = await RegisterPersonAsync(fixture, "pilot-c@example.com");

        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var first = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personA), TestContext.Current.CancellationToken);
        var second = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personB), TestContext.Current.CancellationToken);
        var third = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personC), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        third.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        fetched.Value.Competition.Competitors.Should().HaveCount(3);
        fetched.Value.Competition.Competitors.Select(c => c.Id).Should().Equal(first.Value, second.Value, third.Value);
        fetched.Value.Competition.Competitors.Select(c => c.CompetitorNumber).Should().Equal(1, 2, 3);
        fetched.Value.Competition.Competitors.Select(c => c.PersonRef).Should().Equal(personA, personB, personC);
    }

    [Fact]
    public async Task RegisterCompetitor_the_same_person_twice_is_rejected_against_real_postgres()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F5JDefinition, "Double Entry");
        var person = await RegisterPersonAsync(fixture, "pilot-double@example.com");

        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var first = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, person), TestContext.Current.CancellationToken);
        first.IsSuccess.Should().BeTrue();

        // The point of this test: invariant 1 (one registration per PersonId)
        // is a decide-function check over the folded stream, not a unique
        // index — this proves it survives a real append/reload round trip,
        // not just FakeEventStore holding events in memory.
        var second = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, person), TestContext.Current.CancellationToken);
        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("competition.competitor.alreadyRegistered");
    }

    [Fact]
    public async Task WithdrawCompetitor_persists_withdrawnAt_and_leaves_the_field_at_three()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F5LDefinition, "Withdrawal Leaves Field Intact");
        var personA = await RegisterPersonAsync(fixture, "pilot-withdraw-a@example.com");
        var personB = await RegisterPersonAsync(fixture, "pilot-withdraw-b@example.com");
        var personC = await RegisterPersonAsync(fixture, "pilot-withdraw-c@example.com");

        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var first = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personA), TestContext.Current.CancellationToken);
        await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personB), TestContext.Current.CancellationToken);
        await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personC), TestContext.Current.CancellationToken);

        var withdrawHandler = new WithdrawCompetitorHandler(fixture.EventStore, new SystemClock());
        var withdrawn = await withdrawHandler.HandleAsync(new WithdrawCompetitor(competitionId, first.Value), TestContext.Current.CancellationToken);
        withdrawn.IsSuccess.Should().BeTrue();

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        fetched.IsSuccess.Should().BeTrue();

        // Three, not two: withdrawal records rather than removes
        // (aggregate-roots.md:330-333) — the number stays retired.
        fetched.Value.Competition.Competitors.Should().HaveCount(3);
        fetched.Value.Competition.Competitors.Single(c => c.Id == first.Value).WithdrawnAt.Should().NotBeNull();
        fetched.Value.Competition.Competitors.Where(c => c.Id != first.Value).Should().OnlyContain(c => c.WithdrawnAt == null);
    }

    [Fact]
    public async Task Competitions_read_model_dropped_and_fully_replayed_with_competitor_events_present_lands_identical()
    {
        var competitionId = await CreateCompetitionAsync(fixture, F3FDefinition, "Replay With Competitor Events");
        var personA = await RegisterPersonAsync(fixture, "pilot-replay-a@example.com");
        var personB = await RegisterPersonAsync(fixture, "pilot-replay-b@example.com");

        var registerHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var first = await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personA), TestContext.Current.CancellationToken);
        await registerHandler.HandleAsync(new RegisterCompetitor(competitionId, personB), TestContext.Current.CancellationToken);

        var withdrawHandler = new WithdrawCompetitorHandler(fixture.EventStore, new SystemClock());
        await withdrawHandler.HandleAsync(new WithdrawCompetitor(competitionId, first.Value), TestContext.Current.CancellationToken);

        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var before = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        before.IsSuccess.Should().BeTrue();

        // Also capture the CompetitionSummary row: CompetitionProjection's
        // pass-through default arm (CompetitionProjection.cs:19-27) is what
        // this test really targets — CompetitorRegistered/CompetitorWithdrawn
        // must fold through the Inline projection without being recognised,
        // leaving the summary's own fields (which do not carry field data,
        // deliberately — see CompetitionSummary.cs) unchanged either side of
        // the drop/rebuild.
        var summaryBefore = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        var rowBefore = summaryBefore.Single(c => c.Id == competitionId);

        // Drop the read model's data only — the event log, and therefore
        // GetCompetition's fold, is untouched (LADR-0001 §4.10).
        await fixture.DocumentStore.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(CompetitionSummary), TestContext.Current.CancellationToken);

        var afterDrop = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        afterDrop.Should().NotContain(c => c.Id == competitionId);

        // Replay the whole log — now including CompetitorRegistered and
        // CompetitorWithdrawn — through the same Inline projection, on
        // demand, never the async daemon (LADR-0001 §2).
        using var daemon = await fixture.DocumentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync("CompetitionSummaryProjection", TestContext.Current.CancellationToken);

        var summaryAfter = await fixture.CompetitionsQuery.SearchAsync(null, null, TestContext.Current.CancellationToken);
        var rowAfter = summaryAfter.Single(c => c.Id == competitionId);
        rowAfter.Should().BeEquivalentTo(rowBefore);

        // The field itself — Competitors is not on CompetitionSummary at all —
        // survives replay because it lives in the event log, re-folded fresh
        // by GetCompetitionHandler; unaffected by the read-model drop, but
        // asserted here to show the two new event types round-trip correctly
        // once real instances of them exist in the log.
        var after = await getHandler.HandleAsync(new GetCompetition(competitionId), TestContext.Current.CancellationToken);
        after.IsSuccess.Should().BeTrue();
        after.Value.Should().BeEquivalentTo(before.Value);
    }
}
