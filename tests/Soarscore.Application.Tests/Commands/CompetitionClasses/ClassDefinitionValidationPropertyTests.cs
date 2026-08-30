// kanban/completed/class-definition-adoption-steel-thread-plan.md WI-2's own
// "Verify" section proposes this: "generate a valid definition (a corpus
// definition plus one random mutation drawn from a table mapping 'this
// mutation' -> 'should trip check N'), assert the *only* defect raised is
// check N." ClassDefinitionValidationTests.cs's nineteen fixed examples prove
// each check CAN fire; they cannot prove a check fires ONLY on its own
// trigger and never masks or duplicates an adjacent one — the checks share
// traversal helpers (AllTasks, AllTermsOf, WalkPredicate), so a bug in a
// shared helper is exactly the kind of thing fixed examples can't surface but
// randomising each mutation's concrete values (not just its shape) can.
//
// Checks 4, 5 and 6 have no case below, for the same reason
// ClassDefinitionValidation.cs's own header gives them no real check method:
// check 4 is unrepresentable by construction, and 5/6 guard a notation
// construct that never survives into a ClassDefinition.
//
// The second property fuzzes Validate() directly: structural mutations
// unrelated to any specific check, composed in random combinations on top of
// a random corpus definition, checking the "total and non-throwing" claim
// ClassDefinitionValidation.cs's header comment makes.

using System.Collections.Immutable;
using CsCheck;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;
using static Soarscore.Application.Tests.Shared.CompetitionClasses.ClassDefinitionFixtures;

namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

public class ClassDefinitionValidationPropertyTests
{
    // ---------------------------------------------------------- mutation table

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check1 =
        Gen.Int[1, 999_999].Select(n =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with
            {
                Score = [new RateTerm { MetricRef = $"bogus{n}", Rate = 1 }],
            };
            return (WithSingleTask(definition, task), "class-definition.check-1.unresolved-metric-ref");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check2 =
        Gen.Int[1, 999_999].Select(n =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with
            {
                Flights = new BestNFlights { Count = 1, RankByMetric = $"bogus{n}" },
            };
            return (WithSingleTask(definition, task), "class-definition.check-2.unresolved-rank-by-metric");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check3 =
        Gen.Int[1, 999_999].Select(n =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with
            {
                Timing = definition.Phases[0].Tasks[0].Timing with { MaxLaunches = NumberOrParam.Param($"undeclared{n}") },
            };
            return (WithSingleTask(definition, task), "class-definition.check-3.unresolved-parameter-ref");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check7 =
        Gen.OneOfConst("m", "kg", "pts", "deg").Select(unit =>
        {
            var definition = Minimal() with { Parameters = [new Parameter { Name = "wt", Unit = unit }] };
            var task = definition.Phases[0].Tasks[0] with
            {
                Timing = definition.Phases[0].Tasks[0].Timing with { WorkingTime = NumberOrParam.Param("wt") },
            };
            return (WithSingleTask(definition, task), "class-definition.check-7.parameter-unit-mismatch");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check8 =
        Gen.Int[1, 999_999].Select(n =>
        {
            var (a, b) = ($"a{n}", $"b{n}");
            // Unit "s" matches Minimal()'s "flightTime" metric so check 7 (parameter
            // unit agreement) does not also fire on these bands' from/to slots —
            // isolates the mutation to check 8 alone, unlike the un-unitted
            // parameters ClassDefinitionValidationTests.cs's Check8 example uses
            // (masked there only because that test asserts ContainSingle(predicate),
            // not that check-8 is the sole defect).
            var definition = Minimal() with { Parameters = [new Parameter { Name = a, Unit = "s" }, new Parameter { Name = b, Unit = "s" }] };
            var task = definition.Phases[0].Tasks[0] with
            {
                Score =
                [
                    new PiecewiseTerm
                    {
                        MetricRef = "flightTime",
                        Bands =
                        [
                            new Band(null, NumberOrParam.Param(a), 1),
                            new Band(NumberOrParam.Param(b), null, -1),
                        ],
                    },
                ],
            };
            return (WithSingleTask(definition, task), "class-definition.check-8.piecewise-bands-do-not-meet");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check9RowsNotAscending =
        (from hi in Gen.Int[10, 1000] from lo in Gen.Int[0, 9] select (hi, lo))
        .Select(t =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with
            {
                Score = [new LookupTerm { MetricRef = "flightTime", Rows = [new LookupRow(t.hi, 10), new LookupRow(t.lo, 20)] }],
            };
            return (WithSingleTask(definition, task), "class-definition.check-9.rows-not-ascending");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check9UnboundedRowNotLast =
        Gen.Int[1, 1000].Select(bound =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with
            {
                Score = [new LookupTerm { MetricRef = "flightTime", Rows = [new LookupRow(null, 10), new LookupRow(bound, 20)] }],
            };
            return (WithSingleTask(definition, task), "class-definition.check-9.unbounded-row-not-last");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check10 =
        (from prev in Gen.Int[3, 10] from deltaCurr in Gen.Int[0, 10] select (prev, curr: prev + deltaCurr))
        .Select(t =>
        {
            var definition = Minimal();
            var phase = definition.Phases[0] with
            {
                Drops =
                [
                    new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1, ApplyWhenRoundsCompletedAtLeast = t.prev },
                    new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1, ApplyWhenRoundsCompletedAtLeast = t.curr },
                ],
            };
            return (definition with { Phases = [phase] }, "class-definition.check-10.drops-not-descending");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check11 =
        Gen.Int[2, 4].Select(n =>
            (NPhases(n) with { FinalRanking = FinalRankingKind.SinglePhase }, "class-definition.check-11.single-phase-final-ranking-with-multiple-phases"));

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check12 =
        Gen.Int[2, 4].Select(n => (NPhases(n), "class-definition.check-12.missing-final-ranking"));

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check13 =
        Gen.Int[1, 50].Select(n =>
        {
            var definition = Minimal() with
            {
                Reflight = new ReflightRule
                {
                    EntitledScores = ReflightSelection.NotPermitted,
                    OthersScore = ReflightSelection.NotPermitted,
                    MinNewGroupSize = n,
                },
            };
            return (definition, "class-definition.check-13.minnewgroupsize-with-no-reflight");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check14 =
        Gen.Int[-100, 100].Select(v =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with { ScoreNormalised = [new ConstantTerm { Value = v }] };
            return (WithSingleTask(definition, task), "class-definition.check-14.normalised-terms-without-normalisation");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check15 =
        (from direction in Gen.OneOfConst(NormalisationDirection.HigherIsBetter, NormalisationDirection.LowerIsBetter)
         from winnerScore in Gen.Int[1, 5000]
         select (direction, winnerScore))
        .Select(t =>
        {
            var definition = Minimal();
            var task = definition.Phases[0].Tasks[0] with { Normalise = new Normalisation { Direction = t.direction, WinnerScore = t.winnerScore } };
            return (WithSingleTask(definition, task), "class-definition.check-15.normalisation-without-group");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> Check16 =
        (from effect in Gen.OneOfConst(PenaltyEffect.ZeroFlight, PenaltyEffect.ZeroRound, PenaltyEffect.ZeroTask, PenaltyEffect.Disqualify)
         from n in Gen.Int[1, 999_999]
         select (effect, n))
        .Select(t =>
        {
            var definition = Minimal() with
            {
                Penalties = [new PenaltyDefinition { InfractionType = $"test{t.n}", ExclusionGroups = [$"g{t.n}"], Effects = [new PenaltyEffectSpec(t.effect)] }],
            };
            return (definition, "class-definition.check-16.exclusion-group-non-deduct-effect");
        });

    private static readonly Gen<(ClassDefinition Definition, string ExpectedCode)> AnyMutation = Gen.OneOf(
        Check1, Check2, Check3, Check7, Check8, Check9RowsNotAscending, Check9UnboundedRowNotLast,
        Check10, Check11, Check12, Check13, Check14, Check15, Check16);

    [Fact]
    public void Each_mutation_trips_only_the_check_it_targets()
    {
        AnyMutation.Sample(t =>
        {
            var defects = ClassDefinitionValidation.Validate(t.Definition);
            return defects.Count > 0 && defects.All(d => d.Code == t.ExpectedCode);
        });
    }

    // -------------------------------------------------- totality / non-throwing

    private static readonly Gen<ClassDefinition> AnyCorpusDefinition =
        Gen.OneOfConst(Corpus.All.Select(c => c.Definition).ToArray());

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> ClearFinalRanking =
        Gen.Const<Func<ClassDefinition, ClassDefinition>>(d => d with { FinalRanking = null });

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> RandomiseFinalRanking =
        Gen.Enum<FinalRankingKind>().Select(k => (Func<ClassDefinition, ClassDefinition>)(d => d with { FinalRanking = k }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> ClearGroupOnFirstTask =
        Gen.Const<Func<ClassDefinition, ClassDefinition>>(MutateFirstTask(t => t with { Group = null }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> ClearNormaliseOnFirstTask =
        Gen.Const<Func<ClassDefinition, ClassDefinition>>(MutateFirstTask(t => t with { Normalise = null }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> ScrambleFirstMetricName =
        Gen.Int[1, 999_999].Select(n => MutateFirstTask(t =>
            t.Metrics.Length == 0 ? t : t with { Metrics = t.Metrics.SetItem(0, t.Metrics[0] with { Name = $"scrambled{n}" }) }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> RandomiseDropGates =
        Gen.Int[0, 10].Array[0, 4].Select(gates => (Func<ClassDefinition, ClassDefinition>)(d =>
        {
            if (d.Phases.Length == 0)
            {
                return d;
            }

            var drops = gates
                .Select(g => new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1, ApplyWhenRoundsCompletedAtLeast = g })
                .ToImmutableArray();
            return d with { Phases = d.Phases.SetItem(0, d.Phases[0] with { Drops = drops }) };
        }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> RandomiseReflightMinNewGroupSize =
        Gen.Int[-5, 20].Select(n => (Func<ClassDefinition, ClassDefinition>)(d => d with { Reflight = d.Reflight with { MinNewGroupSize = n } }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> AddRandomPenalty =
        (from effect in Gen.Enum<PenaltyEffect>() from n in Gen.Int[1, 999_999] select (effect, n))
        .Select(t => (Func<ClassDefinition, ClassDefinition>)(d => d with
        {
            Penalties = d.Penalties.Add(new PenaltyDefinition
            {
                InfractionType = $"fuzz{t.n}",
                ExclusionGroups = [$"g{t.n}"],
                Effects = [new PenaltyEffectSpec(t.effect)],
            }),
        }));

    private static readonly Gen<Func<ClassDefinition, ClassDefinition>> AnyMutator = Gen.OneOf(
        ClearFinalRanking, RandomiseFinalRanking, ClearGroupOnFirstTask, ClearNormaliseOnFirstTask,
        ScrambleFirstMetricName, RandomiseDropGates, RandomiseReflightMinNewGroupSize, AddRandomPenalty);

    [Fact]
    public void Validate_never_throws_and_every_defect_is_well_formed_under_combined_structural_fuzzing()
    {
        (from definition in AnyCorpusDefinition
         from mutators in AnyMutator.Array[0, 4]
         select mutators.Aggregate(definition, (d, m) => m(d)))
        .Sample(mutated =>
        {
            var defects = ClassDefinitionValidation.Validate(mutated);
            return defects.All(d =>
                d.Code.StartsWith("class-definition.check-", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(d.Path)
                && !string.IsNullOrEmpty(d.Message));
        });
    }

    private static Func<ClassDefinition, ClassDefinition> MutateFirstTask(Func<TaskDefinition, TaskDefinition> f) =>
        d =>
        {
            if (d.Phases.Length == 0 || d.Phases[0].Tasks.Length == 0)
            {
                return d;
            }

            var phase = d.Phases[0];
            var task = f(phase.Tasks[0]);
            return d with { Phases = d.Phases.SetItem(0, phase with { Tasks = phase.Tasks.SetItem(0, task) }) };
        };
}
