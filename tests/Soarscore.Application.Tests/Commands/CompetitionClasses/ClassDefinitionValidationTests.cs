// kanban/completed/class-definition-adoption-steel-thread-plan.md WI-2 and
// kanban/in-progress/tie-break-policy-in-class-definition.md WI-2 (checks 17–19).
// One negative fixture per numbered check, each built from a minimal baseline that itself
// validates clean, mutated to break exactly the one construct that check
// guards. Plus the corpus-wide "all seed classes validate clean" assertion
// LADR-0002 §1 asks for ("seed classes must enter through the same door as
// user classes").

using AwesomeAssertions;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;
using static Soarscore.Application.Tests.Shared.CompetitionClasses.ClassDefinitionFixtures;
using Predicate = Soarscore.Domain.PublishedClassDefinition.Predicate;

namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-1.unresolved-metric-ref");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-2.unresolved-rank-by-metric");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-3.unresolved-parameter-ref");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-7.parameter-unit-mismatch");
    }

    [Fact]
    public void Check8_adjacent_piecewise_bands_naming_different_parameters_do_not_meet()
    {
        // Unit "s" matches Minimal()'s "flightTime" metric so check 7 (parameter
        // unit agreement) does not also fire on these bands' from/to slots —
        // isolates the mutation to check 8 alone.
        var definition = Minimal() with
        {
            Parameters =
            [
                new Parameter { Name = "a", Unit = "s" },
                new Parameter { Name = "b", Unit = "s" },
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-8.piecewise-bands-do-not-meet");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-9.rows-not-ascending");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-9.unbounded-row-not-last");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-10.drops-not-descending");
    }

    [Fact]
    public void Check11_finalRanking_SinglePhase_is_rejected_with_more_than_one_phase()
    {
        var definition = NPhases(2) with { FinalRanking = FinalRankingKind.SinglePhase };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-11.single-phase-final-ranking-with-multiple-phases");
    }

    [Fact]
    public void Check12_finalRanking_is_required_with_more_than_one_phase()
    {
        var definition = NPhases(2);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-12.missing-final-ranking");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-13.minnewgroupsize-with-no-reflight");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-14.normalised-terms-without-normalisation");
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-15.normalisation-without-group");
    }

    // WI-1 (kanban/in-progress/should-level-minima-warn-dont-refuse.md): the
    // hardness datum is a closed enum carrying no ParameterRef, so check 3 has
    // nothing to resolve for it — Shall, Should and absent alike validate
    // clean, on literal and on parameterised minima.

    [Fact]
    public void GroupConstraint_with_Should_hardness_on_a_literal_minimum_validates_clean()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Group = new GroupConstraint { MinPerGroup = 6, MinEnforcement = MinEnforcement.Should },
        };
        definition = WithSingleTask(definition, task);

        ClassDefinitionValidation.Validate(definition).Should().BeEmpty();
    }

    [Fact]
    public void GroupConstraint_with_Should_hardness_on_a_parameterised_minimum_validates_clean()
    {
        var definition = Minimal() with
        {
            Parameters = [new Parameter { Name = "minPerGroup", Kind = MeasuredKind.Number }],
        };
        var task = definition.Phases[0].Tasks[0] with
        {
            Group = new GroupConstraint
            {
                MinPerGroup = NumberOrParam.Param("minPerGroup"),
                MinEnforcement = MinEnforcement.Should,
            },
        };
        definition = WithSingleTask(definition, task);

        ClassDefinitionValidation.Validate(definition).Should().BeEmpty();
    }

    [Fact]
    public void GroupConstraint_with_absent_hardness_validates_clean()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Group = new GroupConstraint { MinPerGroup = 6 },
        };
        definition = WithSingleTask(definition, task);

        ClassDefinitionValidation.Validate(definition).Should().BeEmpty();
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

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-16.exclusion-group-non-deduct-effect");
    }

    // Check 20 (kanban/completed/permitted-scopes-on-penalty-definitions.md#wi-2):
    // a populated permittedScopes is fine, an empty one is provably inert and
    // rejected, an absent one is the unrestricted default needing nothing.

    [Fact]
    public void Check20_populated_permitted_scopes_produces_no_defects()
    {
        var definition = Minimal() with
        {
            Penalties =
            [
                new PenaltyDefinition
                {
                    InfractionType = "test",
                    Effects = [new PenaltyEffectSpec(PenaltyEffect.ZeroFlight)],
                    PermittedScopes = [PenaltyScope.Flight],
                },
            ],
        };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().NotContain(d => d.Code.StartsWith("class-definition.check-20"));
    }

    [Fact]
    public void Check20_empty_permitted_scopes_is_rejected()
    {
        var definition = Minimal() with
        {
            Penalties =
            [
                new PenaltyDefinition
                {
                    InfractionType = "test",
                    Effects = [new PenaltyEffectSpec(PenaltyEffect.ZeroFlight)],
                    PermittedScopes = [],
                },
            ],
        };

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-20.permitted-scopes-empty");
        defect.Path.Should().Be("$.penalties[0].permittedScopes");
    }

    [Fact]
    public void Check20_absent_permitted_scopes_produces_no_defects()
    {
        var definition = Minimal() with
        {
            Penalties =
            [
                new PenaltyDefinition
                {
                    InfractionType = "test",
                    Effects = [new PenaltyEffectSpec(PenaltyEffect.ZeroFlight)],
                },
            ],
        };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().NotContain(d => d.Code.StartsWith("class-definition.check-20"));
    }

    [Fact]
    public void Check17_qualifying_position_source_must_name_an_existing_earlier_phase()
    {
        // SourcePhaseOrdinal is a PhaseDefinition.Ordinal — the definition's
        // own 1-based ordinal vocabulary. On the first phase (Ordinal 1) the
        // rung is unwritable outright: source 0 names no phase of the
        // definition at all, so the minimal single-phase baseline is the
        // smallest violation.
        var definition = Minimal();
        var phase = definition.Phases[0] with
        {
            TieBreaks = [new QualifyingPosition { SourcePhaseOrdinal = 0 }],
        };
        definition = definition with { Phases = [phase] };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-17.qualifying-position-source-not-earlier");

        // A source that names an existing phase but not a strictly lower one
        // — self-reference — is the same defect.
        var selfReferencing = Minimal();
        var selfPhase = selfReferencing.Phases[0] with
        {
            Ordinal = 1,
            TieBreaks = [new QualifyingPosition { SourcePhaseOrdinal = 1 }],
        };
        selfReferencing = selfReferencing with { Phases = [selfPhase] };

        ClassDefinitionValidation.Validate(selfReferencing)
            .Should().ContainSingle().Which.Code.Should().Be("class-definition.check-17.qualifying-position-source-not-earlier");
    }

    [Fact]
    public void Check18_undefinedRequiresRuling_must_be_the_only_rung_in_a_ladder()
    {
        // A non-BestDroppedScore stated rung so check 19 does not also fire.
        var definition = Minimal();
        var phase = definition.Phases[0] with
        {
            TieBreaks = [new AdditionalFullRound(), new UndefinedRequiresRuling()],
        };
        definition = definition with { Phases = [phase] };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-18.undefined-requires-ruling-mixed-with-stated");
    }

    [Fact]
    public void Check21_equalPlaces_must_be_the_only_rung_in_a_ladder()
    {
        // Pete's 2026-09-04 NZ ruling: the stated settlement stands alone —
        // any rung beside it could separate what it settles.
        var definition = Minimal();
        var phase = definition.Phases[0] with
        {
            TieBreaks = [new EqualPlaces(), new TieBreakFlyoff()],
        };
        definition = definition with { Phases = [phase] };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-21.equal-places-mixed-with-stated");
    }

    [Fact]
    public void Check19_bestDroppedScore_requires_a_declared_drop_policy()
    {
        // Minimal()'s phase declares no Drops, so stating bestDroppedScore alone
        // is the smallest violation.
        var definition = Minimal();
        var phase = definition.Phases[0] with
        {
            TieBreaks = [new BestDroppedScore()],
        };
        definition = definition with { Phases = [phase] };

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().ContainSingle().Which.Code.Should().Be("class-definition.check-19.best-dropped-score-without-drop-policy");
    }

    // Check 22 (kanban/in-progress/metric-absence-semantics.md#wi-2): a
    // whenNotRecorded assumption carries the metric's own kind — Flag/Flag,
    // Number/Number. Kind is the only restriction: an assumption on a
    // reporting-only metric is harmless.

    [Fact]
    public void Check22_number_assumption_on_a_flag_metric_is_rejected()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "touchedByCompetitor", Kind = MeasuredKind.Flag, WhenNotRecorded = MeasuredValue.Of(0m) },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-22.when-not-recorded-kind-mismatch");
        defect.Path.Should().Be("$.phases[0].tasks[0].metrics[1].whenNotRecorded");
    }

    [Fact]
    public void Check22_flag_assumption_on_a_number_metric_is_rejected()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s", WhenNotRecorded = MeasuredValue.Of(false) },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-22.when-not-recorded-kind-mismatch");
        defect.Path.Should().Be("$.phases[0].tasks[0].metrics[0].whenNotRecorded");
    }

    [Fact]
    public void Check22_kind_matched_assumptions_produce_no_defects()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s", WhenNotRecorded = MeasuredValue.Of(0m) },
                new MetricDefinition { Name = "touchedByCompetitor", Kind = MeasuredKind.Flag, WhenNotRecorded = MeasuredValue.Of(false) },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Should().NotContain(d => d.Code.StartsWith("class-definition.check-22"));
        defects.Should().BeEmpty();
    }

    [Fact]
    public void Check22_metric_without_an_assumption_produces_no_defect()
    {
        // Minimal()'s flightTime declares no whenNotRecorded — check 22 has
        // nothing to say about a metric with no assumption.
        var defects = ClassDefinitionValidation.Validate(Minimal());

        defects.Should().NotContain(d => d.Code.StartsWith("class-definition.check-22"));
    }

    [Fact]
    public void Check22_assumption_on_a_reporting_only_metric_is_accepted()
    {
        // touchedByCompetitor is read by no term and no predicate — an
        // assumption on it is harmless; kind is the only adoption restriction
        // (the story's decision, not a licence for more checks).
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "touchedByCompetitor", Kind = MeasuredKind.Flag, WhenNotRecorded = MeasuredValue.Of(false) },
            ],
        };
        definition = WithSingleTask(definition, task);

        ClassDefinitionValidation.Validate(definition).Should().BeEmpty();
    }

    [Fact]
    public void Check22_runs_alongside_the_other_checks()
    {
        // Total and non-throwing: the kind mismatch is returned together with
        // the unrelated unresolved-metric-ref defect, not instead of it.
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Score = [new RateTerm { MetricRef = "bogus", Rate = 1 }],
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s", WhenNotRecorded = MeasuredValue.Of(false) },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        defects.Select(d => d.Code).Should().BeEquivalentTo(
            "class-definition.check-1.unresolved-metric-ref",
            "class-definition.check-22.when-not-recorded-kind-mismatch");
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

    // Checks 23–24 and the check-1 extension (kanban/in-progress/
    // add-recorded-predicate.md#wi-2): one negative fixture per numbered check,
    // each built from the minimal baseline, mutated to break exactly the one
    // construct that check guards — Minimal()'s declared flightTime keeps every
    // other check quiet.

    [Fact]
    public void Check1_IsRecorded_over_an_undeclared_metric_is_refused_at_its_metricRef()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            FlightValidWhen = new IsRecorded { MetricRef = "bogus" },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-1.unresolved-metric-ref");
        defect.Path.Should().Be("$.phases[0].tasks[0].flightValidWhen.metricRef");
    }

    [Fact]
    public void Check1_IsRecorded_over_an_undeclared_metric_is_refused_nested_in_the_gate()
    {
        // The comparison beside it resolves, so the only defect is the
        // recordedness one — nested paths still carry the flightValidWhen
        // prefix (check 23's legality does not exempt check 1).
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            FlightValidWhen = new AllOf
            {
                Children =
                [
                    new IsRecorded { MetricRef = "bogus" },
                    new Comparison { LeftMetricRef = "flightTime", Op = Comparator.GreaterThan, RightValue = MeasuredValue.Of(0m) },
                ],
            },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-1.unresolved-metric-ref");
        defect.Path.Should().Be("$.phases[0].tasks[0].flightValidWhen.children[0].metricRef");
    }

    [Fact]
    public void Check23_IsRecorded_in_validWhen_is_refused()
    {
        // Settled decision 2: flightValidWhen-only (v1) — the validWhen site
        // evaluates per-term against selected flights and could reach an absent
        // metric, which a Comparison treats as a throw, never a false.
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            ValidWhen = new IsRecorded { MetricRef = "flightTime" },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-23.is-recorded-outside-flight-gate");
        defect.Path.Should().Be("$.phases[0].tasks[0].validWhen");
    }

    [Fact]
    public void Check23_IsRecorded_in_a_conditional_term_s_when_is_refused()
    {
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Score =
            [
                new RateTerm { MetricRef = "flightTime", Rate = 1 },
                new ConditionalTerm
                {
                    When = new IsRecorded { MetricRef = "flightTime" },
                    Then = new ConstantTerm { Value = 1 },
                },
            ],
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-23.is-recorded-outside-flight-gate");
        defect.Path.Should().Be("$.phases[0].tasks[0].score[1].when");
    }

    [Fact]
    public void Check23_IsRecorded_at_the_flight_gate_top_level_and_nested_in_its_AllOf_validate_clean()
    {
        // The gate is the one site whose evaluation is zero-or-interpret,
        // never a throw — the interpreter's gate decision — and nesting inside
        // its AllOf tree is legal because the children's paths keep the
        // flightValidWhen prefix.
        var topLevel = Minimal();
        var topTask = topLevel.Phases[0].Tasks[0] with
        {
            FlightValidWhen = new IsRecorded { MetricRef = "flightTime" },
        };
        topLevel = WithSingleTask(topLevel, topTask);
        ClassDefinitionValidation.Validate(topLevel).Should().BeEmpty();

        var nested = Minimal();
        var nestedTask = nested.Phases[0].Tasks[0] with
        {
            FlightValidWhen = new AllOf
            {
                Children =
                [
                    new IsRecorded { MetricRef = "flightTime" },
                    new Comparison { LeftMetricRef = "flightTime", Op = Comparator.GreaterThan, RightValue = MeasuredValue.Of(0m) },
                ],
            },
        };
        nested = WithSingleTask(nested, nestedTask);
        ClassDefinitionValidation.Validate(nested).Should().BeEmpty();
    }

    [Fact]
    public void Check24_whenNotRecorded_on_an_IsRecorded_referenced_metric_is_refused()
    {
        // Settled decision 5: "absence resolves to a value" and "absence fails
        // the flight" cannot both hold for one metric — the self-contradiction
        // whose refusal keeps the evaluator a plain dictionary lookup. The
        // assumption is kind-matched (check 22) so it fires alone.
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "startHeight", Kind = MeasuredKind.Number, Unit = "m", WhenNotRecorded = MeasuredValue.Of(0m) },
            ],
            FlightValidWhen = new IsRecorded { MetricRef = "startHeight" },
        };
        definition = WithSingleTask(definition, task);

        var defects = ClassDefinitionValidation.Validate(definition);

        var defect = defects.Should().ContainSingle().Which;
        defect.Code.Should().Be("class-definition.check-24.assumption-on-is-recorded-metric");
        defect.Path.Should().Be("$.phases[0].tasks[0].metrics[1].whenNotRecorded");
    }

    [Fact]
    public void Check24_whenNotRecorded_on_an_unrelated_metric_is_accepted()
    {
        // The walk is over the IsRecorded-referenced set: an assumption on a
        // metric no recordedness predicate reads is the check-22 world's
        // ordinary assumption, untouched by check 24.
        var definition = Minimal();
        var task = definition.Phases[0].Tasks[0] with
        {
            Metrics =
            [
                new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" },
                new MetricDefinition { Name = "startHeight", Kind = MeasuredKind.Number, Unit = "m" },
                new MetricDefinition { Name = "touchedByCompetitor", Kind = MeasuredKind.Flag, WhenNotRecorded = MeasuredValue.Of(false) },
            ],
            FlightValidWhen = new IsRecorded { MetricRef = "startHeight" },
        };
        definition = WithSingleTask(definition, task);

        ClassDefinitionValidation.Validate(definition).Should().BeEmpty();
    }

}
