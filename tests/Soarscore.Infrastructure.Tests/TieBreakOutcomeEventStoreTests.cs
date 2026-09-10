// operational-tie-break-resolution.md WI-3 ("Store-backed") — the store-backed
// test for RecordTieBreakOutcome, mirroring ReflightRulingEventStoreTests.cs:
// real handlers, read-back through GetCompetitionHandler so the fold is fresh.
//
// This is the suite that fails at runtime if WI-3's registration line is
// missing: appending TieBreakOutcomeRecorded without its line in
// SoarscoreEventTypes.All fails at runtime on BOTH backends per LADR-0001
// §4.8, and nothing below can pass without a real append-and-re-fold through
// a real store.
//
// Written once against IStoreFixture, with one concrete subclass per backend
// at the foot of the file. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.
//
// F5L (60-f5l), not F3K: its phase-0 ladder is UndefinedRequiresRuling, so a
// CD ruling is accepted there with no fly-off machinery needed. Its
// MinPerGroup is the parameter "groupSize" with no default, which must be
// bound before DrawPhase or the draw fails drawPhase.parameterUnbound —
// bound here to 2, so a two-pilot field draws to exactly one round with one
// task-round and one group of two.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Infrastructure;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Infrastructure.Tests;

public abstract class TieBreakOutcomeEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition F5LDefinition = Corpus.All.Single(c => c.FileName == "60-f5l").Definition;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(F5LDefinition), Ct);
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

    private static async Task<Competition> LoadAsync(IStoreFixture fixture, CompetitionId competitionId)
    {
        var getHandler = new GetCompetitionHandler(fixture.EventStore);
        var fetched = await getHandler.HandleAsync(new GetCompetition(competitionId), Ct);
        fetched.IsSuccess.Should().BeTrue($"{fetched.Code}: {fetched.Message}");
        return fetched.Value.Competition;
    }

    /// <summary>Creates the competition, registers two pilots, binds F5L's groupSize parameter to 2 and draws one round — exactly one task-round, one group.</summary>
    private static async Task<(CompetitionId CompetitionId, List<CompetitorId> Competitors)> DrawnCompetitionAsync(
        IStoreFixture fixture, string name, string emailSlug)
    {
        var competitionId = await CreateCompetitionAsync(fixture, name);

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 2; i++)
        {
            competitors.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-{emailSlug}-{i}@example.com"));
        }

        // F5L's MinPerGroup is the parameter "groupSize" with no default —
        // unbound, DrawPhase fails drawPhase.parameterUnbound.
        var bindHandler = new BindParameterHandler(fixture.EventStore, fixture.EntryQuery, new SystemClock());
        var bound = await bindHandler.HandleAsync(
            new BindParameter(competitionId, "groupSize", MeasuredValue.Of(2m), "CD"),
            Ct);
        bound.IsSuccess.Should().BeTrue($"{bound.Code}: {bound.Message}");

        var drawHandler = new DrawPhaseHandler(fixture.EventStore, new SystemClock());
        var drawn = await drawHandler.HandleAsync(new DrawPhase(competitionId, 1), Ct);
        drawn.IsSuccess.Should().BeTrue($"{drawn.Code}: {drawn.Message}");

        return (competitionId, competitors);
    }

    // ---- 1. A recorded outcome appends and re-folds fresh -------------------

    [Fact]
    public async Task RecordTieBreakOutcome_round_trips_through_the_real_store_and_folds_the_outcome_on()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Tie-Break Round Trip", "tiebreak");

        var instantBefore = new SystemClock().UtcNow;

        var recordHandler = new RecordTieBreakOutcomeHandler(fixture.EventStore, new SystemClock());
        var appended = await recordHandler.HandleAsync(
            new RecordTieBreakOutcome(
                competitionId, 0, new UndefinedRequiresRuling(),
                [new TieBreakOutcomePlacing(competitors[0], 1), new TieBreakOutcomePlacing(competitors[1], 2)],
                "CD ruling: first pilot's landing was measurably closer",
                "the contest director"), Ct);
        appended.IsSuccess.Should().BeTrue($"{appended.Code}: {appended.Message}");

        // Read back through the store, so it is the re-folded stream asserting,
        // not anything the handler held in memory.
        var competition = await LoadAsync(fixture, competitionId);

        competition.TieBreakOutcomes.Should().HaveCount(1);
        var outcome = competition.TieBreakOutcomes.Single();
        outcome.PhaseOrdinal.Should().Be(0);
        outcome.Directive.Should().BeOfType<UndefinedRequiresRuling>();
        outcome.Placings.Select(p => (p.CompetitorRef, p.PlaceInGroup)).Should().Equal(
            (competitors[0], 1), (competitors[1], 2));
        outcome.Reason.Should().Be("CD ruling: first pilot's landing was measurably closer");
        outcome.By.Should().Be("the contest director");
        outcome.At.Should().BeOnOrAfter(instantBefore);
    }

    // ---- 2. Superseding outcomes accumulate, in log order -------------------

    [Fact]
    public async Task A_second_outcome_for_the_same_group_accumulates_in_log_order()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Tie-Break Superseded", "supersede");

        var recordHandler = new RecordTieBreakOutcomeHandler(fixture.EventStore, new SystemClock());
        var first = await recordHandler.HandleAsync(
            new RecordTieBreakOutcome(
                competitionId, 0, new UndefinedRequiresRuling(),
                [new TieBreakOutcomePlacing(competitors[0], 1), new TieBreakOutcomePlacing(competitors[1], 2)],
                "CD ruling: first pilot's landing was measurably closer",
                "the contest director"), Ct);
        first.IsSuccess.Should().BeTrue($"{first.Code}: {first.Message}");

        // No uniqueness check by design (D4): re-recording for the same group
        // supersedes, and the log keeps every decision — last logged wins at
        // resolution time.
        var second = await recordHandler.HandleAsync(
            new RecordTieBreakOutcome(
                competitionId, 0, new UndefinedRequiresRuling(),
                [new TieBreakOutcomePlacing(competitors[1], 1), new TieBreakOutcomePlacing(competitors[0], 2)],
                "CD revisits the ruling on review",
                "the contest director"), Ct);
        second.IsSuccess.Should().BeTrue($"{second.Code}: {second.Message}");

        var competition = await LoadAsync(fixture, competitionId);
        competition.TieBreakOutcomes.Should().HaveCount(2);
        competition.TieBreakOutcomes[0].Placings[0].CompetitorRef.Should().Be(competitors[0]);
        competition.TieBreakOutcomes[1].Placings[0].CompetitorRef.Should().Be(competitors[1]);
    }

    // ---- 3. The failure codes survive the JSON round trip -------------------

    [Fact]
    public async Task A_blank_reason_is_refused_through_the_real_store()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Reason Required", "reason");

        var recordHandler = new RecordTieBreakOutcomeHandler(fixture.EventStore, new SystemClock());
        var appended = await recordHandler.HandleAsync(
            new RecordTieBreakOutcome(
                competitionId, 0, new UndefinedRequiresRuling(),
                [new TieBreakOutcomePlacing(competitors[0], 1), new TieBreakOutcomePlacing(competitors[1], 2)],
                "   ", "the contest director"), Ct);

        appended.IsFailure.Should().BeTrue();
        appended.Code.Should().Be("recordTieBreakOutcome.reasonRequired");
    }

    [Fact]
    public async Task An_outcome_for_an_undrawn_phase_is_refused_through_the_real_store()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Phase Not Found", "phase");

        var recordHandler = new RecordTieBreakOutcomeHandler(fixture.EventStore, new SystemClock());
        var appended = await recordHandler.HandleAsync(
            new RecordTieBreakOutcome(
                competitionId, 99, new UndefinedRequiresRuling(),
                [new TieBreakOutcomePlacing(competitors[0], 1), new TieBreakOutcomePlacing(competitors[1], 2)],
                "CD ruling: first pilot's landing was measurably closer",
                "the contest director"), Ct);

        appended.IsFailure.Should().BeTrue();
        appended.Code.Should().Be("recordTieBreakOutcome.phaseNotFound");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresTieBreakOutcomeEventStoreTests(PostgresFixture fixture)
    : TieBreakOutcomeEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteTieBreakOutcomeEventStoreTests(SqliteFixture fixture)
    : TieBreakOutcomeEventStoreTests<SqliteFixture>(fixture);
