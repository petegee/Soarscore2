// kanban/in-progress/lane-assignment.md WI-6 (Stage 2). Covers
// AssignGroupSpotsHandler directly against a FakeEventStore — same style as
// DrawPhaseHandlerTests.cs: no cross-aggregate read (finding 9 — the decide
// function re-derives the group's live membership from the fold itself, so
// the handler needs no port beyond IEventStore/IClock), the adopted class
// definition is already sitting in AdoptedRules. The draw is produced by the
// real DrawPhaseHandler, and the drawn group's id and members are read back
// from the fold.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class AssignGroupSpotsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3K = SeedF3K.Definition; // literal MinPerGroup = 5

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3K.Version,
            AdoptedAt = Now,
        };

    /// <summary>
    /// F3K, 8 competitors registered, one round (task A) drawn through the
    /// real DrawPhaseHandler — field ≤ 9 with F3K's MinPerGroup 5 means one
    /// group, whose id and members come back from the fold.
    /// </summary>
    private static async Task<(FakeEventStore Store, CompetitionId CompetitionId, GroupId GroupRef, ImmutableArray<CompetitorId> Members)>
        SeedDrawnCompetitionAsync()
    {
        var store = new FakeEventStore();
        var competitionId = CompetitionId.New();
        var created = new CompetitionCreated(
            competitionId, "Club Champs 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now);
        await store.AppendAsync(competitionId.Value, ExpectedVersion.NoStream, [created], TestContext.Current.CancellationToken);

        var version = 1L;
        for (var i = 0; i < 8; i++)
        {
            var competitor = new Competitor
            {
                Id = CompetitorId.New(),
                PersonRef = PersonId.New(),
                CompetitorNumber = i + 1,
                RegisteredAt = Now,
            };
            await store.AppendAsync(
                competitionId.Value, ExpectedVersion.Exact(version), [new CompetitorRegistered(competitor, Now)],
                TestContext.Current.CancellationToken);
            version++;
        }

        var drawHandler = new DrawPhaseHandler(store, new FakeClock(Now));
        var drawn = await drawHandler.HandleAsync(
            new DrawPhase(competitionId, 1, ["A"]), TestContext.Current.CancellationToken);
        drawn.IsSuccess.Should().BeTrue(drawn.Code ?? "draw succeeded");

        var competition = store.Streams[competitionId.Value]
            .Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        var group = competition.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single();

        return (store, competitionId, group.Id, group.CompetitorRefs);
    }

    [Fact]
    public async Task Assigning_a_drawn_groups_spots_appends_the_event_and_returns_the_group_id_it_named()
    {
        var (store, competitionId, groupRef, members) = await SeedDrawnCompetitionAsync();
        var handler = new AssignGroupSpotsHandler(store, new FakeClock(Now));

        // Deliberately non-contiguous spots in an as-given scramble (D1: a
        // broken lane skipped is the ordinary case) — the fold stores exactly
        // what was commanded; the read view sorts.
        var commanded = new List<GroupSpot>
        {
            new(members[0], 3),
            new(members[1], 1),
            new(members[2], 7),
            new(members[3], 2),
            new(members[4], 13),
            new(members[5], 5),
            new(members[6], 21),
            new(members[7], 8),
        };

        var result = await handler.HandleAsync(
            new AssignGroupSpots(competitionId, 0, 1, 1, groupRef, commanded),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Code ?? "assignment succeeded");
        result.Value.Should().Be(groupRef);

        var stream = store.Streams[competitionId.Value];
        stream.Should().HaveCount(11); // 1 created + 8 registered + 1 drawn + 1 assigned
        var assigned = stream[^1].Should().BeOfType<GroupSpotsAssigned>().Subject;
        assigned.GroupRef.Should().Be(groupRef);
        assigned.Spots.ToArray().Should().Equal(commanded);

        var folded = stream
            .Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
        var group = folded.Phases.Single().Rounds.Single().TaskRounds.Single().Groups.Single(g => g.Id == groupRef);
        group.Spots.ToArray().Should().Equal(commanded);
    }

    [Fact]
    public async Task A_defective_assignment_surfaces_the_decide_functions_stable_code_unchanged()
    {
        var (store, competitionId, groupRef, members) = await SeedDrawnCompetitionAsync();

        // Full coverage (D4) fails: the last live member has no spot.
        var commanded = members.Take(members.Length - 1)
            .Select((member, index) => new GroupSpot(member, index + 1))
            .ToList();

        var handler = new AssignGroupSpotsHandler(store, new FakeClock(Now));
        var result = await handler.HandleAsync(
            new AssignGroupSpots(competitionId, 0, 1, 1, groupRef, commanded),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("assignSpots.memberMissing");

        // Nothing appended — the last event is still the draw.
        store.Streams[competitionId.Value][^1].Should().BeOfType<PhaseDrawn>();
    }

    [Fact]
    public async Task Assigning_against_an_unknown_competition_fails_with_competition_notFound()
    {
        var store = new FakeEventStore();
        var handler = new AssignGroupSpotsHandler(store, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new AssignGroupSpots(CompetitionId.New(), 0, 1, 1, GroupId.New(), [new GroupSpot(CompetitorId.New(), 1)]),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }
}
