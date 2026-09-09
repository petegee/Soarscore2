// kanban/in-progress/should-level-minima-warn-dont-refuse.md WI-2.
//
// CsCheck property for the named invariant: "every SHOULD breach in [2, min)
// prescribes + warns naming the group; every shall breach refuses; no group
// < 2 ever prescribes". Generated over hardness x field x partition shape on
// a synthetic definition (min 6, FixedSequence) — the corpus seeds are all
// default-hard until WI-3, so the corpus itself cannot exhibit SHOULD.
//
// PD-P2's non-vacuity discipline applies: the unmutated (valid) partition
// must prescribe cleanly, or a rejection proves nothing.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Domain.Tests;

public class ShouldMinimaWarnPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private const int MinPerGroup = 6;

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number }];

    private enum Shape { Valid, BelowMinimum, Singleton }

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

    private static Competition SampleCompetition(ClassDefinition definition)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "SHOULD Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        return Competition.Create(created);
    }

    private static Competition Registered(ClassDefinition definition, IReadOnlyList<CompetitorId> ids)
    {
        var competition = SampleCompetition(definition);

        foreach (var id in ids)
        {
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
        }

        return competition;
    }

    /// <summary>
    /// An even split of <paramref name="field"/> into the fewest groups the
    /// minimum allows — every group clears the minimum, the valid base every
    /// shape mutates from.
    /// </summary>
    private static List<List<CompetitorId>> BasePartition(ImmutableArray<CompetitorId> field)
    {
        var groupCount = Math.Max(1, field.Length / MinPerGroup);
        var sizes = new List<int>(groupCount);
        var baseSize = field.Length / groupCount;
        var remainder = field.Length % groupCount;
        for (var g = 0; g < groupCount; g++)
        {
            sizes.Add(g < remainder ? baseSize + 1 : baseSize);
        }

        var groups = new List<List<CompetitorId>>();
        var cursor = 0;
        foreach (var size in sizes)
        {
            groups.Add(field.Skip(cursor).Take(size).ToList());
            cursor += size;
        }

        return groups;
    }

    private static PrescribedRound ToRound(List<List<CompetitorId>> groups) =>
        new(null, [.. groups.Select(members => new PrescribedGroup([.. members]))]);

    [Fact]
    public void Every_SHOULD_breach_in_2_to_min_prescribes_with_a_warning_every_shall_breach_refuses_and_no_group_under_2_ever_prescribes()
    {
        // Fields start at 12: against a minimum of 6 that guarantees at
        // least two groups, which BelowMinimum's donor/receiver move needs.
        (from hardness in Gen.OneOfConst(MinEnforcement.Shall, MinEnforcement.Should)
         from fieldSize in Gen.Int[12, 18]
         from shape in Gen.OneOfConst(Shape.Valid, Shape.BelowMinimum, Shape.Singleton)
         from seed in Gen.Int[0, 999]
         select (hardness, fieldSize, shape, seed))
        .Sample(t =>
        {
            var definition = DefinitionWith(
                t.hardness == MinEnforcement.Should ? MinEnforcement.Should : null);
            var ids = Enumerable.Range(0, t.fieldSize).Select(_ => CompetitorId.New()).ToArray();

            var groups = BasePartition([.. ids]);

            // Non-vacuity: the unmutated partition must prescribe cleanly.
            var pristine = Registered(definition, ids);
            var clean = pristine.PrescribeDraw([ToRound(BasePartition([.. ids]))], "property", Now);
            clean.IsSuccess.Should().BeTrue("the base prescription must be legal before mutating it");
            clean.Value.Warnings.Should().BeEmpty();
            clean.Advisories.Should().BeEmpty();

            var donorOrdinal = 0;
            var donorSize = 0;

            switch (t.shape)
            {
                case Shape.BelowMinimum:
                    // Shrink the smallest group to a size in [2, min) —
                    // overflow moves to another group, keeping the partition
                    // exact — so exactly one group breaches.
                    var donorIndex = 0;
                    for (var candidate = 1; candidate < groups.Count; candidate++)
                    {
                        if (groups[candidate].Count < groups[donorIndex].Count)
                        {
                            donorIndex = candidate;
                        }
                    }

                    var receiver = (donorIndex + 1) % groups.Count;
                    donorSize = 2 + (t.seed % (MinPerGroup - 2));
                    while (groups[donorIndex].Count > donorSize)
                    {
                        var moved = groups[donorIndex][^1];
                        groups[donorIndex].RemoveAt(groups[donorIndex].Count - 1);
                        groups[receiver].Add(moved);
                    }

                    donorOrdinal = donorIndex + 1;
                    break;

                case Shape.Singleton:
                    // Singleton goes FIRST so the size checks meet it before
                    // any below-minimum donor — the code under test is
                    // groupTooSmall on both hardnesses.
                    var loner = groups[0][0];
                    groups[0].RemoveAt(0);
                    groups.Insert(0, [loner]);
                    break;
            }

            var target = Registered(definition, ids);
            var result = target.PrescribeDraw([ToRound(groups)], "property", Now);

            switch (t.shape)
            {
                case Shape.Valid:
                    result.IsSuccess.Should().BeTrue("a partition clearing the minimum prescribes");
                    result.Value.Warnings.Should().BeEmpty();
                    result.Advisories.Should().BeEmpty();
                    break;

                case Shape.BelowMinimum when t.hardness == MinEnforcement.Should:
                    result.IsSuccess.Should().BeTrue("a SHOULD breach prescribes");
                    var warnings = result.Value.Warnings!;
                    warnings.Count.Should().Be(1);
                    warnings[0].Code.Should().Be("prescribeDraw.groupBelowClassMinimum");
                    warnings[0].Message.Should().Contain($"group {donorOrdinal}");
                    warnings[0].Message.Should().Contain($"{donorSize} member(s)");
                    warnings[0].Message.Should().Contain($"({MinPerGroup})");
                    result.Advisories.Should().Equal(warnings);

                    // The invariant's third clause, on the success side: no
                    // prescribed group is under 2.
                    result.Value.Rounds.Single().TaskRounds[0].Groups
                        .Should().OnlyContain(g => g.CompetitorRefs.Length >= 2);
                    break;

                case Shape.BelowMinimum:
                    result.IsFailure.Should().BeTrue("a shall breach refuses");
                    result.Code.Should().Be("prescribeDraw.groupBelowClassMinimum");
                    result.Advisories.Should().BeEmpty();
                    break;

                case Shape.Singleton:
                    result.IsFailure.Should().BeTrue("no group under 2 ever prescribes");
                    result.Code.Should().Be("prescribeDraw.groupTooSmall");
                    result.Advisories.Should().BeEmpty();
                    break;
            }
        });
    }

    [Fact]
    public void DrawPhase_below_the_minimum_warns_through_on_SHOULD_and_refuses_on_shall()
    {
        (from hardness in Gen.OneOfConst(MinEnforcement.Shall, MinEnforcement.Should)
         from fieldSize in Gen.Int[2, 10]
         select (hardness, fieldSize))
        .Sample(t =>
        {
            var definition = DefinitionWith(
                t.hardness == MinEnforcement.Should ? MinEnforcement.Should : null);
            var ids = Enumerable.Range(0, t.fieldSize).Select(_ => CompetitorId.New()).ToArray();
            var competition = Registered(definition, ids);

            var result = competition.DrawPhase(1, [], Now);

            if (t.fieldSize < MinPerGroup && t.hardness == MinEnforcement.Should)
            {
                result.IsSuccess.Should().BeTrue("a SHOULD breach draws with a warning");
                var warnings = result.Value.Warnings!;
                warnings.Count.Should().Be(1);
                warnings[0].Code.Should().Be("drawPhase.fieldTooSmall");
                warnings[0].Message.Should().Contain($"eligible field ({t.fieldSize})");
                warnings[0].Message.Should().Contain($"({MinPerGroup})");
                result.Advisories.Should().Equal(warnings);

                // The generator is untouched: one whole-field group.
                result.Value.Rounds.Single().TaskRounds[0].Groups.Length.Should().Be(1);
            }
            else if (t.fieldSize < MinPerGroup)
            {
                result.IsFailure.Should().BeTrue("a shall breach refuses");
                result.Code.Should().Be("drawPhase.fieldTooSmall");
                result.Advisories.Should().BeEmpty();
            }
            else
            {
                result.IsSuccess.Should().BeTrue("a field clearing the minimum draws cleanly");
                result.Value.Warnings.Should().BeEmpty();
                result.Advisories.Should().BeEmpty();
            }
        });
    }
}
