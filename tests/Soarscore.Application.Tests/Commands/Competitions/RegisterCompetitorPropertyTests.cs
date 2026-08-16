// kanban/completed/register-competitor-steel-thread-plan.md WI-4. The handler-level
// companion to Soarscore.Domain.Tests.CompetitionFieldPropertyTests (invariant
// 1): this drives the real async RegisterCompetitorHandler.HandleAsync, not
// just Competition.RegisterCompetitor directly, so it is what actually proves
// the cross-aggregate PersonLoader check (WI-3) composes with the domain's own
// duplicate check into invariant 2 — "a competition cannot hold more
// competitors than there are registered people."
//
// IMPORTANT: that bound is per-competition, not system-wide. System-wide,
// total competitor records legitimately exceed total people, because one
// person can enter many competitions over time. This test seeds exactly one
// competition and N real people and asserts the field never exceeds N — do
// not generalise this into a global assertion; it would be false.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Commands.Competitions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Tests.Shared.Competitions;

namespace Soarscore.Application.Tests.Commands.Competitions;

public class RegisterCompetitorPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = Now,
        };

    private static CompetitionId SeedCompetition(FakeEventStore store)
    {
        var id = CompetitionId.New();
        var created = new CompetitionCreated(
            id, "Field Property Test Comp", "Nowhere", new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [created]).GetAwaiter().GetResult();
        return id;
    }

    private static PersonId SeedPerson(FakeEventStore store)
    {
        var id = PersonId.New();
        var registered = new PersonRegistered(id, "Pilot", new ContactDetails { Email = $"{id.Value}@example.com" }, null, Now);
        store.AppendAsync(id.Value, ExpectedVersion.NoStream, [registered]).GetAwaiter().GetResult();
        return id;
    }

    private static readonly Gen<int> PoolSize = Gen.Int[1, 8];

    // Each attempt either targets a real seeded PersonId (by pool index) or a
    // freshly-minted bogus one — the mix is what exercises both rejection
    // codes across the run, not just one.
    private static readonly Gen<(bool IsBogus, int Index)> Attempt =
        from isBogus in Gen.Bool
        from index in Gen.Int[0, 999]
        select (isBogus, index);

    [Fact]
    public void RegisterCompetitorHandler_never_lets_a_competition_field_exceed_its_registered_person_pool()
    {
        (from poolSize in PoolSize
         from attempts in Attempt.Array[0, 30]
         select (poolSize, attempts))
        .Sample(t =>
        {
            var store = new FakeEventStore();
            var competitionId = SeedCompetition(store);
            var pool = Enumerable.Range(0, t.poolSize).Select(_ => SeedPerson(store)).ToImmutableArray();
            var handler = new RegisterCompetitorHandler(store, new FakeClock(Now));

            foreach (var attempt in t.attempts)
            {
                var personId = attempt.IsBogus ? PersonId.New() : pool[attempt.Index % t.poolSize];

                var result = handler
                    .HandleAsync(new RegisterCompetitor(competitionId, personId), CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                if (result.IsFailure)
                {
                    result.Code.Should().BeOneOf("registerCompetitor.personNotFound", "competition.competitor.alreadyRegistered");
                    if (attempt.IsBogus)
                    {
                        // A bogus PersonId was never seeded, so it can only
                        // ever fail the existence check, never the duplicate
                        // check.
                        result.Code.Should().Be("registerCompetitor.personNotFound");
                    }
                    else
                    {
                        result.Code.Should().Be("competition.competitor.alreadyRegistered");
                    }
                }
                else
                {
                    attempt.IsBogus.Should().BeFalse();
                }
            }

            var events = store.Streams[competitionId.Value];
            var competition = events.Aggregate((Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;

            competition.Competitors.Length.Should().BeLessThanOrEqualTo(t.poolSize);
            competition.Competitors.Select(c => c.PersonRef).Should().BeSubsetOf(pool);
        });
    }
}
