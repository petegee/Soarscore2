// kanban/completed/capture-a-score-steel-thread-plan.md WI-8. Covers
// OpenEntryHandler directly against a FakeEventStore/FakeEntryQuery — same
// style as RegisterCompetitorHandlerTests.cs. The Phase/Round/TaskRound/Group
// shape is hand-built directly into the event stream rather than drawn
// through DrawPhaseHandler, mirroring OpenEntryDecideTests's own rationale
// (Domain.Tests) for doing the same at the decide-function level.
//
// No "stale-version retry" test in the RegisterCompetitor/DrawPhase sense:
// OpenEntry's only append is ExpectedVersion.NoStream against a freshly
// minted EntryId, never a re-append to a stream this handler also read, so
// there is no read-version to go stale. What genuinely races here is the
// entry_index read used for openEntry.alreadyOpen — deliberately advisory
// (the plan's WI-8, silence 2) — so the concurrency-shaped test below proves
// that race is accepted rather than that it is prevented.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Entries;

using Soarscore.Application.Queries.Entries;
namespace Soarscore.Application.Tests.Commands.Entries;

public class OpenEntryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3J = SeedF3J.Definition; // TaskD: Fixed WorkingTime = 600 s literal

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3J,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3J.Version,
            AdoptedAt = Now,
        };

    private static (FakeEventStore Store, CompetitionId CompetitionId, ImmutableArray<CompetitorId> Competitors, GroupId GroupRef)
        SeedDrawnCompetition(int competitorCount = 2)
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>();
        var version = 1L;
        for (var i = 0; i < competitorCount; i++)
        {
            var competitor = new Competitor
            {
                Id = CompetitorId.New(),
                PersonRef = PersonId.New(),
                CompetitorNumber = i + 1,
                RegisteredAt = Now,
            };
            store.AppendAsync(
                id.Value, ExpectedVersion.Exact(version), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();
            competitors.Add(competitor.Id);
            version++;
        }

        var groupRef = GroupId.New();
        var group = new Group { Id = groupRef, Ordinal = 1, CompetitorRefs = [competitors[0]] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "D", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };
        store.AppendAsync(
            id.Value, ExpectedVersion.Exact(version), [new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now)])
            .GetAwaiter().GetResult();

        return (store, id, competitors.ToImmutable(), groupRef);
    }

    [Fact]
    public async Task Opening_an_entry_for_a_drawn_competitor_succeeds_and_appends_EntryOpened_to_a_new_stream()
    {
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var entryQuery = new FakeEntryQuery();
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0]), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var stream = store.Streams[result.Value.Value];
        stream.Should().ContainSingle();
        var opened = stream[0].Should().BeOfType<EntryOpened>().Subject;
        opened.CompetitionRef.Should().Be(competitionId);
        opened.PhaseOrdinal.Should().Be(0);
        opened.RoundOrdinal.Should().Be(1);
        opened.TaskRoundOrdinal.Should().Be(1);
        opened.GroupRef.Should().Be(groupRef);
        opened.CompetitorRef.Should().Be(competitors[0]);
        opened.Role.Should().Be(ReflightRole.Original);
    }

    [Fact]
    public async Task Opening_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var entryQuery = new FakeEntryQuery();
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(CompetitionId.New(), 0, 1, 1, GroupId.New(), CompetitorId.New()), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Opening_against_a_group_that_does_not_exist_fails_with_the_domain_code_surfaced_unchanged()
    {
        var (store, competitionId, competitors, _) = SeedDrawnCompetition();
        var entryQuery = new FakeEntryQuery();
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, GroupId.New(), competitors[0]), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.groupNotFound");
    }

    [Fact]
    public async Task Opening_a_second_entry_for_the_same_competitor_and_task_round_fails_with_openEntry_alreadyOpen()
    {
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var existingId = EntryId.New();
        // The index reports an Original entry, and its stream holds a live
        // (non-annulled) EntryOpened — so a new open is refused.
        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original));
        await store.AppendAsync(existingId.Value, ExpectedVersion.NoStream,
            [new EntryOpened(existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, Now)],
            TestContext.Current.CancellationToken);
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0]), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.alreadyOpen");
        // The message must not imply the RULES forbid a second entry — only
        // that an Original one is already open (a re-flight legitimately
        // opens a second Entry for the same competitor/task-round).
        result.Message.Should().NotContain("rule");
    }

    [Fact]
    public async Task An_annulled_original_entry_does_not_block_a_new_open()
    {
        // The F3F.1.5 provisional re-flight shape: the competitor re-flies under
        // protest, the jury annuls the first attempt, and a second Original
        // Entry opens for the same competitor/task-round.
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var existingId = EntryId.New();
        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original));
        await store.AppendAsync(existingId.Value, ExpectedVersion.NoStream,
            [new EntryOpened(existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, Now)],
            TestContext.Current.CancellationToken);
        await store.AppendAsync(existingId.Value, ExpectedVersion.Exact(1),
            [new EntryAnnulled(new Annulment { Reason = "protest", By = "the jury", At = Now })],
            TestContext.Current.CancellationToken);
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0]), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_live_Filler_entry_blocks_a_new_Original_open()
    {
        // reflight-groups.md WI-5 flips the pre-reflight behaviour: an
        // Original open is now blocked by ANY live entry of ANY role, not
        // just by a live Original.
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var existingId = EntryId.New();
        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Filler));
        await store.AppendAsync(existingId.Value, ExpectedVersion.NoStream,
            [new EntryOpened(existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Filler, Now)],
            TestContext.Current.CancellationToken);
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0]), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.alreadyOpen");
    }

    [Fact]
    public async Task A_live_Original_does_not_block_an_Entitled_open()
    {
        // The reflight shape: the entitled competitor holds a live Original
        // and opens their Entitled re-flight against it.
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var existingId = EntryId.New();
        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original));
        await store.AppendAsync(existingId.Value, ExpectedVersion.NoStream,
            [new EntryOpened(existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, Now)],
            TestContext.Current.CancellationToken);
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_second_live_Entitled_entry_is_blocked_with_reflightAlreadyOpen()
    {
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var existingId = EntryId.New();
        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled));
        await store.AppendAsync(existingId.Value, ExpectedVersion.NoStream,
            [new EntryOpened(existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled, Now)],
            TestContext.Current.CancellationToken);
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("openEntry.reflightAlreadyOpen");
    }

    [Fact]
    public async Task An_annulled_Entitled_entry_does_not_block_a_new_Entitled_open()
    {
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var existingId = EntryId.New();
        var entryQuery = new FakeEntryQuery();
        entryQuery.Seed(new EntrySummary(
            existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled));
        await store.AppendAsync(existingId.Value, ExpectedVersion.NoStream,
            [new EntryOpened(existingId, competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled, Now)],
            TestContext.Current.CancellationToken);
        await store.AppendAsync(existingId.Value, ExpectedVersion.Exact(1),
            [new EntryAnnulled(new Annulment { Reason = "withdrawn ruling", By = "the jury", At = Now })],
            TestContext.Current.CancellationToken);
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Entitled),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Two_concurrent_opens_can_both_succeed_because_the_alreadyOpen_check_is_advisory_not_a_concurrency_arbiter()
    {
        // entry_index is Inline (read-your-own-writes for a single caller),
        // but FakeEntryQuery here stands in for a projection that has not yet
        // observed the first open — exactly the residual race the plan
        // accepts: two simultaneous opens for one pilot, which a single
        // scorer at a single task-round does not produce.
        var (store, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var entryQuery = new FakeEntryQuery();
        var handler = new OpenEntryHandler(store, entryQuery, new FakeClock(Now));

        var first = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0]), TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(
            new OpenEntry(competitionId, 0, 1, 1, groupRef, competitors[0]), TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().NotBe(first.Value);
    }

    [Fact]
    public void Appended_EntryOpened_folds_idempotently_when_applied_twice()
    {
        var (_, competitionId, competitors, groupRef) = SeedDrawnCompetition();
        var opened = new EntryOpened(
            EntryId.New(),
            competitionId, 0, 1, 1, groupRef, competitors[0], ReflightRole.Original, Now);

        var first = EntryProjection.Apply(null, opened);
        var second = EntryProjection.Apply(first, opened);

        second.Should().Be(first);
    }
}
