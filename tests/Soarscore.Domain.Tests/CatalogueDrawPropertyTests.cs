// Property tests — kanban/in-progress/catalogue-choice-draws-plan.md WI-3.
// CsCheck, in PhaseDrawPropertyTests / BindParameterPropertyTests's style:
// drives PhaseDraw.BuildGroups and Competition.DrawPhase directly, domain
// level — no FakeEventStore, no command handler.

using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class CatalogueDrawPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 9, 0, 0, TimeSpan.Zero);

    private static ImmutableArray<CompetitorId> Field(int size) =>
        Enumerable.Range(0, size).Select(_ => CompetitorId.New()).ToImmutableArray();

    private static Competition CompetitionAdopting(ClassDefinition definition, int competitorCount)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Catalogue Draw Test Comp", "Nowhere",
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

    /// <summary>
    /// A ChooseFromCatalogue-shaped fixture built off SeedF3J's single
    /// FixedSequence task, cloned into <paramref name="taskCount"/> distinct
    /// catalogue entries T0..T(n-1) all sharing the same literal MinPerGroup,
    /// so properties 1/2/5 below exercise selection and distinctness in
    /// isolation from group-shape variation — that is property 3's job,
    /// tested directly against PhaseDraw.BuildGroups.
    /// </summary>
    private static ClassDefinition CatalogueFixture(int taskCount, bool requireDistinct, int minPerGroup)
    {
        var baseDefinition = SeedF3J.Definition;
        var baseTask = baseDefinition.Phases[0].Tasks[0];
        var tasks = Enumerable.Range(0, taskCount)
            .Select(i => baseTask with { Code = $"T{i}", Group = new GroupConstraint { MinPerGroup = minPerGroup } })
            .ToImmutableArray();
        var phase = baseDefinition.Phases[0] with
        {
            Tasks = tasks,
            Rounds = new RoundComposition
            {
                Kind = CompositionKind.ChooseFromCatalogue,
                TasksPerRound = 1,
                RequireDistinctTaskPerRound = requireDistinct,
            },
        };
        return baseDefinition with { Phases = [phase] };
    }

    // ------------------------------------------- property 1: selection round-trips

    private static readonly Gen<(int TaskCount, int Rounds, int Field, ImmutableArray<string> Selection)> RoundTripInput =
        from taskCount in Gen.Int[1, 6]
        from rounds in Gen.Int[1, 8]
        from field in Gen.Int[6, 14]
        from picks in Gen.Int[0, taskCount - 1].Array[rounds, rounds]
        select (taskCount, rounds, field, picks.Select(i => $"T{i}").ToImmutableArray());

    [Fact]
    public void DrawPhase_selection_round_trips_into_the_drawn_phase_in_round_order()
    {
        RoundTripInput.Sample(t =>
        {
            var definition = CatalogueFixture(t.TaskCount, requireDistinct: false, minPerGroup: 4);
            var competition = CompetitionAdopting(definition, t.Field);

            var result = competition.DrawPhase(t.Rounds, t.Selection, Now);

            result.IsSuccess.Should().BeTrue(result.Code);
            result.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal(t.Selection);
        });
    }

    // ------------------------------------------- property 2: distinctness both ways

    private static readonly Gen<(int TaskCount, int Rounds, int Field)> DistinctInput =
        from taskCount in Gen.Int[2, 8]
        from rounds in Gen.Int[1, taskCount]
        from field in Gen.Int[6, 14]
        select (taskCount, rounds, field);

    [Fact]
    public void DrawPhase_with_RequireDistinctTaskPerRound_accepts_every_distinct_selection()
    {
        DistinctInput.Sample(t =>
        {
            var definition = CatalogueFixture(t.TaskCount, requireDistinct: true, minPerGroup: 4);
            var competition = CompetitionAdopting(definition, t.Field);
            var selection = Enumerable.Range(0, t.Rounds).Select(i => $"T{i}").ToImmutableArray();

            var result = competition.DrawPhase(t.Rounds, selection, Now);

            result.IsSuccess.Should().BeTrue(result.Code);
            result.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef).Distinct().Count().Should().Be(t.Rounds);
        });
    }

    private static readonly Gen<(int TaskCount, int Field)> RepeatInput =
        from taskCount in Gen.Int[2, 8]
        from field in Gen.Int[6, 14]
        select (taskCount, field);

    [Fact]
    public void DrawPhase_with_RequireDistinctTaskPerRound_refuses_a_selection_with_a_repeat()
    {
        RepeatInput.Sample(t =>
        {
            var definition = CatalogueFixture(t.TaskCount, requireDistinct: true, minPerGroup: 4);
            var competition = CompetitionAdopting(definition, t.Field);
            // Every catalogue code once, plus T0 again — a genuine repeat,
            // not the pigeonhole case (rounds == taskCount + 1 <= catalogue
            // size + 1, so the count/contents checks do not also fire).
            var selection = Enumerable.Range(0, t.TaskCount).Select(i => $"T{i}").Append("T0").ToImmutableArray();

            var result = competition.DrawPhase(selection.Length, selection, Now);

            result.IsFailure.Should().BeTrue();
            result.Code.Should().Be("drawPhase.taskSelectionNotDistinct");
        });
    }

    [Fact]
    public void DrawPhase_without_RequireDistinctTaskPerRound_accepts_a_repeated_task()
    {
        RepeatInput.Sample(t =>
        {
            var definition = CatalogueFixture(t.TaskCount, requireDistinct: false, minPerGroup: 4);
            var competition = CompetitionAdopting(definition, t.Field);
            var selection = Enumerable.Range(0, t.TaskCount).Select(i => $"T{i}").Append("T0").ToImmutableArray();

            var result = competition.DrawPhase(selection.Length, selection, Now);

            result.IsSuccess.Should().BeTrue(result.Code);
        });
    }

    // ------------------------------------------- property 3: field partition invariant, heterogeneous sizing

    private static readonly Gen<(int Field, ImmutableArray<int> MinPerGroupByRound)> HeterogeneousInput =
        from field in Gen.Int[6, 20]
        from rounds in Gen.Int[1, 5]
        from sizes in Gen.Int[2, Math.Max(2, field / 2)].Array[rounds, rounds]
        select (field, sizes.ToImmutableArray());

    [Fact]
    public void BuildGroups_per_round_sizing_places_every_competitor_exactly_once_with_the_right_group_count()
    {
        HeterogeneousInput.Sample(t =>
        {
            var field = Field(t.Field);

            var rounds = PhaseDraw.BuildGroups(field, t.MinPerGroupByRound);

            rounds.Length.Should().Be(t.MinPerGroupByRound.Length);

            for (var i = 0; i < rounds.Length; i++)
            {
                var minPerGroup = t.MinPerGroupByRound[i];
                var expectedGroupCount = Math.Max(1, t.Field / minPerGroup);
                var groups = rounds[i];

                groups.Length.Should().Be(expectedGroupCount);

                var placed = groups.SelectMany(g => g).ToArray();
                placed.Length.Should().Be(t.Field);
                placed.Distinct().Count().Should().Be(t.Field);
                placed.Should().BeEquivalentTo(field);

                groups.Should().OnlyContain(g => g.Length >= minPerGroup);
            }
        });
    }

    // ------------------------------------------- property 4: pairing fairness not degraded

    private static readonly Gen<(int Field, int MinPerGroup, int Rounds)> UniformInput =
        from field in Gen.Int[4, 16]
        from minPerGroup in Gen.Int[2, Math.Max(2, field / 2)]
        from rounds in Gen.Int[1, 5]
        select (field, minPerGroup, rounds);

    [Fact]
    public void BuildGroups_with_uniform_sizing_matches_the_two_argument_overload_byte_for_byte()
    {
        UniformInput.Sample(t =>
        {
            var field = Field(t.Field);

            var viaTwoArgOverload = PhaseDraw.BuildGroups(field, t.MinPerGroup, t.Rounds);
            var viaPerRoundArray = PhaseDraw.BuildGroups(field, [.. Enumerable.Repeat(t.MinPerGroup, t.Rounds)]);

            viaPerRoundArray.Should().BeEquivalentTo(viaTwoArgOverload, o => o.WithStrictOrdering());
        });
    }

    // Small-scale brute-force oracle, generalising PhaseDrawPropertyTests's
    // single-size version to a per-round sizes array — small field/round
    // counts only, the search space is combinatorial in both group count
    // (per round) and round count.
    private static readonly Gen<(int Field, ImmutableArray<int> Sizes)> FairnessHeterogeneousInput =
        from field in Gen.Int[4, 6]
        from rounds in Gen.Int[1, 2]
        from sizes in Gen.Int[2, Math.Max(2, field / 2)].Array[rounds, rounds]
        select (field, sizes.ToImmutableArray());

    [Fact]
    public void BuildGroups_maximum_pairwise_co_occurrence_with_varying_group_shape_matches_the_brute_force_minimum()
    {
        FairnessHeterogeneousInput.Sample(
            t =>
            {
                var field = Field(t.Field);

                var actualRounds = PhaseDraw.BuildGroups(field, t.Sizes);
                var actualMax = MaxPairwise(actualRounds);

                var trueMinimum = TrueMinimumMaxPairwise(field, t.Sizes, actualMax);

                actualMax.Should().Be(trueMinimum);
            },
            iter: 30);
    }

    // ------------------------------------------- property 5: determinism

    [Fact]
    public void DrawPhase_is_deterministic_for_the_same_field_selection_and_bindings()
    {
        RoundTripInput.Sample(t =>
        {
            var definition = CatalogueFixture(t.TaskCount, requireDistinct: false, minPerGroup: 4);

            // Same field ids fed to two independent draws — CompetitorId is
            // pre-minted here (unlike CompetitionAdopting, which mints its
            // own), which is what fixes the field identity across the two.
            var field = Field(t.Field);

            var first = DrawWithField(definition, field, t.Rounds, t.Selection);
            var second = DrawWithField(definition, field, t.Rounds, t.Selection);

            first.IsSuccess.Should().BeTrue(first.Code);
            second.IsSuccess.Should().BeTrue(second.Code);

            first.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef)
                .Should().Equal(second.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef));

            for (var i = 0; i < first.Value.Rounds.Length; i++)
            {
                var a = first.Value.Rounds[i].TaskRounds[0].Groups.Select(g => g.CompetitorRefs);
                var b = second.Value.Rounds[i].TaskRounds[0].Groups.Select(g => g.CompetitorRefs);
                a.Should().BeEquivalentTo(b, o => o.WithStrictOrdering());
            }
        });
    }

    private static Result<PhaseDrawn> DrawWithField(
        ClassDefinition definition, ImmutableArray<CompetitorId> field, int rounds, ImmutableArray<string> selection)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Determinism Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        foreach (var id in field)
        {
            var registered = competition.RegisterCompetitor(id, PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        return competition.DrawPhase(rounds, selection, Now);
    }

    // ------------------------------------------- property 6: corpus-generic

    private static readonly string SeedJsonDirectory = FindSeedJsonDirectory();

    private static string FindSeedJsonDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find the repository root from the test's base directory.");
        }

        return Path.Combine(directory.FullName, "tools", "Soarscore.SeedData", "json");
    }

    [Fact]
    public void Every_catalogue_choice_class_in_the_corpus_is_drawable_given_a_valid_selection()
    {
        var files = Directory.GetFiles(SeedJsonDirectory, "*.json");
        files.Should().NotBeEmpty("the seed corpus JSON must have already been emitted by the seed tool");

        var catalogueClasses = files
            .Select(f => (FileName: Path.GetFileName(f), Definition: JsonSerializer.Deserialize<ClassDefinition>(File.ReadAllText(f), SoarscoreJson.Ingestion)!))
            .Where(x => x.Definition.Phases[0].Rounds.Kind == CompositionKind.ChooseFromCatalogue
                        && x.Definition.Phases[0].Rounds.TasksPerRound == 1)
            .ToImmutableArray();

        // A sanity floor, not a hard-coded name list: today's corpus has
        // exactly F3K and F5K in this shape. A future catalogue-choice class
        // should be picked up here without editing this test — only this
        // assertion would need raising.
        catalogueClasses.Length.Should().Be(2);

        foreach (var (fileName, definition) in catalogueClasses)
        {
            DrawCatalogueClass(fileName, definition);
        }
    }

    private static void DrawCatalogueClass(string fileName, ClassDefinition definition)
    {
        const int fieldSize = 20; // NFR-3's field ceiling — generous enough for any declared literal MinPerGroup in the corpus.

        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), $"Catalogue Corpus Test — {fileName}", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < fieldSize; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            competition = competition.Apply(registered.Value);
        }

        // Generic, not class-specific: bind every declared parameter with no
        // default so the draw never hits drawPhase.parameterUnbound
        // regardless of which parameter its first phase's tasks resolve.
        foreach (var parameter in definition.Parameters.Where(p => p.DefaultValue is null))
        {
            var value = parameter.AllowedValues.Length > 0
                ? parameter.AllowedValues[0]
                : parameter.Kind == MeasuredKind.Number ? MeasuredValue.Of(5m) : MeasuredValue.Of(false);

            var bound = competition.BindParameter(parameter.Name, value, "cd", Now);
            bound.IsSuccess.Should().BeTrue($"{fileName}/{parameter.Name}: {bound.Code}");
            competition = competition.Apply(bound.Value);
        }

        var phase = definition.Phases[0];
        var rounds = Math.Min(5, phase.Tasks.Length);
        var selection = phase.Tasks.Take(rounds).Select(t => t.Code).ToImmutableArray();

        var drawn = competition.DrawPhase(rounds, selection, Now);

        drawn.IsSuccess.Should().BeTrue($"{fileName}: {drawn.Code}");
        drawn.Value.Rounds.Select(r => r.TaskRounds[0].TaskRef).Should().Equal(selection);
    }

    // ------------------------------------------- oracle helpers (property 4)

    private static int MaxPairwise(ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>> rounds)
    {
        var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();
        var max = 0;
        foreach (var groups in rounds)
        {
            foreach (var group in groups)
            {
                for (var i = 0; i < group.Length; i++)
                {
                    for (var j = i + 1; j < group.Length; j++)
                    {
                        var key = PairKey(group[i], group[j]);
                        var count = pairCount.GetValueOrDefault(key) + 1;
                        pairCount[key] = count;
                        if (count > max)
                        {
                            max = count;
                        }
                    }
                }
            }
        }

        return max;
    }

    /// <summary>
    /// Exhaustive search over every valid partition-per-round for each
    /// round's own group-size shape, using PhaseDraw's own group-count/size
    /// formula per round, returning the true minimum achievable maximum
    /// pairwise count. Branch-and-bound: <paramref name="incumbent"/> seeds
    /// the bound with BuildGroups's own (already-valid) result.
    /// </summary>
    private static int TrueMinimumMaxPairwise(
        ImmutableArray<CompetitorId> field, ImmutableArray<int> minPerGroupByRound, int incumbent)
    {
        var fieldIndex = field
            .Select((c, i) => (c, i))
            .ToDictionary(x => x.c, x => x.i);

        var partitionsByRound = minPerGroupByRound
            .Select(minPerGroup =>
            {
                var groupCount = Math.Max(1, field.Length / minPerGroup);
                var sizes = GroupSizes(field.Length, groupCount);
                return AllPartitions(field, sizes, fieldIndex, -1).ToImmutableArray();
            })
            .ToImmutableArray();

        var pairCount = new Dictionary<(CompetitorId, CompetitorId), int>();

        return Search(partitionsByRound, 0, pairCount, 0, incumbent);
    }

    private static int Search(
        ImmutableArray<ImmutableArray<ImmutableArray<ImmutableArray<CompetitorId>>>> partitionsByRound,
        int roundIndex,
        Dictionary<(CompetitorId, CompetitorId), int> pairCount,
        int currentMax,
        int best)
    {
        if (roundIndex == partitionsByRound.Length)
        {
            return Math.Min(best, currentMax);
        }

        foreach (var partition in partitionsByRound[roundIndex])
        {
            var roundMax = currentMax;
            var deltas = new List<((CompetitorId, CompetitorId) Key, int OldValue)>();

            foreach (var group in partition)
            {
                for (var i = 0; i < group.Length; i++)
                {
                    for (var j = i + 1; j < group.Length; j++)
                    {
                        var key = PairKey(group[i], group[j]);
                        var oldValue = pairCount.GetValueOrDefault(key);
                        deltas.Add((key, oldValue));
                        pairCount[key] = oldValue + 1;
                        if (oldValue + 1 > roundMax)
                        {
                            roundMax = oldValue + 1;
                        }
                    }
                }
            }

            // Pairwise counts only ever grow across further rounds, so a
            // partial max already at or past the incumbent can never
            // produce a strictly better complete assignment — prune.
            if (roundMax < best)
            {
                best = Search(partitionsByRound, roundIndex + 1, pairCount, roundMax, best);
            }

            foreach (var (key, oldValue) in deltas)
            {
                if (oldValue == 0)
                {
                    pairCount.Remove(key);
                }
                else
                {
                    pairCount[key] = oldValue;
                }
            }
        }

        return best;
    }

    /// <summary>Mirrors PhaseDraw's own private group-sizing formula, so the oracle's partitions have the same shape BuildGroups produces.</summary>
    private static ImmutableArray<int> GroupSizes(int fieldSize, int groupCount)
    {
        var baseSize = fieldSize / groupCount;
        var remainder = fieldSize % groupCount;

        var builder = ImmutableArray.CreateBuilder<int>(groupCount);
        for (var g = 0; g < groupCount; g++)
        {
            builder.Add(g < remainder ? baseSize + 1 : baseSize);
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Every way to partition <paramref name="remaining"/> into ordered slots
    /// of exactly <paramref name="sizes"/> (sizes non-increasing, as
    /// GroupSizes produces). Canonicalised against re-deriving the same
    /// unordered partition once per permutation of equal-size slots: within a
    /// run of equal sizes, each slot's chosen combination must have a
    /// strictly greater minimum field-index than the previous slot's —
    /// <paramref name="minIndexFloor"/> carries that constraint down the
    /// recursion.
    /// </summary>
    private static IEnumerable<ImmutableArray<ImmutableArray<CompetitorId>>> AllPartitions(
        ImmutableArray<CompetitorId> remaining,
        ImmutableArray<int> sizes,
        Dictionary<CompetitorId, int> fieldIndex,
        int minIndexFloor)
    {
        if (sizes.IsEmpty)
        {
            yield return ImmutableArray<ImmutableArray<CompetitorId>>.Empty;
            yield break;
        }

        var size = sizes[0];
        var nextIsSameSize = sizes.Length > 1 && sizes[1] == size;

        foreach (var combo in Combinations(remaining, size))
        {
            var minIndex = combo.Min(c => fieldIndex[c]);
            if (minIndex <= minIndexFloor)
            {
                continue;
            }

            var rest = remaining.Where(c => !combo.Contains(c)).ToImmutableArray();
            var childFloor = nextIsSameSize ? minIndex : -1;

            foreach (var tail in AllPartitions(rest, sizes.RemoveAt(0), fieldIndex, childFloor))
            {
                yield return tail.Insert(0, combo);
            }
        }
    }

    private static IEnumerable<ImmutableArray<CompetitorId>> Combinations(ImmutableArray<CompetitorId> items, int k)
    {
        if (k == 0)
        {
            yield return ImmutableArray<CompetitorId>.Empty;
            yield break;
        }

        if (items.Length < k)
        {
            yield break;
        }

        var first = items[0];
        var rest = items.RemoveAt(0);

        foreach (var combo in Combinations(rest, k - 1))
        {
            yield return combo.Insert(0, first);
        }

        foreach (var combo in Combinations(rest, k))
        {
            yield return combo;
        }
    }

    private static (CompetitorId, CompetitorId) PairKey(CompetitorId a, CompetitorId b) =>
        a.Value.CompareTo(b.Value) <= 0 ? (a, b) : (b, a);
}
