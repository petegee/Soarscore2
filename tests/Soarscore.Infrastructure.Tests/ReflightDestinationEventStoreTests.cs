// kanban/in-progress/reflight-aggregate-destination.md WI-2 — the store-backed
// round-trip for the make-up datum: CountsForRoundOrdinal and Reason survive
// write→read through a real store on BOTH backends, mirroring
// ReflightGroupEventStoreTests.cs (real handlers, read back through a fresh
// fold — never anything a handler held in memory).
//
// Written once against IStoreFixture with one concrete subclass per backend at
// the foot of the file; only the Postgres subclass keeps
// Trait("Category", "Storage") (EventStoreTests.cs's header says why).
//
// No GetEntry query exists yet (EntrySummary.cs's doc comment) — the folded
// Entry state is read from the raw stream via fixture.EventStore.ReadStreamAsync
// and folded with the public Entry.Apply, the same shape
// EntryCaptureEventStoreTests.cs uses (that type's header, lines 15-20).
//
// F5J (30-f5j): literal MinPerGroup 6 — a 6-pilot field draws to exactly one
// group per round, and DrawPhase(rounds: 2) gives the two rounds the make-up
// shape needs (a counts-for round must be an earlier round of the same phase).

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class ReflightDestinationEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5JDefinition = Corpus.All.Single(c => c.FileName == "30-f5j").Definition;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(F5JDefinition), Ct);
        published.IsSuccess.Should().BeTrue();

        var createHandler = new CreateCompetitionHandler(fixture.EventStore, new SystemClock());
        var created = await createHandler.HandleAsync(
            new CreateCompetition(name, "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), published.Value),
            Ct);
        created.IsSuccess.Should().BeTrue($"{created.Code}: {created.Message}");

        return created.Value;
    }

    private static async Task<CompetitorId> RegisterCompetitorAsync(IStoreFixture fixture, CompetitionId competitionId, string email)
    {
        var registerPersonHandler = new RegisterPersonHandler(fixture.EventStore, new SystemClock());
        var person = await registerPersonHandler.HandleAsync(
            new RegisterPerson("Test Pilot", new ContactDetails { Email = email }, Club: null), Ct);
        person.IsSuccess.Should().BeTrue();

        var registerCompetitorHandler = new RegisterCompetitorHandler(fixture.EventStore, new SystemClock());
        var competitor = await registerCompetitorHandler.HandleAsync(
            new RegisterCompetitor(competitionId, person.Value), Ct);
        competitor.IsSuccess.Should().BeTrue();

        return competitor.Value;
    }

    /// <summary>Reads the raw Entry stream and folds it with the public <see cref="Entry.Apply(Entry?, EntryEvent)"/>.</summary>
    private static async Task<Entry> LoadEntryAsync(IStoreFixture fixture, EntryId id)
    {
        var read = await fixture.EventStore.ReadStreamAsync(id.Value, 0, Ct);
        read.IsSuccess.Should().BeTrue();
        return read.Value.Aggregate((Entry?)null, (current, e) => Entry.Apply(current, (EntryEvent)e))!;
    }

    /// <summary>Creates the competition, registers six pilots and draws two rounds — F5J's literal MinPerGroup 6 is one group per round.</summary>
    private static async Task<(CompetitionId CompetitionId, List<CompetitorId> Competitors, GroupId Round2GroupRef)> TwoRoundCompetitionAsync(
        IStoreFixture fixture, string name, string emailSlug)
    {
        var competitionId = await CreateCompetitionAsync(fixture, name);

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 6; i++)
        {
            competitors.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-{emailSlug}-{i}@example.com"));
        }

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 2), Ct);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        var accepted = await new AcceptDrawHandler(fixture.EventStore, new SystemClock())
            .HandleAsync(new AcceptDraw(competitionId), Ct);
        accepted.IsSuccess.Should().BeTrue($"{accepted.Code}: {accepted.Message}");

        // Phase.Ordinal is Phases.Length at draw time — 0 for the first (and,
        // in this thread, only) phase drawn; Round/TaskRound/Group are 1-based.
        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), Ct);
        fetched.IsSuccess.Should().BeTrue($"{fetched.Code}: {fetched.Message}");
        var round2 = fetched.Value.Competition.Phases.Single().Rounds.Single(r => r.Ordinal == 2);
        var round2Group = round2.TaskRounds.Single().Groups.Single(g => g.Ordinal == 1);

        return (competitionId, competitors, round2Group.Id);
    }

    // ---- 1. The make-up datum survives write→read on the real store -------

    [Fact]
    public async Task A_make_up_entry_round_trips_destination_and_reason_through_the_real_store()
    {
        var (competitionId, competitors, round2GroupRef) = await TwoRoundCompetitionAsync(fixture, "Make-up Round Trip", "makeup");
        var competitorRef = competitors[0];

        var openEntryHandler = new OpenEntryHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());

        // The competitor's Original in round 2 — the ordinary entry, opened
        // first (trap 2's open-order law).
        var original = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, 0, 2, 1, round2GroupRef, competitorRef), Ct);
        original.IsSuccess.Should().BeTrue($"{original.Code}: {original.Message}");

        // The make-up: counts for round 1, which the competitor holds no live
        // entry in — the D8 destination-conflict check finds nothing and the
        // decide accepts.
        var makeup = await openEntryHandler.HandleAsync(
            new OpenEntry(competitionId, 0, 2, 1, round2GroupRef, competitorRef,
                ReflightRole.Entitled, CountsForRoundOrdinal: 1, Reason: "Missed round 1 — car trouble"),
            Ct);
        makeup.IsSuccess.Should().BeTrue($"{makeup.Code}: {makeup.Message}");

        // Read back through the store, so it is the re-folded streams
        // asserting, not anything the handler held in memory.
        var originalEntry = await LoadEntryAsync(fixture, original.Value);
        originalEntry.Role.Should().Be(ReflightRole.Original);
        originalEntry.CountsForRoundOrdinal.Should().BeNull();

        var makeupEntry = await LoadEntryAsync(fixture, makeup.Value);
        makeupEntry.Role.Should().Be(ReflightRole.Entitled);
        makeupEntry.CountsForRoundOrdinal.Should().Be(1);
        makeupEntry.Reason.Should().Be("Missed round 1 — car trouble");
        makeupEntry.Annulment.Should().BeNull();
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresReflightDestinationEventStoreTests(PostgresFixture fixture)
    : ReflightDestinationEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteReflightDestinationEventStoreTests(SqliteFixture fixture)
    : ReflightDestinationEventStoreTests<SqliteFixture>(fixture);
