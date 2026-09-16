// authentication-and-authorisation.md WI-7 (D10). Covers
// ConfigureCapturePolicyHandler directly against the People + Competitions
// fakes: the cross-aggregate read (every allow-listed capturer must be a
// registered person — Competition cannot read people itself) refuses with
// competition.capturePolicy.unknownPerson, and the decide's own shape
// refusals surface verbatim. The shared BindParameterHandlerTests seeding
// shape: a CompetitionCreated stream from SeedF3J's definition.

using AwesomeAssertions;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Queries.People;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;
using FakePeopleQuery = Soarscore.Application.Tests.Shared.People.FakePeopleQuery;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class ConfigureCapturePolicyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, CompetitionId CompetitionId, FakePeopleQuery People) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id, new FakePeopleQuery());
    }

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SeedF3J.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3J.Definition.Version,
            AdoptedAt = Now,
        };

    private static PersonSummary KnownPerson() =>
        new(PersonId.New(), "Tama Pilot", "tama@example.org", null, null, null, []);

    private static IReadOnlyList<IDomainEvent> Stream(FakeEventStore store, Guid streamId) =>
        store.ReadStreamAsync(streamId, 0).GetAwaiter().GetResult().Value;

    private static ConfigureCapturePolicyHandler Handler(FakeEventStore store, FakePeopleQuery people) =>
        new(store, people, new FakeClock(Now));

    // ---- Happy path ---------------------------------------------------------

    [Fact]
    public async Task An_AllowList_of_known_people_appends_CapturePolicyConfigured_and_the_policy_folds()
    {
        var (store, competitionId, people) = SeedCompetition();
        var tama = KnownPerson();
        var hana = KnownPerson();
        people.Seed(tama);
        people.Seed(hana);
        var handler = Handler(store, people);

        var result = await handler.HandleAsync(
            new ConfigureCapturePolicy(competitionId, new CapturePolicy(CapturePolicyMode.AllowList, [tama.Id, hana.Id])),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(competitionId);

        var stream = Stream(store, competitionId.Value);
        stream.Should().HaveCount(2);
        var configured = stream[1].Should().BeOfType<CapturePolicyConfigured>().Subject;
        configured.Policy.Mode.Should().Be(CapturePolicyMode.AllowList);
        configured.Policy.Capturers.Should().BeEquivalentTo(new[] { tama.Id, hana.Id });
    }

    // ---- The cross-aggregate read -------------------------------------------

    [Fact]
    public async Task An_AllowList_naming_an_unknown_person_fails_unknownPerson()
    {
        var (store, competitionId, people) = SeedCompetition();
        var tama = KnownPerson();
        people.Seed(tama);
        var handler = Handler(store, people);

        var result = await handler.HandleAsync(
            new ConfigureCapturePolicy(competitionId, new CapturePolicy(CapturePolicyMode.AllowList, [tama.Id, PersonId.New()])),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.capturePolicy.unknownPerson");
        Stream(store, competitionId.Value).Should().HaveCount(1);
    }

    // ---- The decide's shape refusals surface verbatim ------------------------

    [Fact]
    public async Task A_non_AllowList_mode_with_capturers_surfaces_the_decides_capturersIgnored()
    {
        var (store, competitionId, people) = SeedCompetition();
        people.Seed(KnownPerson());   // a capturer that exists — the refusal is the decide's, not the existence check's
        var handler = Handler(store, people);

        var result = await handler.HandleAsync(
            new ConfigureCapturePolicy(competitionId, new CapturePolicy(CapturePolicyMode.OrganisersOnly, [PersonId.New()])),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.capturePolicy.capturersIgnored");
        Stream(store, competitionId.Value).Should().HaveCount(1);
    }

    [Fact]
    public async Task An_empty_AllowList_surfaces_the_decides_emptyAllowList()
    {
        var (store, competitionId, people) = SeedCompetition();
        var handler = Handler(store, people);

        var result = await handler.HandleAsync(
            new ConfigureCapturePolicy(competitionId, new CapturePolicy(CapturePolicyMode.AllowList, [])),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.capturePolicy.emptyAllowList");
    }

    [Fact]
    public async Task Configuring_an_unknown_competition_fails_notFound()
    {
        var handler = Handler(new FakeEventStore(), new FakePeopleQuery());

        var result = await handler.HandleAsync(
            new ConfigureCapturePolicy(CompetitionId.New(), new CapturePolicy(CapturePolicyMode.OrganisersOnly, [])),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }
}