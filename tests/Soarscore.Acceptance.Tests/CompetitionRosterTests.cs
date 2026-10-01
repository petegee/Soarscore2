// ui_competition-roster-read.md acceptance: a Competitor-role caller who is
// not an organiser gets every competitor's display name for a competition in
// one read, with no contact details exposed. Driven over real HTTP against
// the mock-mode factory (AuthApi), so the AuthenticatedPolicy row, the DI
// registration and the route all prove themselves together — the story's
// single acceptance criterion, plus the anonymous-denied pin. Runs on
// whichever store SOARSCORE_TEST_STORE selects; names and emails are unique
// per fact (the suite's unique-name discipline against the shared fixture).
//
// The no-contact-details assertion reads the raw body: the view type
// structurally cannot carry them, so this pins the wire, not the type.

using AwesomeAssertions;
using Soarscore.Acceptance.Tests.Support;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Acceptance.Tests;

public sealed class CompetitionRosterTests
{
    private static string Pete => TestJwt.ForPerson(AuthActors.Organiser);

    private static string Tama => TestJwt.ForPerson(AuthActors.Competitor);

    private sealed record LinkSignInView(PersonId PersonId);

    [Fact]
    public async Task A_non_organiser_competitor_reads_every_display_name_in_one_read_with_no_contact_details()
    {
        var slug = Guid.NewGuid().ToString("N");
        var contentHash = await AuthApi.PostCommandAsync<string>(
            Pete, "/publish-class-definition",
            new PublishClassDefinition(Corpus.All.Single(c => c.FileName == "30-f5j").Definition));

        var competitionId = await AuthApi.PostCommandAsync<CompetitionId>(
            Pete, "/create-competition",
            new CreateCompetition($"Roster {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        var names = new[] { $"Roster Pilot A {slug}", $"Roster Pilot B {slug}", $"Roster Pilot C {slug}" };
        var competitorIds = new List<CompetitorId>();
        for (var i = 0; i < names.Length; i++)
        {
            var email = $"roster-{slug}-{i}@example.com".ToLowerInvariant();
            var personId = await AuthApi.PostCommandAsync<PersonId>(
                Pete, "/register-person",
                new RegisterPerson(names[i], new ContactDetails { Email = email }, null));
            competitorIds.Add(await AuthApi.PostCommandAsync<CompetitorId>(
                Pete, "/register-competitor", new RegisterCompetitor(competitionId, personId)));
        }

        // Tama is a Competitor-role persona, not an organiser — /people refuses
        // them (the D4 narrowing); the roster must not.
        var peopleResponse = await AuthApi.GetAsync(Tama, "/people");
        peopleResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        (await AuthApi.ProblemTitleAsync(peopleResponse)).Should().Be("auth.forbidden");

        var rosterResponse = await AuthApi.GetAsync(Tama, $"/competition-roster?competitionRef={competitionId.Value}");
        rosterResponse.EnsureSuccessStatusCode();

        var raw = await rosterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (var name in names)
        {
            raw.Should().Contain(name);
        }

        // No contact details or identity links anywhere on the wire — the
        // pilots' emails, and every field name only a person read may carry.
        raw.Should().NotContain($"roster-{slug}");
        raw.Should().NotContain("email");
        raw.Should().NotContain("phone");
        raw.Should().NotContain("homeCity");
        raw.Should().NotContain("clubName");
        raw.Should().NotContain("roles");
        raw.Should().NotContain("identities");

        var view = await AuthApi.ReadAsync<CompetitionRosterView>(rosterResponse);
        view.CompetitionRef.Should().Be(competitionId);
        view.Entries.Select(e => e.CompetitorNumber).Should().Equal(1, 2, 3);
        view.Entries.Select(e => e.Name).Should().Equal(names);
        view.Entries.Should().OnlyContain(e => !e.Withdrawn);
    }

    [Fact]
    public async Task Roster_marks_withdrawn_competitors_and_refuses_anonymous_callers()
    {
        var slug = Guid.NewGuid().ToString("N");
        var contentHash = await AuthApi.PostCommandAsync<string>(
            Pete, "/publish-class-definition",
            new PublishClassDefinition(Corpus.All.Single(c => c.FileName == "30-f5j").Definition));

        var competitionId = await AuthApi.PostCommandAsync<CompetitionId>(
            Pete, "/create-competition",
            new CreateCompetition($"Roster Wd {slug}", "Taupo", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), contentHash));

        var personId = await AuthApi.PostCommandAsync<PersonId>(
            Pete, "/register-person",
            new RegisterPerson($"Roster Pilot Wd {slug}", new ContactDetails { Email = $"roster-wd-{slug}@example.com".ToLowerInvariant() }, null));
        var competitorId = await AuthApi.PostCommandAsync<CompetitorId>(
            Pete, "/register-competitor", new RegisterCompetitor(competitionId, personId));
        (await AuthApi.PostAsync(Pete, "/withdraw-competitor", new WithdrawCompetitor(competitionId, competitorId)))
            .EnsureSuccessStatusCode();

        var rosterResponse = await AuthApi.GetAsync(Tama, $"/competition-roster?competitionRef={competitionId.Value}");
        rosterResponse.EnsureSuccessStatusCode();
        var view = await AuthApi.ReadAsync<CompetitionRosterView>(rosterResponse);
        view.Entries.Should().ContainSingle()
            .Which.Should().Be(new CompetitionRosterEntry(competitorId, 1, $"Roster Pilot Wd {slug}", Withdrawn: true));

        // AuthenticatedPolicy: no token, no roster.
        await AuthAcceptanceFixture.EnsureInitializedAsync();
        var anonymous = await AuthAcceptanceFixture.Client.GetAsync(
            $"/competition-roster?competitionRef={competitionId.Value}", TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        (await AuthApi.ProblemTitleAsync(anonymous)).Should().Be("auth.notAuthenticated");
    }
}
