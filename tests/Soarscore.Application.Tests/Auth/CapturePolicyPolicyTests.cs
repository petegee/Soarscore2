// authentication-and-authorisation.md WI-5 — CapturePolicyPolicy's truth
// table: D10's six steps in order (first match wins), the two deliberate
// non-denials (entry-not-found allow-through; no-configured-policy ⇒
// OrganisersOnly) and the fail-closed branches. Real C-mapped commands
// throughout: OpenEntry for the competition-scoped shape, CaptureMeasurement
// for the entry-scoped one whose competition the policy resolves through the
// entry index.

using AwesomeAssertions;
using Soarscore.Application.Auth;
using Soarscore.Application.Auth.Policies;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Tests.Shared.Competitions;
using Soarscore.Application.Tests.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

// The two Shared folders each carry their own internal FakeServiceProvider —
// same shape, duplicate by design (their headers record why) — so this file
// names one explicitly.
using FakeServiceProvider = Soarscore.Application.Tests.Shared.Competitions.FakeServiceProvider;

namespace Soarscore.Application.Tests.Auth;

public class CapturePolicyPolicyTests
{
    private static readonly CompetitionId Competition = CompetitionId.New();
    private static readonly PersonId CompetitorPerson = PersonId.New();
    private static readonly PersonId OtherPerson = PersonId.New();
    private static readonly EntryId KnownEntry = EntryId.New();
    private static readonly EntryId UnknownEntry = EntryId.New();

    private static readonly CapturePolicyPolicy Policy = new();

    private static readonly ICurrentUser Anonymous = new FakeCurrentUser();
    private static readonly ICurrentUser Unlinked = new FakeCurrentUser(IsAuthenticated: true, Provider: "auth0", Subject: "sub-1");
    private static readonly ICurrentUser Competitor = new FakeCurrentUser(IsAuthenticated: true, PersonId: CompetitorPerson, HeldRoles: [PersonRole.Competitor]);
    private static readonly ICurrentUser Organiser = new FakeCurrentUser(IsAuthenticated: true, PersonId: OtherPerson, HeldRoles: [PersonRole.Organiser]);

    private static OpenEntry OpenEntryCommand() => new(
        Competition, PhaseOrdinal: 0, RoundOrdinal: 1, TaskRoundOrdinal: 1,
        GroupRef: GroupId.New(), CompetitorRef: CompetitorId.New());

    private static CaptureMeasurement CaptureCommand(EntryId entryRef) => new(
        entryRef, FlightSequence: 1, Metric: "flightTime", MeasuredValue.Of(120.5m));

    private static FakeServiceProvider ServicesWith(ICompetitionsQuery? competitions = null, IEntryQuery? entries = null)
    {
        var services = new Dictionary<Type, object>();
        if (competitions is not null)
        {
            services[typeof(ICompetitionsQuery)] = competitions;
        }

        if (entries is not null)
        {
            services[typeof(IEntryQuery)] = entries;
        }

        return new FakeServiceProvider(services);
    }

    private static FakeEntryQuery IndexWith(EntryId entryRef) => SeedIndex(new EntrySummary(
        entryRef, Competition, PhaseOrdinal: 0, RoundOrdinal: 1, TaskRoundOrdinal: 1,
        GroupId.New(), CompetitorId.New(), ReflightRole.Original));

    private static FakeEntryQuery SeedIndex(params EntrySummary[] summaries)
    {
        var index = new FakeEntryQuery();
        foreach (var summary in summaries)
        {
            index.Seed(summary);
        }

        return index;
    }

    private static FakeCompetitionsQuery WithPolicy(CapturePolicy? policy)
    {
        var competitions = new FakeCompetitionsQuery();
        competitions.SeedCapturePolicy(Competition, policy);
        return competitions;
    }

    // ---- D10 step 1: not authenticated (401) -------------------------------

    [Fact]
    public async Task Anonymous_is_denied_notAuthenticated()
    {
        var outcome = await Policy.AuthorizeAsync(OpenEntryCommand(), Anonymous, ServicesWith(), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.notAuthenticated");
        outcome.Message.Should().NotBeNullOrEmpty();
    }

    // ---- D10 step 2: organiser always passes, before any read ---------------

    [Fact]
    public async Task An_organiser_passes_whatever_the_competitions_policy_says()
    {
        // No policy seeded — the organiser never reads the competitions index
        // (or the entry index), which is also why the bare service provider
        // below is legitimate for this case.
        var outcome = await Policy.AuthorizeAsync(OpenEntryCommand(), Organiser, ServicesWith(), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
        outcome.Code.Should().BeNull();
    }

    // ---- D10 step 3: OrganisersOnly, and the no-configured-policy default --

    [Fact]
    public async Task A_competition_with_no_configured_policy_evaluates_as_OrganisersOnly()
    {
        var outcome = await Policy.AuthorizeAsync(OpenEntryCommand(), Competitor, ServicesWith(WithPolicy(null)), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.capturePolicy.denied");
    }

    [Fact]
    public async Task OrganisersOnly_denies_a_competitor()
    {
        var outcome = await Policy.AuthorizeAsync(
            OpenEntryCommand(), Competitor, ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.OrganisersOnly, []))), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.capturePolicy.denied");
    }

    // ---- D10 step 4: an unlinked identity is nobody -------------------------

    [Fact]
    public async Task An_authenticated_unlinked_identity_is_denied_even_when_the_policy_is_open()
    {
        var outcome = await Policy.AuthorizeAsync(
            OpenEntryCommand(), Unlinked, ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AnyRegisteredPerson, []))), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.capturePolicy.denied");
    }

    [Fact]
    public async Task An_authenticated_unlinked_identity_is_denied_on_an_allow_list_too()
    {
        var outcome = await Policy.AuthorizeAsync(
            OpenEntryCommand(), Unlinked, ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AllowList, [CompetitorPerson]))), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.capturePolicy.denied");
    }

    // ---- D10 step 5: AnyRegisteredPerson ------------------------------------

    [Fact]
    public async Task AnyRegisteredPerson_allows_a_linked_competitor()
    {
        var outcome = await Policy.AuthorizeAsync(
            OpenEntryCommand(), Competitor, ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AnyRegisteredPerson, []))), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    // ---- D10 step 6: the allow-list ------------------------------------------

    [Fact]
    public async Task An_allow_list_containing_the_person_allows_them()
    {
        var outcome = await Policy.AuthorizeAsync(
            OpenEntryCommand(), Competitor, ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AllowList, [OtherPerson, CompetitorPerson]))), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task An_allow_list_not_containing_the_person_denies_them()
    {
        var outcome = await Policy.AuthorizeAsync(
            OpenEntryCommand(), Competitor, ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AllowList, [OtherPerson]))), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.capturePolicy.denied");
    }

    // ---- Entry-scoped resolution: the competition comes off the index row ---

    [Fact]
    public async Task An_entry_scoped_capture_resolves_the_competition_through_the_entry_index_and_applies_its_policy()
    {
        var outcome = await Policy.AuthorizeAsync(
            CaptureCommand(KnownEntry), Competitor,
            ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AllowList, [CompetitorPerson])), IndexWith(KnownEntry)),
            TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task An_entry_scoped_capture_resolves_the_competition_for_a_denial_too()
    {
        var outcome = await Policy.AuthorizeAsync(
            CaptureCommand(KnownEntry), Competitor,
            ServicesWith(WithPolicy(new CapturePolicy(CapturePolicyMode.AllowList, [OtherPerson])), IndexWith(KnownEntry)),
            TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.capturePolicy.denied");
    }

    // ---- Entry not found ⇒ allow through (the handler owns *.notFound) -------

    [Fact]
    public async Task An_entry_scoped_capture_for_an_entry_the_index_does_not_know_is_allowed_through()
    {
        var outcome = await Policy.AuthorizeAsync(
            CaptureCommand(UnknownEntry), Competitor, ServicesWith(WithPolicy(null), SeedIndex()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task The_entry_not_found_pass_through_applies_to_an_unlinked_identity_too()
    {
        // The pass-through is about the resource not existing, not the
        // principal — nothing is being authorised for a nonexistent EntryRef.
        var outcome = await Policy.AuthorizeAsync(
            CaptureCommand(UnknownEntry), Unlinked, ServicesWith(WithPolicy(null), SeedIndex()), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeTrue();
    }

    // ---- Fail-closed: wiring bugs deny, never guess ---------------------------

    [Fact]
    public async Task A_capture_command_carrying_neither_scope_marker_fails_closed()
    {
        // Only reachable if a C-mapped command stops implementing its marker —
        // the policy must refuse, not guess a competition.
        var outcome = await Policy.AuthorizeAsync(
            new RenamePerson(CompetitorPerson, "New Name"), Competitor, ServicesWith(WithPolicy(null)), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.policyMissing");
    }

    [Fact]
    public async Task A_missing_entry_index_fails_closed()
    {
        var outcome = await Policy.AuthorizeAsync(
            CaptureCommand(UnknownEntry), Competitor, ServicesWith(WithPolicy(null), entries: null), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.policyMissing");
    }

    [Fact]
    public async Task A_missing_competitions_index_fails_closed()
    {
        var outcome = await Policy.AuthorizeAsync(OpenEntryCommand(), Competitor, ServicesWith(competitions: null), TestContext.Current.CancellationToken);

        outcome.Allowed.Should().BeFalse();
        outcome.Code.Should().Be("auth.policyMissing");
    }
}
