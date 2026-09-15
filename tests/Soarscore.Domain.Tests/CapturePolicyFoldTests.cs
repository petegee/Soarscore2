using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Fold-semantics tests for the capture-policy event — authentication-and-
/// authorisation.md WI-4, mirroring TeamsFoldTests's style: events applied
/// directly, fold effects asserted on the projection. The story's fold
/// contract: latest-wins whole replacement — reconfiguration is allowed at
/// any time, including mid-contest, and the log keeps every configuration.
/// </summary>
public class CapturePolicyFoldTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static CompetitionCreated SampleCreatedEvent() =>
        new(
            CompetitionId.New(),
            "Club Champs 2026",
            "Auckland",
            new DateOnly(2026, 9, 15),
            new DateOnly(2026, 9, 16),
            "1.0.0",
            new AdoptedRules
            {
                Definition = SampleDefinition,
                SourceClassId = "content-hash-abc123",
                SourceVersion = SampleDefinition.Version,
                AdoptedAt = At,
            },
            At);

    [Fact]
    public void Created_leaves_the_capture_policy_unconfigured()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        competition.CapturePolicy.Should().BeNull();
    }

    [Fact]
    public void CapturePolicyConfigured_replaces_last_wins()
    {
        var competition = Competition.Create(SampleCreatedEvent());

        competition = competition.Apply(new CapturePolicyConfigured(
            new CapturePolicy(CapturePolicyMode.OrganisersOnly, []), At));
        competition = competition.Apply(new CapturePolicyConfigured(
            new CapturePolicy(CapturePolicyMode.AnyRegisteredPerson, []), At.AddMinutes(1)));

        competition.CapturePolicy.Should().NotBeNull();
        competition.CapturePolicy!.Mode.Should().Be(CapturePolicyMode.AnyRegisteredPerson);
    }

    [Fact]
    public void A_capture_policy_stream_folds_in_order_to_the_latest_policy()
    {
        var allowList = new CapturePolicy(CapturePolicyMode.AllowList, [PersonId.New(), PersonId.New()]);

        CompetitionEvent[] stream =
        [
            SampleCreatedEvent(),
            new CapturePolicyConfigured(new CapturePolicy(CapturePolicyMode.OrganisersOnly, []), At),
            new CapturePolicyConfigured(allowList, At.AddMinutes(1)),
        ];

        var final = stream.Aggregate((Competition?)null, Competition.Apply);

        final.Should().NotBeNull();
        final.CapturePolicy.Should().BeSameAs(allowList);
    }

    [Fact]
    public void Non_creation_capture_policy_event_against_no_current_projection_throws()
    {
        FluentActions.Invoking(() =>
            Competition.Apply(null, new CapturePolicyConfigured(
                new CapturePolicy(CapturePolicyMode.OrganisersOnly, []), At)))
            .Should().Throw<ArgumentException>();
    }
}
