using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for <see cref="Competition.Decide"/> (WI-2,
/// docs/plans/create-competition-steel-thread-plan.md, LADR-0003: CsCheck).
/// CompetitionDecideTests covers the fixed, hand-built cases; this generates
/// the name/location/date-pair space and checks two general claims: valid
/// input always succeeds with fields copied through unchanged, and an
/// inverted date pair always fails on the dates code alone — independent of
/// whatever name/location happen to be, so the date check is a genuine
/// standalone invariant rather than one accidentally coupled to the others.
/// </summary>
public class CompetitionDecidePropertyTests
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

    /// <summary>Printable, guaranteed non-blank text — a stand-in for names and locations, neither of which Decide constrains beyond non-blank.</summary>
    private static readonly Gen<string> NonBlankText =
        Gen.Char["ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 '-"]
            .Array[1, 24]
            .Select(chars => new string(chars))
            .Where(s => !string.IsNullOrWhiteSpace(s));

    private static readonly Gen<DateOnly> AnyDate =
        Gen.DateOnly[new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31)];

    [Fact]
    public void Decide_with_valid_name_location_and_a_non_inverted_date_pair_always_succeeds_with_fields_copied_through()
    {
        (from name in NonBlankText
         from location in NonBlankText
         from startDate in AnyDate
         from dayOffset in Gen.Int[0, 365]
         select (name, location, startDate, endDate: startDate.AddDays(dayOffset)))
        .Sample(t =>
        {
            var id = CompetitionId.New();
            var adoptedRules = SampleAdoptedRules();
            var at = DateTimeOffset.UtcNow;

            var result = Competition.Decide(
                id, t.name, t.location, t.startDate, t.endDate, "1.0.0", adoptedRules, at);

            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(id);
            result.Value.Name.Should().Be(t.name);
            result.Value.Location.Should().Be(t.location);
            result.Value.StartDate.Should().Be(t.startDate);
            result.Value.EndDate.Should().Be(t.endDate);
            result.Value.EvaluatorVersion.Should().Be("1.0.0");
            result.Value.AdoptedRules.Should().BeSameAs(adoptedRules);
            result.Value.At.Should().Be(at);
        });
    }

    [Fact]
    public void Decide_with_startDate_after_endDate_always_fails_on_the_dates_code_regardless_of_name_and_location()
    {
        (from name in NonBlankText
         from location in NonBlankText
         from endDate in AnyDate
         from dayOffset in Gen.Int[1, 365]
         select (name, location, endDate, startDate: endDate.AddDays(dayOffset)))
        .Sample(t =>
        {
            var result = Competition.Decide(
                CompetitionId.New(), t.name, t.location, t.startDate, t.endDate,
                "1.0.0", SampleAdoptedRules(), DateTimeOffset.UtcNow);

            result.IsFailure.Should().BeTrue();
            result.Code.Should().Be("competition.dates.invalid");
        });
    }
}
