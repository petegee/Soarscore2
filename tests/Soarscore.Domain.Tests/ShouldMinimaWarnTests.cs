// kanban/in-progress/should-level-minima-warn-dont-refuse.md WI-2.
//
// Example tests for the SHOULD-vs-shall recalibration: a SHOULD-level minimum
// prescribes (or draws) with a recorded warning, a shall minimum refuses
// exactly as before, and no group under 2 ever prescribes. Synthetic class
// definitions carry the hardness — no seed sets it yet (WI-3 owns seeds), and
// every seed is still default-hard, so the corpus behaviour is unchanged.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class ShouldMinimaWarnTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private const int MinPerGroup = 6;

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number }];

    private static ClassDefinition DefinitionWith(MinEnforcement? hardness) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks =
                [
                    new TaskDefinition
                    {
                        Code = "T",
                        Name = "Synthetic task",
                        Metrics = MetricDefs,
                        Flights = new LastFlight(),
                        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
                        Group = new GroupConstraint { MinPerGroup = MinPerGroup, MinEnforcement = hardness },
                        Normalise = new Normalisation
                        {
                            Direction = NormalisationDirection.HigherIsBetter, WinnerScore = 1000,
                        },
                        Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
                    },
                ],
            },
        ],
    };

    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "SHOULD Minima Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < competitorCount; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        return competition;
    }

    private static ImmutableArray<CompetitorId> EligibleField(Competition competition) =>
        [.. competition.Competitors.Where(c => c.WithdrawnAt is null).Select(c => c.Id)];

    private static PrescribedRound MakeRound(params IReadOnlyList<CompetitorId>[] groups) =>
        new(null, [.. groups.Select(g => new PrescribedGroup(g))]);

    // The R5 shape from the christchurch witness: 18 pilots drawn 5/6/7
    // against a SHOULD-level minimum of 6.

    [Fact]
    public void R5_shape_against_a_SHOULD_minimum_prescribes_with_one_warning_naming_the_group()
    {
        var competition = CompetitionAdopting(DefinitionWith(MinEnforcement.Should), 18);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(field.Take(5).ToArray(), field.Skip(5).Take(6).ToArray(), field.Skip(11).Take(7).ToArray())],
            "CD", Now);

        result.IsSuccess.Should().BeTrue(result.Code ?? "prescription succeeded");

        // One entry per breach: round 1, group 1, 5 against 6.
        var warnings = result.Value.Warnings!;
        warnings.Count.Should().Be(1);
        warnings[0].Code.Should().Be("prescribeDraw.groupBelowClassMinimum");
        warnings[0].Message.Should().Contain("Round 1");
        warnings[0].Message.Should().Contain("group 1");
        warnings[0].Message.Should().Contain("5 member(s)");
        warnings[0].Message.Should().Contain("(6)");

        // The synchronous channel carries the same entries.
        result.Advisories.Should().Equal(warnings);

        // The fold retains them on the phase — GET /competition rides free.
        var folded = competition.Apply(result.Value);
        folded.Phases.Single().Warnings.Should().Equal(warnings);
    }

    [Fact]
    public void R5_shape_against_a_shall_minimum_refuses_with_the_stable_code()
    {
        // Absent hardness is Shall (WI-1): today's hard refusal, byte-identical.
        var competition = CompetitionAdopting(DefinitionWith(null), 18);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(field.Take(5).ToArray(), field.Skip(5).Take(6).ToArray(), field.Skip(11).Take(7).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.groupBelowClassMinimum");
        result.Message.Should().Be(
            "Round 1, group 1: the group has 5 member(s), smaller than the class's minimum group size (6).");
        result.Advisories.Should().BeEmpty();
    }

    [Fact]
    public void A_singleton_group_refuses_under_SHOULD_too_with_no_warning()
    {
        // G5's sub-2 floor is class-independent: a lone pilot cannot
        // group-score, whatever the hardness.
        var competition = CompetitionAdopting(DefinitionWith(MinEnforcement.Should), 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(field.Take(1).ToArray(), field.Skip(1).Take(11).ToArray())],
            "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.groupTooSmall");
        result.Advisories.Should().BeEmpty();
    }

    [Fact]
    public void DrawPhase_with_a_field_below_a_SHOULD_minimum_draws_the_single_group_with_a_warning()
    {
        var competition = CompetitionAdopting(DefinitionWith(MinEnforcement.Should), 5);

        var result = competition.DrawPhase(1, [], Now);

        result.IsSuccess.Should().BeTrue(result.Code ?? "generation succeeded");

        var warnings = result.Value.Warnings!;
        warnings.Count.Should().Be(1);
        warnings[0].Code.Should().Be("drawPhase.fieldTooSmall");
        warnings[0].Message.Should().Contain("Round 1");
        warnings[0].Message.Should().Contain("eligible field (5)");
        warnings[0].Message.Should().Contain("(6)");

        result.Advisories.Should().Equal(warnings);

        // The generator itself is untouched: one whole-field group.
        result.Value.Rounds.Single().TaskRounds[0].Groups.Length.Should().Be(1);
        result.Value.Rounds.Single().TaskRounds[0].Groups[0].CompetitorRefs.Length.Should().Be(5);

        var folded = competition.Apply(result.Value);
        folded.Phases.Single().Warnings.Should().Equal(warnings);
    }

    [Fact]
    public void DrawPhase_with_a_field_below_a_shall_minimum_refuses_with_the_stable_code()
    {
        var competition = CompetitionAdopting(DefinitionWith(null), 5);

        var result = competition.DrawPhase(1, [], Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.fieldTooSmall");
        result.Advisories.Should().BeEmpty();
    }

    // G1/G2/G3 stay terminal refusals on both hardnesses — not minima gates.

    [Fact]
    public void AlreadyDrawn_refuses_under_SHOULD()
    {
        var competition = CompetitionAdopting(DefinitionWith(MinEnforcement.Should), 12);
        competition = competition.Apply(competition.DrawPhase(1, [], Now).Value);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(field.Take(6).ToArray(), field.Skip(6).Take(6).ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.alreadyDrawn");
        result.Advisories.Should().BeEmpty();
    }

    [Fact]
    public void An_empty_field_refuses_under_SHOULD()
    {
        var competition = CompetitionAdopting(DefinitionWith(MinEnforcement.Should), 0);

        var result = competition.DrawPhase(1, [], Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("drawPhase.fieldEmpty");
        result.Advisories.Should().BeEmpty();
    }

    [Fact]
    public void An_unbound_parameterised_minimum_refuses_under_SHOULD()
    {
        var task = DefinitionWith(MinEnforcement.Should).Phases[0].Tasks[0] with
        {
            Group = new GroupConstraint
            {
                MinPerGroup = NumberOrParam.Param("minPerGroup"),
                MinEnforcement = MinEnforcement.Should,
            },
        };
        var definition = DefinitionWith(MinEnforcement.Should) with
        {
            Phases = [DefinitionWith(MinEnforcement.Should).Phases[0] with { Tasks = [task] }],
        };
        var competition = CompetitionAdopting(definition, 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(field.Take(6).ToArray(), field.Skip(6).Take(6).ToArray())], "CD", Now);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("prescribeDraw.parameterUnbound");
        result.Advisories.Should().BeEmpty();
    }

    // The advisory channel is additive: every pre-existing path stays empty.

    [Fact]
    public void A_clean_prescription_carries_no_warnings_and_no_advisories()
    {
        var competition = CompetitionAdopting(DefinitionWith(MinEnforcement.Should), 12);
        var field = EligibleField(competition);

        var result = competition.PrescribeDraw(
            [MakeRound(field.Take(6).ToArray(), field.Skip(6).Take(6).ToArray())], "CD", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Warnings.Should().BeEmpty();
        result.Advisories.Should().BeEmpty();
    }

    [Fact]
    public void Result_advisories_are_empty_on_every_pre_existing_path()
    {
        Result<int>.Success(42).Advisories.Should().BeEmpty();
        Result<int>.Failure("some.code", "some message").Advisories.Should().BeEmpty();
    }
}
