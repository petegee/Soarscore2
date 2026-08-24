// kanban/in-progress/reflight-scoring-rulings.md WI-6 ("Store-backed") — the
// store-backed test for RecordReflightRuling, mirroring
// ReflightGroupEventStoreTests.cs exactly: real handlers, read-back through
// GetCompetitionHandler so the fold is fresh (that file's header, lines 5-17,
// states why).
//
// This is the suite that fails at runtime if WI-5's registration line is
// missing: appending ReflightRulingRecorded without its line in
// SoarscoreEventTypes.All fails at runtime on BOTH backends per LADR-0001
// §4.8, and nothing below can pass without a real append-and-re-fold through
// a real store.
//
// Written once against IStoreFixture, with one concrete subclass per backend
// at the foot of the file, same as ReflightGroupEventStoreTests.cs /
// BindParameterEventStoreTests.cs. Only the Postgres subclass keeps
// Trait("Category", "Storage"); EventStoreTests.cs's header says why.
//
// NZ Class M ALES 200 (80-nz-m-ales200), not F5J: its class-level Reflight
// rule has BOTH slots = UndefinedRequiresRuling, so a CD ruling is accepted
// there (F5J's rulebook speaks and would refuse classRuleSpeaks). Its
// MinPerGroup is the parameter "groupSize" with no default, which must be
// bound before DrawPhase or the draw fails drawPhase.parameterUnbound —
// bound here to 2, so a two-pilot field draws to exactly one round with one
// task-round and one group of two.

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

public abstract class ReflightRulingEventStoreTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : class, IStoreFixture
{
    private static readonly ClassDefinition NzMAles200Definition = Corpus.All.Single(c => c.FileName == "80-nz-m-ales200").Definition;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- setup

    private static async Task<CompetitionId> CreateCompetitionAsync(IStoreFixture fixture, string name)
    {
        var publishHandler = new PublishClassDefinitionHandler(fixture.EventStore, new SystemClock());
        var published = await publishHandler.HandleAsync(new PublishClassDefinition(NzMAles200Definition), Ct);
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

    /// <summary>Creates the competition, registers two pilots, binds NZ-M's groupSize parameter to 2 and draws one round — exactly one task-round, one group.</summary>
    private static async Task<(CompetitionId CompetitionId, List<CompetitorId> Competitors)> DrawnCompetitionAsync(
        IStoreFixture fixture, string name, string emailSlug)
    {
        var competitionId = await CreateCompetitionAsync(fixture, name);

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 2; i++)
        {
            competitors.Add(await RegisterCompetitorAsync(fixture, competitionId, $"pilot-{emailSlug}-{i}@example.com"));
        }

        // NZ Class M ALES 200's MinPerGroup is the parameter "groupSize" with
        // no default — unbound, DrawPhase fails drawPhase.parameterUnbound.
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

    // ---- 1. A recorded ruling appends and re-folds fresh ------------------

    [Fact]
    public async Task RecordReflightRuling_round_trips_through_the_real_store_and_folds_the_ruling_on()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Ruling Round Trip", "ruling");

        var instantBefore = new SystemClock().UtcNow;

        var recordHandler = new RecordReflightRulingHandler(fixture.EventStore, new SystemClock());
        var appended = await recordHandler.HandleAsync(
            new RecordReflightRuling(
                competitionId, 0, 1, 1, competitors[0],
                ReflightSelection.Replacement,
                "Timing failure unresolved by the rulebook",
                "the contest director"), Ct);
        appended.IsSuccess.Should().BeTrue($"{appended.Code}: {appended.Message}");

        // Read back through the store, so it is the re-folded stream asserting,
        // not anything the handler held in memory.
        var competition = await LoadAsync(fixture, competitionId);

        competition.Rulings.Should().HaveCount(1);
        var ruling = competition.Rulings.Single();
        ruling.TaskRound.PhaseOrdinal.Should().Be(0);
        ruling.TaskRound.RoundOrdinal.Should().Be(1);
        ruling.TaskRound.TaskRoundOrdinal.Should().Be(1);
        ruling.CompetitorRef.Should().Be(competitors[0]);
        ruling.Selection.Should().Be(ReflightSelection.Replacement);
        ruling.Reason.Should().Be("Timing failure unresolved by the rulebook");
        ruling.By.Should().Be("the contest director");
        ruling.At.Should().BeOnOrAfter(instantBefore);
    }

    // ---- 2. Superseding rulings accumulate, in log order ------------------

    [Fact]
    public async Task A_second_ruling_for_the_same_key_accumulates_in_log_order()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Ruling Superseded", "supersede");

        var recordHandler = new RecordReflightRulingHandler(fixture.EventStore, new SystemClock());
        var first = await recordHandler.HandleAsync(
            new RecordReflightRuling(
                competitionId, 0, 1, 1, competitors[0],
                ReflightSelection.Replacement,
                "Timing failure unresolved by the rulebook",
                "the contest director"), Ct);
        first.IsSuccess.Should().BeTrue($"{first.Code}: {first.Message}");

        // No uniqueness check by design (decision 2): re-recording for the
        // same (task-round, competitor) supersedes, and the log keeps every
        // decision — last logged wins at resolution time.
        var second = await recordHandler.HandleAsync(
            new RecordReflightRuling(
                competitionId, 0, 1, 1, competitors[0],
                ReflightSelection.BetterOf,
                "CD revisits the first ruling on review",
                "the contest director"), Ct);
        second.IsSuccess.Should().BeTrue($"{second.Code}: {second.Message}");

        var competition = await LoadAsync(fixture, competitionId);
        competition.Rulings.Should().HaveCount(2);
        competition.Rulings[0].Selection.Should().Be(ReflightSelection.Replacement);
        competition.Rulings[1].Selection.Should().Be(ReflightSelection.BetterOf);
    }

    // ---- 3. The failure codes survive the JSON round trip -----------------

    [Fact]
    public async Task A_blank_reason_is_refused_through_the_real_store()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Reason Required", "reason");

        var recordHandler = new RecordReflightRulingHandler(fixture.EventStore, new SystemClock());
        var appended = await recordHandler.HandleAsync(
            new RecordReflightRuling(
                competitionId, 0, 1, 1, competitors[0],
                ReflightSelection.Replacement, "   ", "the contest director"), Ct);

        appended.IsFailure.Should().BeTrue();
        appended.Code.Should().Be("recordReflightRuling.reasonRequired");
    }

    [Fact]
    public async Task A_ruling_at_an_undrawn_coordinate_is_refused_through_the_real_store()
    {
        var (competitionId, competitors) = await DrawnCompetitionAsync(fixture, "Coordinate Not Found", "coord");

        var recordHandler = new RecordReflightRulingHandler(fixture.EventStore, new SystemClock());
        var appended = await recordHandler.HandleAsync(
            new RecordReflightRuling(
                competitionId, 99, 1, 1, competitors[0],
                ReflightSelection.Replacement,
                "Timing failure unresolved by the rulebook",
                "the contest director"), Ct);

        appended.IsFailure.Should().BeTrue();
        appended.Code.Should().Be("recordReflightRuling.taskRoundNotFound");
    }
}

[Trait("Category", "Storage")]
public sealed class PostgresReflightRulingEventStoreTests(PostgresFixture fixture)
    : ReflightRulingEventStoreTests<PostgresFixture>(fixture);

public sealed class SqliteReflightRulingEventStoreTests(SqliteFixture fixture)
    : ReflightRulingEventStoreTests<SqliteFixture>(fixture);
