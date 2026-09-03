// kanban/in-progress/teams-mvp.md WI-6. Covers GetDrawProtectionDiagnosticsHandler
// directly against a FakeEventStore, with the live phase built through the
// Competition aggregate's own decide functions. The story's assertions: empty
// when there are none, populated (group ordinal + the two competitor refs) when
// a protected pair is co-grouped — and identical treatment for generated and
// prescribed draws, because the query reads Group.CompetitorRefs and never the
// draw's provenance.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;
using Soarscore.Application.Tests.Shared.CompetitionClasses;
using FakeEventStore = Soarscore.Application.Tests.Shared.Competitions.FakeEventStore;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class GetDrawProtectionDiagnosticsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var definition = ClassDefinitionFixtures.Minimal();
        var created = new CompetitionCreated(
            id, "Teams Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", new AdoptedRules
            {
                Definition = definition,
                SourceClassId = "content-hash-synthetic",
                SourceVersion = definition.Version!,
                AdoptedAt = Now,
            }, Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return (store, id);
    }

    /// <summary>Folds the stream so decide functions can build the next event,
    /// then appends it — the ScoreTaskRoundHandlerTests seeding pattern.</summary>
    private static void Append<TEvent>(FakeEventStore store, CompetitionId competitionId, Func<Competition, Result<TEvent>> decide)
        where TEvent : CompetitionEvent
    {
        var read = store.ReadStreamAsync(competitionId.Value, 0).GetAwaiter().GetResult();
        var competition = read.Value.Aggregate(
            (Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;

        var decided = decide(competition);
        decided.IsSuccess.Should().BeTrue();
        store.AppendAsync(
            competitionId.Value, ExpectedVersion.Exact(read.Value.Count), [decided.Value])
            .GetAwaiter().GetResult().IsSuccess.Should().BeTrue();
    }

    /// <summary>A two-competitor competition whose protection group names exactly
    /// those two — the protected pair. No phase yet.</summary>
    private static (FakeEventStore Store, CompetitionId CompetitionId, CompetitorId A, CompetitorId B)
        SeedCompetitionWithProtectedPair()
    {
        var (store, competitionId) = SeedCompetition();

        var a = CompetitorId.New();
        var b = CompetitorId.New();
        Append(store, competitionId, competition => competition.RegisterCompetitor(
            a, PersonId.New(), Now));
        Append(store, competitionId, competition => competition.RegisterCompetitor(
            b, PersonId.New(), Now));
        Append(store, competitionId, competition => competition.DefineProtectionGroup(
            ProtectionGroupId.New(), "Helpers", Now));
        Append(store, competitionId, competition => competition.AddProtectionGroupMember(a, competition.ProtectionGroups[0].Id, Now));
        Append(store, competitionId, competition => competition.AddProtectionGroupMember(b, competition.ProtectionGroups[0].Id, Now));

        return (store, competitionId, a, b);
    }

    private static async Task<ImmutableArray<DrawProtectionViolationView>> QueryViolations(
        FakeEventStore store, CompetitionId competitionId)
    {
        var handler = new GetDrawProtectionDiagnosticsHandler(store);
        var result = await handler.HandleAsync(
            new GetDrawProtectionDiagnostics(competitionId), TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value.Violations;
    }

    [Fact]
    public async Task An_undrawn_competition_reports_no_violations_even_with_protected_members()
    {
        var (store, competitionId, _, _) = SeedCompetitionWithProtectedPair();

        var violations = await QueryViolations(store, competitionId);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task A_generated_draw_co_grouping_a_protected_pair_reports_it()
    {
        var (store, competitionId, a, b) = SeedCompetitionWithProtectedPair();
        Append(store, competitionId, competition => competition.DrawPhase(
            1, ImmutableArray<string>.Empty, Now));

        var violations = await QueryViolations(store, competitionId);

        // The whole-field draw puts both competitors in one group — the
        // protected pair is unavoidably co-grouped and must show as exactly
        // one diagnostic row.
        var violation = violations.Should().ContainSingle().Subject;
        violation.PhaseOrdinal.Should().Be(0);
        violation.RoundOrdinal.Should().Be(1);
        violation.TaskRoundOrdinal.Should().Be(1);
        violation.GroupOrdinal.Should().Be(1);
        new[] { violation.CompetitorA, violation.CompetitorB }.Should().Equal(
            new ProtectedPair(a, b).A, new ProtectedPair(a, b).B);
    }

    [Fact]
    public async Task A_prescribed_draw_co_grouping_a_protected_pair_reports_it_identically()
    {
        var (store, competitionId, a, b) = SeedCompetitionWithProtectedPair();
        Append(store, competitionId, competition => competition.PrescribeDraw(
            [new PrescribedRound(null, [new PrescribedGroup([a, b])])], "CD", Now));

        var violations = await QueryViolations(store, competitionId);

        // Diagnostic-only for prescribed imports too — same row shape, same
        // provenance-blind read of the live phase's groups.
        var violation = violations.Should().ContainSingle().Subject;
        violation.PhaseOrdinal.Should().Be(0);
        violation.RoundOrdinal.Should().Be(1);
        violation.TaskRoundOrdinal.Should().Be(1);
        violation.GroupOrdinal.Should().Be(1);
        new[] { violation.CompetitorA, violation.CompetitorB }.Should().Equal(
            new ProtectedPair(a, b).A, new ProtectedPair(a, b).B);
    }

    [Fact]
    public async Task Removing_the_protection_membership_before_the_draw_clears_the_diagnostics()
    {
        var (store, competitionId, a, b) = SeedCompetitionWithProtectedPair();
        Append(store, competitionId, competition => competition.RemoveProtectionGroupMember(
            b, competition.ProtectionGroups[0].Id, Now));
        Append(store, competitionId, competition => competition.DrawPhase(
            1, ImmutableArray<string>.Empty, Now));

        var violations = await QueryViolations(store, competitionId);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task Diagnostics_against_an_unknown_competition_fail_with_competition_notFound()
    {
        var handler = new GetDrawProtectionDiagnosticsHandler(new FakeEventStore());

        var result = await handler.HandleAsync(
            new GetDrawProtectionDiagnostics(CompetitionId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }
}
