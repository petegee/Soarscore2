using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.ConfigureCapturePolicy"/> —
/// authentication-and-authorisation.md WI-4, mirroring TeamsDecideTests's
/// style: one fact per defect code (both, asserted by stable code) plus the
/// happy paths, the strictness rule (the client says what it means), and the
/// no-gates stance (reconfiguration is always allowed; latest wins in the
/// fold).
/// </summary>
public class CapturePolicyDecideTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static Competition SampleCompetition() =>
        Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 16),
            "1.0.0",
            new AdoptedRules
            {
                Definition = SampleDefinition,
                SourceClassId = "content-hash-abc123",
                SourceVersion = SampleDefinition.Version,
                AdoptedAt = At,
            },
            At));

    // ------------------------------------------------- ConfigureCapturePolicy

    [Fact]
    public void ConfigureCapturePolicy_organisers_only_folds_the_policy()
    {
        var competition = SampleCompetition();
        var policy = new CapturePolicy(CapturePolicyMode.OrganisersOnly, []);

        var result = competition.ConfigureCapturePolicy(policy, At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "configuration succeeded");
        result.Value.Policy.Should().BeSameAs(policy);
        result.Value.At.Should().Be(At);

        var updated = competition.Apply(result.Value);
        updated.CapturePolicy.Should().BeSameAs(policy);
    }

    [Fact]
    public void ConfigureCapturePolicy_any_registered_person_folds_the_policy()
    {
        var competition = SampleCompetition();

        var result = competition.ConfigureCapturePolicy(
            new CapturePolicy(CapturePolicyMode.AnyRegisteredPerson, []), At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "configuration succeeded");

        var updated = competition.Apply(result.Value);
        updated.CapturePolicy.Should().NotBeNull();
        updated.CapturePolicy!.Mode.Should().Be(CapturePolicyMode.AnyRegisteredPerson);
        updated.CapturePolicy.Capturers.Should().BeEmpty();
    }

    [Fact]
    public void ConfigureCapturePolicy_allow_list_emits_the_capturers_verbatim_and_folds_them()
    {
        var competition = SampleCompetition();
        var capturers = new[] { PersonId.New(), PersonId.New() };
        var policy = new CapturePolicy(CapturePolicyMode.AllowList, capturers);

        var result = competition.ConfigureCapturePolicy(policy, At);

        result.IsSuccess.Should().BeTrue(result.Code ?? "configuration succeeded");
        result.Value.Policy.Capturers.Should().Equal(capturers);

        var updated = competition.Apply(result.Value);
        updated.CapturePolicy.Should().BeSameAs(policy);
    }

    [Fact]
    public void ConfigureCapturePolicy_allow_list_with_an_empty_list_fails_with_a_stable_code()
    {
        var competition = SampleCompetition();

        var result = competition.ConfigureCapturePolicy(
            new CapturePolicy(CapturePolicyMode.AllowList, []), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.capturePolicy.emptyAllowList");
    }

    [Theory]
    [InlineData(CapturePolicyMode.OrganisersOnly)]
    [InlineData(CapturePolicyMode.AnyRegisteredPerson)]
    public void ConfigureCapturePolicy_non_allow_list_mode_with_a_list_fails_with_a_stable_code(CapturePolicyMode mode)
    {
        var competition = SampleCompetition();

        var result = competition.ConfigureCapturePolicy(
            new CapturePolicy(mode, [PersonId.New()]), At);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.capturePolicy.capturersIgnored");
    }

    [Fact]
    public void ConfigureCapturePolicy_reconfiguration_is_always_allowed_and_the_latest_wins_in_the_fold()
    {
        var competition = SampleCompetition();

        var first = competition.ConfigureCapturePolicy(
            new CapturePolicy(CapturePolicyMode.OrganisersOnly, []), At);
        first.IsSuccess.Should().BeTrue();
        competition = competition.Apply(first.Value);

        var second = competition.ConfigureCapturePolicy(
            new CapturePolicy(CapturePolicyMode.AllowList, [PersonId.New()]), At.AddMinutes(1));

        second.IsSuccess.Should().BeTrue(second.Code ?? "reconfiguration succeeded");
        var updated = competition.Apply(second.Value);
        updated.CapturePolicy.Should().NotBeNull();
        updated.CapturePolicy!.Mode.Should().Be(CapturePolicyMode.AllowList);
        updated.CapturePolicy.Capturers.Should().ContainSingle();
    }
}
