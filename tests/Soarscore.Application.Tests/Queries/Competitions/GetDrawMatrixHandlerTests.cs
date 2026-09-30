// kanban/backlog/draw-fairness-matrix.md WI-2. Covers GetDrawMatrixHandler
// directly against a FakeEventStore, with the live phase built through the
// Competition aggregate's own decide functions (the
// GetDrawProtectionDiagnosticsHandlerTests seeding pattern: SeedCompetition,
// fold-then-Append, ClassDefinitionFixtures.Minimal()).
//
// The hand-built 3-round x 2-group-of-3 fixture reuses
// PairwiseCoOccurrenceTests' partitions and hand-computed counts: this WI
// pins the projection/ordering/stats, which are example-shaped — no new
// property test (the counting kernel is already covered).

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

public class GetDrawMatrixHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);

    private static (FakeEventStore Store, CompetitionId CompetitionId) SeedCompetition()
    {
        var store = new FakeEventStore();
        var id = CompetitionId.New();
        // Minimal() declares no GroupConstraint, which folds as "no group
        // scoring — one whole-field group per round" (minPerGroup == field).
        // The 2-group-of-3 fixture needs a real minimum, so adopt Minimal's
        // task with a group constraint of 3 (the PrescribeDrawDecideTests
        // parity-task precedent).
        var minimal = ClassDefinitionFixtures.Minimal();
        var definition = ClassDefinitionFixtures.WithSingleTask(
            minimal,
            minimal.Phases[0].Tasks[0] with { Group = new GroupConstraint { MinPerGroup = 3 } });
        var created = new CompetitionCreated(
            id, "Matrix Comp 2026", "Auckland", new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
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

    /// <summary>Registers six competitors in a..f order, so CompetitorNumbers
    /// are 1..6 in that order (numbers are max+1 at registration).</summary>
    private static CompetitorId[] RegisterSix(FakeEventStore store, CompetitionId competitionId)
    {
        var ids = Enumerable.Range(0, 6).Select(_ => CompetitorId.New()).ToArray();
        foreach (var id in ids)
        {
            var captured = id;
            Append(store, competitionId, competition => competition.RegisterCompetitor(
                captured, PersonId.New(), Now));
        }

        return ids;
    }

    /// <summary>The PairwiseCoOccurrenceTests partitions as a prescribed draw:
    /// Round1 {A,B,C} {D,E,F}, Round2 {A,D,E} {B,C,F}, Round3 {A,F,B} {C,D,E}.
    /// Round 3 group 1 is deliberately listed in unsorted drawn order [a,f,b]
    /// so the order-preservation assertion has something to bite on.</summary>
    private static void PrescribeThreeRoundFixture(
        FakeEventStore store, CompetitionId competitionId, CompetitorId[] ids)
    {
        var (a, b, c, d, e, f) = (ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
        Append(store, competitionId, competition => competition.PrescribeDraw(
            [
                new PrescribedRound(null, [new PrescribedGroup([a, b, c]), new PrescribedGroup([d, e, f])]),
                new PrescribedRound(null, [new PrescribedGroup([a, d, e]), new PrescribedGroup([b, c, f])]),
                new PrescribedRound(null, [new PrescribedGroup([a, f, b]), new PrescribedGroup([c, d, e])]),
            ],
            "CD",
            Now));
    }

    private static async Task<DrawMatrixView> QueryMatrix(
        FakeEventStore store, CompetitionId competitionId,
        int? phaseOrdinal = null, int? fromRound = null, int? toRound = null)
    {
        var handler = new GetDrawMatrixHandler(store);
        var result = await handler.HandleAsync(
            new GetDrawMatrix(competitionId, phaseOrdinal, fromRound, toRound),
            TestContext.Current.CancellationToken);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task Prescribed_three_round_fixture_matches_hand_computed_entries_distribution_and_stats()
    {
        var (store, competitionId) = SeedCompetition();
        var ids = RegisterSix(store, competitionId);
        var (a, b, c, d, e, f) = (ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
        PrescribeThreeRoundFixture(store, competitionId, ids);

        var view = await QueryMatrix(store, competitionId);

        view.CompetitionRef.Should().Be(competitionId);
        view.PhaseOrdinal.Should().BeNull();
        view.FromRound.Should().BeNull();
        view.ToRound.Should().BeNull();

        // Pilot universe: the full field by CompetitorNumber asc, none withdrawn.
        view.Pilots.Select(p => (p.CompetitorRef, p.CompetitorNumber, p.Withdrawn)).Should().Equal(
            (a, 1, false), (b, 2, false), (c, 3, false),
            (d, 4, false), (e, 5, false), (f, 6, false));

        // Draw view: 6 groups in phase/round/task-round/group ordinal order.
        view.Groups.Should().HaveCount(6);
        view.Groups.Select(g => (g.PhaseOrdinal, g.RoundOrdinal, g.TaskRoundOrdinal, g.GroupOrdinal)).Should().Equal(
            (0, 1, 1, 1), (0, 1, 1, 2),
            (0, 2, 1, 1), (0, 2, 1, 2),
            (0, 3, 1, 1), (0, 3, 1, 2));
        view.Groups.Select(g => g.GroupRef).Should().OnlyHaveUniqueItems();

        // Hand-computed counts (PairwiseCoOccurrenceTests:44-64): AB=2, AC=AD=
        // AE=AF=1, BC=2, BF=2, CD=CE=CF=1, DE=3, DF=EF=1; BD/BE never met.
        // Entries are canonical (smaller number first) and ordered by
        // (row number, col number) — the Pilots axis, not Guid order.
        view.Entries.Select(e => (e.CompetitorA, e.CompetitorB, e.Count)).Should().Equal(
            (a, b, 2),
            (a, c, 1), (a, d, 1), (a, e, 1), (a, f, 1),
            (b, c, 2), (b, f, 2),
            (c, d, 1), (c, e, 1), (c, f, 1),
            (d, e, 3), (d, f, 1),
            (e, f, 1));

        // Stats universe: all 15 unordered pairs incl. never-met zeros.
        // Distribution gap-filled 0..3: 0->2 (BD,BE), 1->9, 2->3 (AB,BC,BF),
        // 3->1 (DE). Min 0, Max 3, Mean 18/15 = 1.2,
        // MAD = (2.4 + 1.8 + 2.4 + 1.8)/15 = 0.56.
        view.Distribution.Select(entry => (entry.Meetings, entry.Count)).Should().Equal(
            (0, 2), (1, 9), (2, 3), (3, 1));
        view.MinMeetings.Should().Be(0);
        view.MaxMeetings.Should().Be(3);
        view.MeanMeetings.Should().BeApproximately(1.2, 1e-9);
        view.MeanAbsoluteDeviation.Should().BeApproximately(0.56, 1e-9);
    }

    [Fact]
    public async Task Round_window_returns_only_the_requested_slice()
    {
        var (store, competitionId) = SeedCompetition();
        var ids = RegisterSix(store, competitionId);
        PrescribeThreeRoundFixture(store, competitionId, ids);

        var view = await QueryMatrix(store, competitionId, fromRound: 1, toRound: 1);

        view.FromRound.Should().Be(1);
        view.ToRound.Should().Be(1);

        // Only round 1's groups are in scope; the pilot universe is unaffected.
        view.Groups.Select(g => g.RoundOrdinal).Should().Equal(1, 1);
        view.Pilots.Should().HaveCount(6);

        // Round 1 alone: {A,B,C} {D,E,F} — six pairs meet once, nine never do.
        // Mean 6/15 = 0.4, MAD = (9*0.4 + 6*0.6)/15 = 0.48.
        var (a, b, c, d, e, f) = (ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
        view.Entries.Select(e => (e.CompetitorA, e.CompetitorB, e.Count)).Should().Equal(
            (a, b, 1), (a, c, 1), (b, c, 1),
            (d, e, 1), (d, f, 1), (e, f, 1));
        view.Distribution.Select(entry => (entry.Meetings, entry.Count)).Should().Equal(
            (0, 9), (1, 6));
        view.MinMeetings.Should().Be(0);
        view.MaxMeetings.Should().Be(1);
        view.MeanMeetings.Should().BeApproximately(0.4, 1e-9);
        view.MeanAbsoluteDeviation.Should().BeApproximately(0.48, 1e-9);
    }

    [Fact]
    public async Task FromRound_after_ToRound_fails_with_roundRangeInvalid()
    {
        var (store, competitionId) = SeedCompetition();
        var handler = new GetDrawMatrixHandler(store);

        var result = await handler.HandleAsync(
            new GetDrawMatrix(competitionId, FromRound: 3, ToRound: 1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawMatrix.roundRangeInvalid");
    }

    [Fact]
    public async Task Unknown_competition_fails_with_competition_notFound()
    {
        var handler = new GetDrawMatrixHandler(new FakeEventStore());

        var result = await handler.HandleAsync(
            new GetDrawMatrix(CompetitionId.New()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.notFound");
    }

    [Fact]
    public async Task Unknown_phaseOrdinal_fails_with_phaseNotFound()
    {
        var (store, competitionId) = SeedCompetition();
        var ids = RegisterSix(store, competitionId);
        PrescribeThreeRoundFixture(store, competitionId, ids);
        var handler = new GetDrawMatrixHandler(store);

        var result = await handler.HandleAsync(
            new GetDrawMatrix(competitionId, PhaseOrdinal: 7),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawMatrix.phaseNotFound");
    }

    [Fact]
    public async Task Undrawn_competition_lists_pilots_with_empty_groups_and_all_zero_distribution()
    {
        var (store, competitionId) = SeedCompetition();
        var ids = RegisterSix(store, competitionId);

        var view = await QueryMatrix(store, competitionId);

        // The field is known pre-draw; the scope is empty — success, never
        // not-found, with the all-zero universe (Meetings: 0, Count: 15).
        view.Pilots.Select(p => p.CompetitorNumber).Should().Equal(1, 2, 3, 4, 5, 6);
        view.Pilots.Select(p => p.Withdrawn).Should().AllBeEquivalentTo(false);
        view.Groups.Should().BeEmpty();
        view.Entries.Should().BeEmpty();
        view.Distribution.Select(entry => (entry.Meetings, entry.Count)).Should().Equal((0, 15));
        view.MinMeetings.Should().Be(0);
        view.MaxMeetings.Should().Be(0);
        view.MeanMeetings.Should().Be(0);
        view.MeanAbsoluteDeviation.Should().Be(0);
    }

    [Fact]
    public async Task Generated_draw_reads_with_the_same_row_shape_as_prescribed()
    {
        var (store, competitionId) = SeedCompetition();
        RegisterSix(store, competitionId);
        Append(store, competitionId, competition => competition.DrawPhase(
            3, ImmutableArray<string>.Empty, Now));

        // Same row shape through the phase-scoped read: pilots on the number
        // axis, groups in ordinal order with drawn order preserved, sparse
        // canonical entries, gap-filled distribution over all 15 pairs.
        var view = await QueryMatrix(store, competitionId, phaseOrdinal: 0);

        view.PhaseOrdinal.Should().Be(0);
        view.Pilots.Select(p => p.CompetitorNumber).Should().Equal(1, 2, 3, 4, 5, 6);
        view.Groups.Should().NotBeEmpty();
        view.Groups.Select(g => g.PhaseOrdinal).Should().AllBeEquivalentTo(0);
        view.Groups.Select(g => g.RoundOrdinal).Should().BeInAscendingOrder();
        view.Groups.Select(g => g.GroupRef).Should().OnlyHaveUniqueItems();

        var numbers = view.Pilots.ToDictionary(p => p.CompetitorRef, p => p.CompetitorNumber);
        view.Entries.Should().NotBeEmpty();
        foreach (var entry in view.Entries)
        {
            numbers[entry.CompetitorA].Should().BeLessThan(numbers[entry.CompetitorB]);
            entry.Count.Should().BeGreaterThan(0);
        }

        view.Entries.Select(e => (numbers[e.CompetitorA], numbers[e.CompetitorB]))
            .Should().BeInAscendingOrder();
        view.Distribution.Select(entry => entry.Meetings)
            .Should().Equal(Enumerable.Range(0, view.MaxMeetings + 1));
        view.Distribution.Sum(entry => entry.Count).Should().Be(15);
        view.Distribution.Select(entry => entry.Meetings * entry.Count).Sum()
            .Should().Be(view.Entries.Sum(e => e.Count));
    }

    [Fact]
    public async Task Withdrawn_pilot_stays_listed_and_draw_stays_intact()
    {
        var (store, competitionId) = SeedCompetition();
        var ids = RegisterSix(store, competitionId);
        var (a, b, c, d, e, f) = (ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
        Append(store, competitionId, competition => competition.PrescribeDraw(
            [new PrescribedRound(null, [new PrescribedGroup([a, b, c]), new PrescribedGroup([d, e, f])])],
            "CD",
            Now));
        Append(store, competitionId, competition => competition.WithdrawCompetitor(f, Now));

        var view = await QueryMatrix(store, competitionId);

        view.Pilots.Single(p => p.CompetitorRef == f).Withdrawn.Should().BeTrue();
        view.Pilots.Where(p => p.CompetitorRef != f)
            .Select(p => p.Withdrawn).Should().AllBeEquivalentTo(false);

        // The draw is intact on withdrawal: the group still holds F and the
        // meeting counts still see every pair.
        view.Groups.Should().HaveCount(2);
        view.Groups.SelectMany(g => g.CompetitorRefs).Should().Contain(f);
        view.Entries.Select(e => (e.CompetitorA, e.CompetitorB, e.Count)).Should().Equal(
            (a, b, 1), (a, c, 1), (b, c, 1),
            (d, e, 1), (d, f, 1), (e, f, 1));
    }

    [Fact]
    public async Task Group_competitorRefs_preserve_drawn_order()
    {
        var (store, competitionId) = SeedCompetition();
        var ids = RegisterSix(store, competitionId);
        var (a, b, c, d, e, f) = (ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
        Append(store, competitionId, competition => competition.PrescribeDraw(
            [new PrescribedRound(null, [new PrescribedGroup([f, e, d]), new PrescribedGroup([c, b, a])])],
            "CD",
            Now));

        var view = await QueryMatrix(store, competitionId);

        // Drawn order preserved verbatim, never re-sorted — even though the
        // entries for the same pairs stay canonical by competitor number.
        view.Groups.Should().HaveCount(2);
        view.Groups[0].CompetitorRefs.Should().Equal(f, e, d);
        view.Groups[1].CompetitorRefs.Should().Equal(c, b, a);
        var numbers = view.Pilots.ToDictionary(p => p.CompetitorRef, p => p.CompetitorNumber);
        foreach (var entry in view.Entries)
        {
            numbers[entry.CompetitorA].Should().BeLessThan(numbers[entry.CompetitorB]);
        }
    }
}
