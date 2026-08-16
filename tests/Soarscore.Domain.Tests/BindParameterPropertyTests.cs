// Property tests — kanban/completed/bind-parameter-steel-thread-plan.md WI-3.
// CsCheck, in PhaseDrawPropertyTests / PhaseDrawnDecideTests's style: drives
// Competition.BindParameter / Competition.DrawPhase / ParameterResolver
// directly, domain-level — no FakeEventStore, no command handler.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class BindParameterPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

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
            CompetitionId.New(), "Bind Parameter Test Comp", "Nowhere",
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
    /// F3J's first phase/task with Group swapped to an unbound, DECLARED Param
    /// — the same F5K-shaped isolation PhaseDrawnDecideTests.WithUnboundMinPerGroup
    /// uses, extended to also add the Parameter to the definition (BindParameter's
    /// ValidateParameterDeclared checks the declared list; DrawPhase never needed
    /// to, so the decide-function fixture did not need it either).
    /// </summary>
    private static ClassDefinition WithParameterisedMinPerGroup(ClassDefinition definition, string parameterName)
    {
        var phase = definition.Phases[0];
        var task = phase.Tasks[0] with { Group = new GroupConstraint { MinPerGroup = NumberOrParam.Param(parameterName) } };
        var parameters = definition.Parameters.Any(p => p.Name == parameterName)
            ? definition.Parameters
            : definition.Parameters.Add(new Parameter { Name = parameterName, Kind = MeasuredKind.Number });
        return definition with { Phases = [phase with { Tasks = [task] }], Parameters = parameters };
    }

    /// <summary>
    /// Isolates a definition's first phase down to a single FixedSequence
    /// task — its own first task, Group and declared Parameters untouched —
    /// so that catalogue-choice / multi-task composition
    /// (drawPhase.unsupportedRoundComposition) cannot mask the
    /// parameter-resolution path under test. F5K's real preliminary/fly-off
    /// phases are ChooseFromCatalogue over five tasks and would fail DrawPhase
    /// on composition alone, before ever reaching the Group.MinPerGroup check
    /// — exactly the reason PhaseDrawnDecideTests tests F5K's shape via an
    /// isolated F3J-based fixture rather than SeedF5K.Definition directly.
    /// Catalogue-choice rounds are explicitly out of this thread's scope
    /// (the plan's Finding 1); this isolation is what lets property 5 assert
    /// the in-scope behaviour (binding unblocks the draw) without also
    /// asserting the out-of-scope one (catalogue rounds are drawable).
    /// </summary>
    private static ClassDefinition IsolatedToFirstTask(ClassDefinition definition)
    {
        var phase = definition.Phases[0];
        var task = phase.Tasks[0];
        var isolatedPhase = phase with { Rounds = new RoundComposition(), Tasks = [task] };
        return definition with { Phases = [isolatedPhase] };
    }

    // ------------------------------------------------------------ property 1

    /// <summary>
    /// Shared assertion for property 1 and property 5: DrawPhase fails with
    /// drawPhase.parameterUnbound; BindParameter then succeeds; the redrawn
    /// phase satisfies the same size invariants PhaseDrawPropertyTests
    /// asserts against PhaseDraw.BuildGroups directly for a literal
    /// MinPerGroup class — every competitor placed exactly once per round,
    /// group count/sizes from the field/minPerGroup formula, no group below
    /// the bound minimum.
    /// </summary>
    private static void AssertBindingUnblocksTheDraw(
        ClassDefinition definition, string parameterName, int fieldSize, int rounds, decimal boundValue)
    {
        var competition = CompetitionAdopting(definition, fieldSize);

        var blocked = competition.DrawPhase(rounds, Now);
        blocked.IsFailure.Should().BeTrue();
        blocked.Code.Should().Be("drawPhase.parameterUnbound");

        var bound = competition.BindParameter(parameterName, MeasuredValue.Of(boundValue), "cd", Now);
        bound.IsSuccess.Should().BeTrue();
        competition = competition.Apply(bound.Value);

        var drawn = competition.DrawPhase(rounds, Now);
        drawn.IsSuccess.Should().BeTrue();
        drawn.Value.Rounds.Length.Should().Be(rounds);

        var minPerGroup = (int)boundValue;
        var groupCount = Math.Max(1, fieldSize / minPerGroup);
        var baseSize = fieldSize / groupCount;

        foreach (var round in drawn.Value.Rounds)
        {
            var taskRound = round.TaskRounds[0];
            var placed = taskRound.Groups.SelectMany(g => g.CompetitorRefs).ToArray();

            placed.Length.Should().Be(fieldSize);
            placed.Distinct().Count().Should().Be(fieldSize);
            taskRound.Groups.Length.Should().Be(groupCount);
            taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length == baseSize || g.CompetitorRefs.Length == baseSize + 1);
            taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length >= minPerGroup);
        }
    }

    /// <summary>
    /// Property 1 — the unblocking property. This is the property that
    /// proves the thread did its job.
    /// </summary>
    [Fact]
    public void BindParameter_unblocks_DrawPhase_and_the_redrawn_groups_satisfy_the_size_invariants()
    {
        var definition = WithParameterisedMinPerGroup(SeedF3J.Definition, "minPerGroup");

        (from field in Gen.Int[4, 16]
         from minPerGroup in Gen.Int[2, Math.Max(2, field / 2)]
         from rounds in Gen.Int[1, 3]
         select (field, minPerGroup, rounds))
        .Sample(t => AssertBindingUnblocksTheDraw(definition, "minPerGroup", t.field, t.rounds, t.minPerGroup));
    }

    // ------------------------------------------------------------ property 2

    /// <summary>
    /// Property 2 — last-write-wins. For any sequence of bindings of one
    /// parameter, the draw resolves the final value, and every earlier
    /// binding is still present in ParameterBindings afterward — the fold
    /// (Competition.Apply(ParameterBound)) only ever appends.
    /// </summary>
    [Fact]
    public void BindParameter_last_write_wins_and_every_earlier_binding_survives_in_ParameterBindings()
    {
        var definition = WithParameterisedMinPerGroup(SeedF3J.Definition, "minPerGroup");

        (from field in Gen.Int[6, 16]
         from values in Gen.Int[2, Math.Max(2, field / 2)].Array[1, 6]
         from rounds in Gen.Int[1, 3]
         select (field, values, rounds))
        .Sample(t =>
        {
            var competition = CompetitionAdopting(definition, t.field);

            foreach (var value in t.values)
            {
                var bound = competition.BindParameter("minPerGroup", MeasuredValue.Of((decimal)value), "cd", Now);
                bound.IsSuccess.Should().BeTrue();
                competition = competition.Apply(bound.Value);
            }

            // Nothing overwritten or removed — every binding made is still there,
            // in the order it was made.
            competition.ParameterBindings.Length.Should().Be(t.values.Length);
            competition.ParameterBindings.Select(b => b.BoundValue.Number)
                .Should().Equal(t.values.Select(v => (decimal?)v));

            var finalValue = t.values[^1];

            var drawn = competition.DrawPhase(t.rounds, Now);
            drawn.IsSuccess.Should().BeTrue();

            var groupCount = Math.Max(1, t.field / finalValue);
            var baseSize = t.field / groupCount;

            foreach (var round in drawn.Value.Rounds)
            {
                var taskRound = round.TaskRounds[0];
                taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length == baseSize || g.CompetitorRefs.Length == baseSize + 1);
                taskRound.Groups.Should().OnlyContain(g => g.CompetitorRefs.Length >= finalValue);
            }
        });
    }

    // ------------------------------------------------------------ property 3

    /// <summary>
    /// Property 3 — F5L's shared binding. 5.5.12.4: the fly-off group size
    /// equals the preliminary group size, and the real definition honours it
    /// by referencing one groupSize parameter from both phases'
    /// Group.MinPerGroup. Only the first phase can currently be drawn
    /// (Competition.DrawPhase's "only the first, unconditional draw"), so
    /// this resolves both slots directly through ParameterResolver against
    /// the flattened bindings, rather than calling DrawPhase twice.
    /// </summary>
    [Fact]
    public void F5L_binding_groupSize_once_resolves_both_phases_minPerGroup_slots_to_the_same_value()
    {
        var definition = SeedF5L.Definition;
        definition.Phases.Length.Should().Be(2);

        var preliminaryMinPerGroup = definition.Phases[0].Tasks[0].Group!.MinPerGroup;
        var flyoffMinPerGroup = definition.Phases[1].Tasks[0].Group!.MinPerGroup;

        preliminaryMinPerGroup.Should().BeOfType<NumberOrParam.Ref>().Subject.ParameterName.Should().Be("groupSize");
        flyoffMinPerGroup.Should().BeOfType<NumberOrParam.Ref>().Subject.ParameterName.Should().Be("groupSize");

        Gen.Int[2, 20].Sample(value =>
        {
            var bindings = new Dictionary<string, MeasuredValue> { ["groupSize"] = MeasuredValue.Of((decimal)value) };

            var preliminaryResolved = ParameterResolver.Resolve(preliminaryMinPerGroup, bindings, definition.Parameters);
            var flyoffResolved = ParameterResolver.Resolve(flyoffMinPerGroup, bindings, definition.Parameters);

            preliminaryResolved.Should().Be(value);
            flyoffResolved.Should().Be(value);
        });
    }

    // ------------------------------------------------------------ property 4

    /// <summary>
    /// Property 4 — binding beats default, default beats nothing. For every
    /// Parameter in the seed corpus that declares a DefaultValue: resolving
    /// with no binding yields the default; resolving with a binding yields
    /// the bound value regardless of what the default says. Drives
    /// ParameterResolver directly, per Kind, against a hand-built bindings
    /// dictionary — not through DrawPhase.
    /// </summary>
    [Fact]
    public void ParameterResolver_prefers_the_binding_over_the_default_and_the_default_over_nothing_for_every_defaulted_corpus_parameter()
    {
        var defaultedParameters = Corpus.All
            .SelectMany(c => c.Definition.Parameters)
            .Where(p => p.DefaultValue is not null)
            .ToImmutableArray();

        // Thirteen today (the plan's Finding 2): F5K nlh, NZ-M targetTime, F3J
        // flyoffSize, F5J flyoffMaxGroup/flyoffMinRounds, F3B minRounds and
        // F3K's six per-round values. Guard the premise, not the count.
        defaultedParameters.Should().NotBeEmpty();

        var emptyBindings = new Dictionary<string, MeasuredValue>();

        foreach (var parameter in defaultedParameters)
        {
            var declared = ImmutableArray.Create(parameter);

            if (parameter.Kind == MeasuredKind.Number)
            {
                ParameterResolver.Resolve(NumberOrParam.Param(parameter.Name), emptyBindings, declared)
                    .Should().Be(parameter.DefaultValue!.Number!.Value);

                Gen.Int[-1000, 1000].Sample(
                    boundValue =>
                    {
                        var bindings = new Dictionary<string, MeasuredValue> { [parameter.Name] = MeasuredValue.Of((decimal)boundValue) };

                        ParameterResolver.Resolve(NumberOrParam.Param(parameter.Name), bindings, declared)
                            .Should().Be(boundValue);
                    },
                    iter: 20);
            }
            else
            {
                ParameterResolver.ResolveFlag(FlagOrParam.Param(parameter.Name), emptyBindings, declared)
                    .Should().Be(parameter.DefaultValue!.Flag!.Value);

                foreach (var boundFlag in new[] { true, false })
                {
                    var bindings = new Dictionary<string, MeasuredValue> { [parameter.Name] = MeasuredValue.Of(boundFlag) };

                    ParameterResolver.ResolveFlag(FlagOrParam.Param(parameter.Name), bindings, declared)
                        .Should().Be(boundFlag);
                }
            }
        }
    }

    // ------------------------------------------------------------ property 5

    /// <summary>
    /// Property 5 — generic over the corpus. Discovers every seed definition
    /// with a parameterised (NumberOrParam.Param, not Literal) minPerGroup on
    /// any phase's task by scanning Soarscore.SeedData.Corpus.All directly —
    /// not a hard-coded list — asserts the discovered set is exactly
    /// {F5K, F5L, NZ Class M ALES 200} so a future fourth parameterised class
    /// makes this fail loudly rather than silently pass, then runs property
    /// 1's assertion against each, isolated to a single FixedSequence task
    /// (see IsolatedToFirstTask) so catalogue-choice composition — out of
    /// this thread's scope — does not mask the parameter-resolution
    /// behaviour under test.
    /// </summary>
    [Fact]
    public void Every_seed_definition_with_a_parameterised_minPerGroup_is_unblocked_by_BindParameter()
    {
        var discovered = Corpus.All
            .Where(c => c.Definition.Phases.Any(phase => phase.Tasks.Any(task => task.Group?.MinPerGroup is NumberOrParam.Ref)))
            .ToImmutableArray();

        discovered.Select(c => c.FileName).Should().BeEquivalentTo(["40-f5k", "60-f5l", "80-nz-m-ales200"]);

        foreach (var seedClass in discovered)
        {
            var isolated = IsolatedToFirstTask(seedClass.Definition);
            var parameterName = ((NumberOrParam.Ref)isolated.Phases[0].Tasks[0].Group!.MinPerGroup).ParameterName;

            (from field in Gen.Int[4, 16]
             from minPerGroup in Gen.Int[2, Math.Max(2, field / 2)]
             from rounds in Gen.Int[1, 3]
             select (field, minPerGroup, rounds))
            .Sample(
                t => AssertBindingUnblocksTheDraw(isolated, parameterName, t.field, t.rounds, t.minPerGroup),
                iter: 30);
        }
    }
}
