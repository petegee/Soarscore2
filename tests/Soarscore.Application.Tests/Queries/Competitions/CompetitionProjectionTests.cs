// docs/plans/create-competition-steel-thread-plan.md WI-1's own "Verify":
// feed CompetitionCreated and check the built summary, then feed one of the
// ten out-of-scope event types against a non-null summary and assert the
// second call is a no-op rather than a throw — CompetitionProjection.Apply's
// default arm is `_ => current`, unlike PeopleProjection's/
// ClassDefinitionProjection's `_ => throw`, precisely because those other ten
// event types have no producing command yet (see CompetitionProjection.cs's
// header comment for why).

using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;
using AwesomeAssertions;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class CompetitionProjectionTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = DateTimeOffset.UtcNow,
        };

    private static CompetitionCreated SampleCreatedEvent() =>
        new(
            CompetitionId.New(),
            "Club Champs 2026",
            "Auckland",
            new DateOnly(2026, 3, 14),
            new DateOnly(2026, 3, 15),
            "1.0.0",
            SampleAdoptedRules(),
            DateTimeOffset.UtcNow);

    [Fact]
    public void CompetitionCreated_builds_the_expected_summary()
    {
        var @event = SampleCreatedEvent();

        var summary = CompetitionProjection.Apply(null, @event);

        summary.Should().NotBeNull();
        summary!.Id.Should().Be(@event.Id);
        summary.Name.Should().Be(@event.Name);
        summary.Location.Should().Be(@event.Location);
        summary.StartDate.Should().Be(@event.StartDate);
        summary.EndDate.Should().Be(@event.EndDate);
        summary.ClassName.Should().Be(SampleDefinition.Name);
        summary.ClassContentHash.Should().Be(@event.AdoptedRules.SourceClassId);
    }

    [Fact]
    public void An_out_of_scope_event_type_against_a_non_null_summary_is_a_no_op()
    {
        var summary = CompetitionProjection.Apply(null, SampleCreatedEvent())!;
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

        var result = CompetitionProjection.Apply(summary, new CompetitorRegistered(competitor, DateTimeOffset.UtcNow));

        result.Should().BeSameAs(summary);
    }

    [Fact]
    public void An_out_of_scope_event_type_against_a_null_summary_is_also_a_no_op_rather_than_a_throw()
    {
        var result = CompetitionProjection.Apply(null, new TaskRoundCompleted(1, 1, 1, DateTimeOffset.UtcNow));

        result.Should().BeNull();
    }
}
