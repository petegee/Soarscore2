// kanban/backlog/tape-points-landing-seeds.md WI-3. Covers
// DeclareInstrumentsHandler and CorrectInstrumentDeclarationHandler directly
// against a FakeEventStore — the BindParameterHandlerTests shape: seed a
// competition on a real seed definition (F3J, which declares landingDistance
// with its rulebook landing table), declare catalogue scales mapped through
// TapeMapping (never re-transcribed), and assert the events, the fold and
// that a refusal appends nothing.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class DeclareInstrumentsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3J = SeedF3J.Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3J,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3J.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    private static ReadingScale F3JSideScale() =>
        TapeCorpus.All.First(t => t.FileName == "tape-nz-f3j-side").Tape.ToReadingScale();

    private static ReadingScale AlesMetreScale() =>
        TapeCorpus.All.First(t => t.FileName == "tape-nz-ales-m-10m").Tape.ToReadingScale();

    private static DeclaredInstrument F3JSideBinding() =>
        new() { Instrument = "nz-f3j-side", Metric = "landingDistance", Scale = F3JSideScale() };

    [Fact]
    public async Task Declaring_a_composable_scale_appends_exactly_one_event_and_folds()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DeclareInstrumentsHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DeclareInstruments(competitionId, [F3JSideBinding()], "CD Jane"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(competitionId);

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(2); // 1 created + 1 declared
        var declared = stream[1].Should().BeOfType<InstrumentsDeclared>().Subject;
        declared.Declaration.Instruments.Should().ContainSingle();
        declared.Declaration.By.Should().Be("CD Jane");
        declared.Declaration.At.Should().Be(Now);

        var folded = Competition.Apply(null, (CompetitionEvent)stream[0])!.Apply(declared);
        folded.DeclaredInstruments.Should().NotBeNull();
        folded.DeclaredInstruments!.Instruments.Single().Instrument.Should().Be("nz-f3j-side");
    }

    [Fact]
    public async Task Declaring_an_empty_set_succeeds_and_is_the_distances_only_default()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DeclareInstrumentsHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DeclareInstruments(competitionId, [], "CD Jane"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var folded = store.Streams[competitionId.Value]
            .Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        folded.DeclaredInstruments.Should().NotBeNull();
        folded.DeclaredInstruments!.Instruments.Should().BeEmpty();
    }

    [Fact]
    public async Task Declaring_twice_is_refused_and_appends_nothing()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DeclareInstrumentsHandler(store, new FakeClock(Now));
        var ct = TestContext.Current.CancellationToken;

        var first = await handler.HandleAsync(
            new DeclareInstruments(competitionId, [F3JSideBinding()], "CD Jane"), ct);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.HandleAsync(
            new DeclareInstruments(competitionId, [], "CD Jane"), ct);

        second.IsFailure.Should().BeTrue();
        second.Code.Should().Be("declareInstruments.alreadyDeclared");
        store.Streams[competitionId.Value].Should().HaveCount(2, "a refused declaration appends no event");
    }

    [Fact]
    public async Task Declaring_a_straddled_pairing_is_refused_loudly_and_appends_nothing()
    {
        // ALES M's metre tape (bounds 1..10, no off-scale reading) against
        // F3J's table: boundary 0.2 sits inside band (0, 1], straddling 100
        // vs 99 — that side of the tape cannot score this class.
        var (store, competitionId) = SeedCompetition();
        var handler = new DeclareInstrumentsHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DeclareInstruments(
                competitionId,
                [new DeclaredInstrument { Instrument = "ales-metre", Metric = "landingDistance", Scale = AlesMetreScale() }],
                "CD Jane"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("tapeComposition.straddledBand");
        result.Message.Should().Contain("cannot score this class");
        store.Streams[competitionId.Value].Should().HaveCount(1, "a refused declaration appends no event");
    }

    [Fact]
    public async Task Declaring_with_a_blank_By_is_refused_at_the_handler()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new DeclareInstrumentsHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DeclareInstruments(competitionId, [F3JSideBinding()], " "),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.instruments.byRequired");
        store.Streams[competitionId.Value].Should().HaveCount(1);
    }

    [Fact]
    public async Task Declaring_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new DeclareInstrumentsHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new DeclareInstruments(CompetitionId.New(), [F3JSideBinding()], "CD Jane"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Correcting_without_a_declaration_is_refused()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new CorrectInstrumentDeclarationHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new CorrectInstrumentDeclaration(competitionId, [], "fix", "CD Jane"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("correctInstrumentDeclaration.notDeclared");
        store.Streams[competitionId.Value].Should().HaveCount(1);
    }

    [Fact]
    public async Task Correcting_without_a_reason_is_refused()
    {
        var (store, competitionId) = SeedCompetition();
        var ct = TestContext.Current.CancellationToken;
        var declared = await new DeclareInstrumentsHandler(store, new FakeClock(Now))
            .HandleAsync(new DeclareInstruments(competitionId, [F3JSideBinding()], "CD Jane"), ct);
        declared.IsSuccess.Should().BeTrue();

        var handler = new CorrectInstrumentDeclarationHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new CorrectInstrumentDeclaration(competitionId, [], " ", "CD Jane"), ct);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("correctInstrumentDeclaration.reasonRequired");
        store.Streams[competitionId.Value].Should().HaveCount(2);
    }

    [Fact]
    public async Task Correcting_replaces_the_set_whole_with_reason_author_and_time()
    {
        var (store, competitionId) = SeedCompetition();
        var ct = TestContext.Current.CancellationToken;
        var declared = await new DeclareInstrumentsHandler(store, new FakeClock(Now))
            .HandleAsync(new DeclareInstruments(competitionId, [F3JSideBinding()], "CD Jane"), ct);
        declared.IsSuccess.Should().BeTrue();

        var handler = new CorrectInstrumentDeclarationHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new CorrectInstrumentDeclaration(competitionId, [], "the tapes never arrived", "CD Jane"), ct);

        result.IsSuccess.Should().BeTrue();
        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(3); // created + declared + corrected
        var corrected = stream[2].Should().BeOfType<InstrumentDeclarationCorrected>().Subject;
        corrected.Correction.Reason.Should().Be("the tapes never arrived");
        corrected.Correction.By.Should().Be("CD Jane");
        corrected.Correction.At.Should().Be(Now);

        var folded = stream.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        folded.DeclaredInstruments.Should().NotBeNull();
        folded.DeclaredInstruments!.Instruments.Should().BeEmpty("the correction replaces the set whole");
    }
}
