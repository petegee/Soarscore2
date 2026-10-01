// ui_competition-roster-read.md. Covers GetCompetitionRosterHandler directly
// against a FakeEventStore plus the People FakePeopleQuery, driven through
// real competition events: the roster lists every competitor ordered by
// number with display names and withdrawn flags, degrades a person missing
// from the read model to "competitor #n" (the event-log precedent), and
// fails competition.notFound for an unknown competition. The no-contact-
// details half of the acceptance lives at the HTTP level
// (CompetitionRosterAcceptanceTests asserts the raw body), because the
// handler's view type structurally cannot carry them.

using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.People;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using CompetitionDoubles = Soarscore.Application.Tests.Shared.Competitions;
using PeopleDoubles = Soarscore.Application.Tests.Shared.People;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class GetCompetitionRosterHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private sealed record Wired(
        CompetitionDoubles.FakeEventStore Store,
        CompetitionId CompetitionId,
        PeopleDoubles.FakePeopleQuery People,
        GetCompetitionRosterHandler Roster);

    private static Wired SeedWired()
    {
        var store = new CompetitionDoubles.FakeEventStore();
        var competitionId = CompetitionId.New();
        var definition = Corpus.All[0].Definition;
        var created = new CompetitionCreated(
            competitionId, "Roster Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", new AdoptedRules
            {
                Definition = definition,
                SourceClassId = "content-hash-abc123",
                SourceVersion = definition.Version,
                AdoptedAt = Now,
            }, Now);
        store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        var people = new PeopleDoubles.FakePeopleQuery();
        return new Wired(store, competitionId, people, new GetCompetitionRosterHandler(store, people));
    }

    private static Competitor SeedRegisteredCompetitor(Wired wired, PersonId personRef, int number)
    {
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = personRef,
            CompetitorNumber = number,
            RegisteredAt = Now,
        };
        wired.Store.AppendAsync(
            wired.CompetitionId.Value, ExpectedVersion.Exact(wired.Store.Streams[wired.CompetitionId.Value].Count),
            [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();
        return competitor;
    }

    private static void SeedWithdrawn(Wired wired, CompetitorId competitorRef)
    {
        wired.Store.AppendAsync(
            wired.CompetitionId.Value, ExpectedVersion.Exact(wired.Store.Streams[wired.CompetitionId.Value].Count),
            [new CompetitorWithdrawn(competitorRef, Now)]).GetAwaiter().GetResult();
    }

    private static void SeedPerson(Wired wired, PersonId id, string name) =>
        wired.People.Seed(new PersonSummary(
            id, name, $"{name.ToLowerInvariant().Replace(' ', '.')}@example.com", null, null, "Auckland Club", []));

    private static async Task<CompetitionRosterView> RosterAsync(Wired wired)
    {
        var result = await wired.Roster.HandleAsync(
            new GetCompetitionRoster(wired.CompetitionId), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Roster_lists_every_competitor_ordered_by_number_with_names_and_withdrawn_flags()
    {
        var wired = SeedWired();
        var alice = PersonId.New();
        var bob = PersonId.New();
        var cara = PersonId.New();
        SeedPerson(wired, alice, "Alice A");
        SeedPerson(wired, bob, "Bob B");
        SeedPerson(wired, cara, "Cara C");

        // Register out of number order — the view still orders by number.
        var third = SeedRegisteredCompetitor(wired, cara, 3);
        var first = SeedRegisteredCompetitor(wired, alice, 1);
        var second = SeedRegisteredCompetitor(wired, bob, 2);
        SeedWithdrawn(wired, second.Id);

        var view = await RosterAsync(wired);

        view.CompetitionRef.Should().Be(wired.CompetitionId);
        view.Entries.Select(e => e.CompetitorNumber).Should().Equal(1, 2, 3);
        view.Entries[0].Should().Be(new CompetitionRosterEntry(first.Id, 1, "Alice A", Withdrawn: false));
        view.Entries[1].Should().Be(new CompetitionRosterEntry(second.Id, 2, "Bob B", Withdrawn: true));
        view.Entries[2].Should().Be(new CompetitionRosterEntry(third.Id, 3, "Cara C", Withdrawn: false));
    }

    [Fact]
    public async Task Roster_degrades_a_person_missing_from_the_read_model_to_competitor_number()
    {
        var wired = SeedWired();
        var known = PersonId.New();
        SeedPerson(wired, known, "Alice A");

        var first = SeedRegisteredCompetitor(wired, known, 1);
        var ghost = SeedRegisteredCompetitor(wired, PersonId.New(), 2);

        var view = await RosterAsync(wired);

        view.Entries.Should().HaveCount(2);
        view.Entries[0].Name.Should().Be("Alice A");
        view.Entries[1].Should().Be(new CompetitionRosterEntry(ghost.Id, 2, "competitor #2", Withdrawn: false));
    }

    [Fact]
    public async Task Roster_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var handler = new GetCompetitionRosterHandler(
            new CompetitionDoubles.FakeEventStore(), new PeopleDoubles.FakePeopleQuery());

        var result = await handler.HandleAsync(
            new GetCompetitionRoster(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }
}
