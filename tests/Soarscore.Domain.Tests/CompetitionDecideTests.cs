using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.Decide"/> (WI-2,
/// docs/plans/create-competition-steel-thread-plan.md) — mirrors
/// PersonDecideTests's style. AdoptedRules validity is deliberately not
/// exercised here (the plan explicitly excludes it from this decide
/// function's scope); only that it passes through the resulting event
/// unchanged is asserted.
/// </summary>
public class CompetitionDecideTests
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

    [Fact]
    public void Decide_with_valid_input_succeeds_with_the_expected_event()
    {
        var id = CompetitionId.New();
        var adoptedRules = SampleAdoptedRules();
        var startDate = new DateOnly(2026, 3, 14);
        var endDate = new DateOnly(2026, 3, 15);
        var at = DateTimeOffset.UtcNow;

        var result = Competition.Decide(
            id, "Club Champs 2026", "Auckland", startDate, endDate, "1.0.0", adoptedRules, at);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.Name.Should().Be("Club Champs 2026");
        result.Value.Location.Should().Be("Auckland");
        result.Value.StartDate.Should().Be(startDate);
        result.Value.EndDate.Should().Be(endDate);
        result.Value.EvaluatorVersion.Should().Be("1.0.0");
        result.Value.AdoptedRules.Should().BeSameAs(adoptedRules);
        result.Value.At.Should().Be(at);
    }

    [Fact]
    public void Decide_with_equal_start_and_end_dates_succeeds()
    {
        var sameDay = new DateOnly(2026, 3, 14);

        var result = Competition.Decide(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            sameDay, sameDay, "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_with_a_blank_name_fails_with_a_stable_code(string blankName)
    {
        var result = Competition.Decide(
            CompetitionId.New(), blankName, "Auckland",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.name.blank");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_with_a_blank_location_fails_with_a_stable_code(string blankLocation)
    {
        var result = Competition.Decide(
            CompetitionId.New(), "Club Champs 2026", blankLocation,
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.location.blank");
    }

    [Fact]
    public void Decide_with_start_date_after_end_date_fails_with_a_stable_code()
    {
        var result = Competition.Decide(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 14),
            "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.dates.invalid");
    }
}
