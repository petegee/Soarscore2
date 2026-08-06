// docs/plans/class-definition-adoption-steel-thread-plan.md WI-2. One negative
// fixture per numbered check, each built from a minimal baseline that itself
// validates clean, mutated to break exactly the one construct that check
// guards. Plus the corpus-wide "all seed classes validate clean" assertion
// LADR-0002 §1 asks for ("seed classes must enter through the same door as
// user classes").

using AwesomeAssertions;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.CompetitionClasses;

public class ClassDefinitionValidationTests
{
    [Fact]
    public void Minimal_baseline_validates_clean()
    {
        ClassDefinitionValidation.Validate(Minimal()).Should().BeEmpty();
    }

    [Fact]
    public void Check1_metric_ref_must_resolve_on_the_task()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Score = [new RateTerm { MetricRef = "bogus", Rate = 1 }],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-1.unresolved-metric-ref");
    }

    [Fact]
    public void Check2_rankByMetric_must_resolve_on_the_task()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Flights = new BestNFlights { Count = 1, RankByMetric = "bogus" },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-2.unresolved-rank-by-metric");
    }

    [Fact]
    public void Check3_parameter_ref_must_resolve_to_a_declared_parameter()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Timing = definition.Phases[0].Tasks[0].Timing with { MaxLaunches = NumberOrParam.Param("undeclared") },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-3.unresolved-parameter-ref");
    }

    [Fact]
    public void Check7_parameter_unit_must_agree_with_the_slot_it_is_consumed_in()
    {
        var definition = Minimal() with
        {
            Parameters = [new Parameter { Name = "wt", Unit = "m" }],
        };
        var task = definition.Phases[0].Tasks[0] with
        {
            Timing = definition.Phases[0].Tasks[0].Timing with { WorkingTime = NumberOrParam.Param("wt") },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-7.parameter-unit-mismatch");
    }

    [Fact]
    public void Check8_adjacent_piecewise_bands_naming_different_parameters_do_not_meet()
    {
        var definition = Minimal() with
        {
            Parameters =
            [
                new Parameter { Name = "a" },
                new Parameter { Name = "b" },
            ],
        };
        var task = definition.Phases[0].Tasks[0] with
        {
            Score =
            [
                new PiecewiseTerm
                {
                    MetricRef = "flightTime",
                    Bands =
                    [
                        new Band(null, NumberOrParam.Param("a"), 1),
                        new Band(NumberOrParam.Param("b"), null, -1),
                    ],
                },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-8.piecewise-bands-do-not-meet");
    }

    [Fact]
    public void Check9_lookup_rows_must_ascend()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Score =
            [
                new LookupTerm
                {
                    MetricRef = "flightTime",
                    Rows = [new LookupRow(100, 10), new LookupRow(50, 20)],
                },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-9.rows-not-ascending");
    }

    [Fact]
    public void Check9_unbounded_lookup_row_must_be_last()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Score =
            [
                new LookupTerm
                {
                    MetricRef = "flightTime",
                    Rows = [new LookupRow(null, 10), new LookupRow(100, 20)],
                },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-9.unbounded-row-not-last");
    }

    [Fact]
    public void Check10_drop_policy_gates_must_be_strictly_descending()
    {
        var definition = Minimal();
        var phase = definition.Phases[0] with
        {
            Drops =
            [
                new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1, ApplyWhenRoundsCompletedAtLeast = 4 },
                new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1, ApplyWhenRoundsCompletedAtLeast = 6 },
            ],
        };
        definition = definition with { Phases = [phase] };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-10.drops-not-descending");
    }

    [Fact]
    public void Check11_finalRanking_SinglePhase_is_rejected_with_more_than_one_phase()
    {
        var definition = TwoPhases() with { FinalRanking = FinalRankingKind.SinglePhase };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-11.single-phase-final-ranking-with-multiple-phases");
    }

    [Fact]
    public void Check12_finalRanking_is_required_with_more_than_one_phase()
    {
        var definition = TwoPhases();

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-12.missing-final-ranking");
    }

    [Fact]
    public void Check13_minNewGroupSize_must_not_be_set_when_reflight_is_not_permitted()
    {
        var definition = Minimal() with
        {
            Reflight = new ReflightRule
            {
                EntitledScores = ReflightSelection.NotPermitted,
                OthersScore = ReflightSelection.NotPermitted,
                MinNewGroupSize = 5,
            },
        };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-13.minnewgroupsize-with-no-reflight");
    }

    [Fact]
    public void Check14_normalised_terms_require_a_normalise_stage()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            ScoreNormalised = [new ConstantTerm { Value = 1 }],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-14.normalised-terms-without-normalisation");
    }

    [Fact]
    public void Check15_normalisation_requires_a_group_constraint()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Normalise = new Normalisation { Direction = NormalisationDirection.HigherIsBetter, WinnerScore = 1000 },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-15.normalisation-without-group");
    }

    [Fact]
    public void Check16_exclusion_group_members_must_all_be_deduct_points()
    {
        var definition = Minimal() with
        {
            Penalties =
            [
                new PenaltyDefinition
                {
                    InfractionType = "test",
                    ExclusionGroups = ["g"],
                    Effects = [new PenaltyEffectSpec(PenaltyEffect.ZeroFlight)],
                },
            ],
        };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.check-16.exclusion-group-non-deduct-effect");
    }

    [Fact]
    public void All_seed_definitions_validate_clean()
    {
        foreach (var (fileName, definition) in Corpus.All)
        {
            var defects = ClassDefinitionValidation.Validate(definition);
            defects.Should().BeEmpty($"{fileName} is part of the model's own test corpus and must validate clean");
        }
    }

    // ---------------------------------------------------------------- fixtures

    private static ClassDefinition Minimal() => new()
    {
        Name = "Test Class",
        Version = "v1",
        Reflight = new ReflightRule { EntitledScores = ReflightSelection.BetterOf, OthersScore = ReflightSelection.BetterOf },
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
                        Code = "A",
                        Name = "Task A",
                        Metrics = [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" }],
                        Flights = new LastFlight(),
                        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
                        Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
                    },
                ],
            },
        ],
    };

    private static ClassDefinition TwoPhases()
    {
        var definition = Minimal();
        var phase = definition.Phases[0];
        return definition with { Phases = [phase, phase with { Ordinal = 2 }] };
    }

    private static ClassDefinition WithSingleTask(ClassDefinition definition, TaskDefinition task) =>
        definition with { Phases = [definition.Phases[0] with { Tasks = [task] }] };
}
