// docs/plans/phase-drawn-steel-thread-plan.md WI-4. The handler-level
// companion to Soarscore.Domain.Tests's PhaseDrawPropertyTests (WI-2,
// invariant 1): this drives the real async DrawPhaseHandler.HandleAsync
// against a FakeEventStore, not just Competition.DrawPhase directly, so it
// proves the field-size correctness of a real draw survives the
// load->decide->append handler plumbing, end to end.
//
// Exercises F3J only (Soarscore.SeedData.SeedF3J.Definition), because F3J
// has a literal, non-parameterised MinPerGroup (6 — see F3J.6.1). This test
// deliberately does NOT assert anything about F5K, F5L or NZ Class M, whose
// MinPerGroup is a Parameter (NumberOrParam.Param), not a literal — those
// classes cannot succeed a draw until a future BindParameter thread exists
// to resolve the parameter first (drawPhase.parameterUnbound otherwise). Do
// not "fix" this test into asserting those classes succeed.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.Competitions;

public class DrawPhasePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F3J = SeedF3J.Definition; // literal MinPerGroup = 6
    private const int MinPerGroup = 6;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F3J,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F3J.Version,
            AdoptedAt = Now,
        };

    private static CompetitionId SeedCompetition(FakeEventStore store)
    {
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Draw Property Test Comp", "Nowhere", new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return id;
    }

    // Starts at version 1, not 0: the stream already carries one event
    // (CompetitionCreated) by the time this runs — same starting point
    // DrawPhaseHandlerTests.SeedRegisteredCompetitors uses explicitly.
    private static ImmutableArray<CompetitorId> SeedRegisteredCompetitors(FakeEventStore store, CompetitionId competitionId, int count)
    {
        var ids = ImmutableArray.CreateBuilder<CompetitorId>(count);
        for (var i = 0; i < count; i++)
        {
            var competitor = new Competitor
            {
                Id = CompetitorId.New(),
                PersonRef = PersonId.New(),
                CompetitorNumber = i + 1,
                RegisteredAt = Now,
            };
            var append = store.AppendAsync(
                competitionId.Value, ExpectedVersion.Exact(i + 1), [new CompetitorRegistered(competitor, Now)]).GetAwaiter().GetResult();
            append.IsSuccess.Should().BeTrue();
            ids.Add(competitor.Id);
        }

        return ids.MoveToImmutable();
    }

    private static readonly Gen<int> FieldSize = Gen.Int[1, 20];
    private static readonly Gen<int> RoundCount = Gen.Int[1, 3];

    [Fact]
    public void DrawPhaseHandler_field_at_or_above_MinPerGroup_always_succeeds_with_every_competitor_grouped_every_round()
    {
        (from fieldSize in Gen.Int[MinPerGroup, 20]
         from rounds in RoundCount
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var store = new FakeEventStore();
            var competitionId = SeedCompetition(store);
            var competitorIds = SeedRegisteredCompetitors(store, competitionId, t.fieldSize);
            var handler = new DrawPhaseHandler(store, new FakeClock(Now));

            var result = handler
                .HandleAsync(new DrawPhase(competitionId, t.rounds), TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();

            result.IsSuccess.Should().BeTrue();

            var stream = store.Streams[competitionId.Value];
            var drawn = stream[^1].Should().BeOfType<PhaseDrawn>().Subject;
            drawn.Rounds.Length.Should().Be(t.rounds);

            foreach (var round in drawn.Rounds)
            {
                var grouped = round.TaskRounds
                    .SelectMany(taskRound => taskRound.Groups)
                    .SelectMany(group => group.CompetitorRefs)
                    .ToImmutableArray();

                grouped.Should().BeEquivalentTo(competitorIds, "every competitor must be grouped exactly once in every round");
                grouped.Length.Should().Be(competitorIds.Length);
            }
        });
    }

    [Fact]
    public void DrawPhaseHandler_field_below_MinPerGroup_always_fails_with_fieldTooSmall_and_no_other_code()
    {
        (from fieldSize in Gen.Int[1, MinPerGroup - 1]
         from rounds in RoundCount
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var store = new FakeEventStore();
            var competitionId = SeedCompetition(store);
            SeedRegisteredCompetitors(store, competitionId, t.fieldSize);
            var handler = new DrawPhaseHandler(store, new FakeClock(Now));

            var result = handler
                .HandleAsync(new DrawPhase(competitionId, t.rounds), TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();

            result.IsFailure.Should().BeTrue();
            result.Code.Should().Be("drawPhase.fieldTooSmall");
        });
    }

    [Fact]
    public void DrawPhaseHandler_appended_PhaseDrawn_folds_idempotently_when_applied_twice()
    {
        (from fieldSize in FieldSize
         from rounds in RoundCount
         select (fieldSize, rounds))
        .Sample(t =>
        {
            var store = new FakeEventStore();
            var competitionId = SeedCompetition(store);
            SeedRegisteredCompetitors(store, competitionId, t.fieldSize);
            var handler = new DrawPhaseHandler(store, new FakeClock(Now));

            var result = handler
                .HandleAsync(new DrawPhase(competitionId, t.rounds), TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();

            if (result.IsFailure)
            {
                // Below MinPerGroup — covered by the dedicated failure test
                // above; nothing to fold here.
                result.Code.Should().Be("drawPhase.fieldTooSmall");
                return;
            }

            var events = store.Streams[competitionId.Value];
            var priorEvents = events.Take(events.Count - 1);
            var priorState = priorEvents.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;
            var phaseDrawn = (PhaseDrawn)events[^1];

            // Idempotent replay (LADR-0001 §4.10): applying the same
            // PhaseDrawn event to the same prior Competition state twice,
            // independently, must reproduce an equal Phases[0] both times —
            // the fold is a pure function of (current, event), not something
            // that mutates hidden state or re-derives the draw differently
            // on a second pass.
            var first = priorState.Apply(phaseDrawn);
            var second = priorState.Apply(phaseDrawn);

            second.Phases[0].Should().Be(first.Phases[0]);
        });
    }
}
