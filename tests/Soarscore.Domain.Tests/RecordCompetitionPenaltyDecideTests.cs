using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Decide-function tests for <see cref="Competition.RecordPenalty"/> —
/// kanban/in-progress/annul-and-penalise-the-second-entry-thread.md WI-4. One
/// example per defect code plus the happy path, seeded by folding
/// CompetitionCreated → CompetitorRegistered → PhaseDrawn (the same pattern
/// CompetitionDecideTests.cs uses). The adopted F5K definition declares
/// safetyZone (DeductPoints 300) — the canonical Competition-scoped penalty.
///
/// Home to the penalty append-only invariant P3 (Competition half).
/// </summary>
public class RecordCompetitionPenaltyDecideTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly ClassDefinition F5K = SeedF5K.Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = F5K,
            SourceClassId = "content-hash-abc123",
            SourceVersion = F5K.Version,
            AdoptedAt = Now,
        };

    /// <summary>CompetitionCreated → CompetitorRegistered → PhaseDrawn, one competitor in one group.</summary>
    private static (Competition Competition, CompetitorId Competitor) SeedCompetition()
    {
        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(), Now));

        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = Now,
        };
        competition = competition.Apply(new CompetitorRegistered(competitor, Now));

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [competitor.Id] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        competition = competition.Apply(new PhaseDrawn(
            0, PhaseType.Preliminary, new Draw { CreatedAt = Now, Status = "drawn" }, [round], Now));

        return (competition, competitor.Id);
    }

    private static Penalty CompetitionPenalty(CompetitorId competitorRef) =>
        new() { InfractionType = "safetyZone", Scope = PenaltyScope.Competition, CompetitorRef = competitorRef };

    /// <summary>CompetitionCreated → CompetitorRegistered → PhaseDrawn, one competitor in one group,
    /// adopting <paramref name="definition"/> instead of F5K.</summary>
    private static (Competition Competition, CompetitorId Competitor) SeedCompetitionWith(ClassDefinition definition)
    {
        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Club Champs 2026", "Auckland",
            new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13),
            "1", SampleAdoptedRules(definition), Now));

        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = Now,
        };
        competition = competition.Apply(new CompetitorRegistered(competitor, Now));

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [competitor.Id] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        competition = competition.Apply(new PhaseDrawn(
            0, PhaseType.Preliminary, new Draw { CreatedAt = Now, Status = "drawn" }, [round], Now));

        return (competition, competitor.Id);
    }

    private static AdoptedRules SampleAdoptedRules(ClassDefinition definition) =>
        new()
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };

    // ------------------------------------------------- WI-3, D-A3 (write side)
    // Zero*-anchoring: every zeroing clause in the rule corpus names its round
    // (F3K.1.2, F3K.4.1, F3B.2.2 p), so a Zero*-carrying record without a
    // task-round coordinate is incomplete data — refused at record time with
    // recordPenalty.zeroEffectRequiresTaskRound, completing the payload rather
    // than refusing the scope (D-A3:
    // kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md#wi-3).
    // nonConformingWinch (F3B.2.2 p, ZeroFlight + DeductPoints 1000) is the
    // corpus's own Zero*-carrying definition.

    [Fact]
    public void RecordPenalty_with_a_zero_carrying_definition_and_no_task_round_fails_with_a_stable_code()
    {
        var (competition, competitorRef) = SeedCompetitionWith(SeedF3B.Definition);
        var penalty = CompetitionPenalty(competitorRef) with { InfractionType = "nonConformingWinch", TaskRound = null };

        var result = competition.RecordPenalty(penalty);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.zeroEffectRequiresTaskRound");
    }

    [Fact]
    public void RecordPenalty_with_a_zero_carrying_definition_and_a_task_round_succeeds()
    {
        var (competition, competitorRef) = SeedCompetitionWith(SeedF3B.Definition);
        var coordinate = new TaskRoundCoordinate(0, 1, 1);
        var penalty = CompetitionPenalty(competitorRef) with
        {
            InfractionType = "nonConformingWinch",
            Scope = PenaltyScope.TaskRound,
            TaskRound = coordinate,
        };

        var result = competition.RecordPenalty(penalty);

        result.IsSuccess.Should().BeTrue();
        result.Value.Penalty.TaskRound.Should().Be(coordinate);
    }

    // ------------------------------------------------------------------- FAILURES

    [Fact]
    public void RecordPenalty_with_a_flight_scope_fails_with_a_stable_code()
    {
        var (competition, competitorRef) = SeedCompetition();
        var penalty = CompetitionPenalty(competitorRef) with { Scope = PenaltyScope.Flight };

        var result = competition.RecordPenalty(penalty);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.wrongScope");
    }

    [Fact]
    public void RecordPenalty_without_a_competitor_ref_fails_with_a_stable_code()
    {
        var (competition, _) = SeedCompetition();
        var penalty = CompetitionPenalty(CompetitorId.New()) with { CompetitorRef = null };

        var result = competition.RecordPenalty(penalty);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.competitorRequired");
    }

    [Fact]
    public void RecordPenalty_against_an_unknown_competitor_fails_with_a_stable_code()
    {
        var (competition, _) = SeedCompetition();

        var result = competition.RecordPenalty(CompetitionPenalty(CompetitorId.New()));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("competition.competitor.notFound");
    }

    [Fact]
    public void RecordPenalty_with_a_task_round_coordinate_that_does_not_exist_fails_with_a_stable_code()
    {
        var (competition, competitorRef) = SeedCompetition();
        var penalty = CompetitionPenalty(competitorRef) with { TaskRound = new TaskRoundCoordinate(0, 2, 1) };

        var result = competition.RecordPenalty(penalty);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.taskRoundNotFound");
    }

    [Fact]
    public void RecordPenalty_with_an_undeclared_infraction_type_fails_with_a_stable_code()
    {
        var (competition, competitorRef) = SeedCompetition();
        var penalty = CompetitionPenalty(competitorRef) with { InfractionType = "madeUp" };

        var result = competition.RecordPenalty(penalty);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.infractionTypeNotDeclared");
    }

    [Fact]
    public void RecordPenalty_with_a_blank_by_fails_with_a_stable_code()
    {
        var (competition, competitorRef) = SeedCompetition();
        var penalty = CompetitionPenalty(competitorRef) with { By = "  " };

        var result = competition.RecordPenalty(penalty);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.byBlank");
    }

    // -------------------------------------------------------------------- SUCCESS

    [Fact]
    public void RecordPenalty_succeeds_carrying_the_payload_into_the_event()
    {
        var (competition, competitorRef) = SeedCompetition();
        var by = "the contest director";
        var coordinate = new TaskRoundCoordinate(0, 1, 1);
        var penalty = CompetitionPenalty(competitorRef) with { By = by, TaskRound = coordinate };

        var result = competition.RecordPenalty(penalty);

        result.IsSuccess.Should().BeTrue();
        result.Value.Penalty.Should().Be(penalty);
        result.Value.Penalty.CompetitorRef.Should().Be(competitorRef);
        result.Value.Penalty.TaskRound.Should().Be(coordinate);
        result.Value.Penalty.By.Should().Be(by);
    }

    [Fact]
    public void RecordPenalty_with_an_absent_coordinate_and_by_succeeds()
    {
        var (competition, competitorRef) = SeedCompetition();

        var result = competition.RecordPenalty(CompetitionPenalty(competitorRef));

        result.IsSuccess.Should().BeTrue();
        result.Value.Penalty.TaskRound.Should().BeNull();
        result.Value.Penalty.By.Should().BeNull();
    }

    [Fact]
    public void Folding_a_recorded_penalty_grows_the_penalties_list()
    {
        var (competition, competitorRef) = SeedCompetition();
        var decision = competition.RecordPenalty(CompetitionPenalty(competitorRef));

        var folded = competition.Apply(decision.Value);

        folded.Penalties.Should().ContainSingle();
        folded.Penalties[0].Should().Be(CompetitionPenalty(competitorRef));
    }

    // ======================================================================= PROPERTY TESTS — P3 (Competition half)

    private static Gen<Penalty> CompetitionPenaltyGen(CompetitorId competitorRef) =>
        from byValue in Gen.OneOfConst<string?>(null, "the scorer", "the CD")
        select new Penalty
        {
            InfractionType = "safetyZone",
            Scope = PenaltyScope.Competition,
            CompetitorRef = competitorRef,
            By = byValue,
        };

    [Fact]
    public void Competition_penalties_are_append_only()
    {
        var (competition, competitorRef) = SeedCompetition();
        var gen = CompetitionPenaltyGen(competitorRef);

        gen.Array[1, 5].Sample(penalties =>
        {
            var folded = competition;

            foreach (var penalty in penalties)
            {
                var decision = folded.RecordPenalty(penalty);
                decision.IsSuccess.Should().BeTrue();
                folded = folded.Apply(decision.Value);
            }

            folded.Penalties.Length.Should().Be(penalties.Length);
            folded.Penalties.Should().Equal(penalties);
        });
    }

    // =============================================== WI-1 scope gate (permitted-scopes-on-penalty-definitions.md#wi-1)
    // A definition may declare PermittedScopes; a record outside them is refused
    // with recordPenalty.scopeNotAllowed. Only the aggregate-legal scopes
    // (TaskRound/Competition) are reachable here — a Flight/Entry-scoped record
    // against a Competition reports recordPenalty.wrongScope first (D-2).

    /// <summary>A minimal one-penalty definition (ReflightScoringTests' fixture shape)
    /// carrying <paramref name="permittedScopes"/> and optional Zero effect.</summary>
    private static ClassDefinition MakeDefinition(
        PenaltyScope[]? permittedScopes, bool withZeroEffect = false) =>
        new()
        {
            Name = "Synthetic",
            Version = "1.0",
            Reflight = new ReflightRule
            {
                EntitledScores = ReflightSelection.NotPermitted,
                OthersScore = ReflightSelection.NotPermitted,
            },
            Penalties =
            [
                new PenaltyDefinition
                {
                    InfractionType = "testInfraction",
                    Effects = withZeroEffect
                        ? [new(PenaltyEffect.ZeroFlight)]
                        : [new(PenaltyEffect.DeductPoints, 100)],
                    PermittedScopes = permittedScopes,
                },
            ],
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
                            Name = "Test task",
                            Metrics = [new MetricDefinition { Name = "raw", Kind = MeasuredKind.Number }],
                            Flights = new LastFlight(),
                            Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
                            Group = new GroupConstraint { MinPerGroup = 2 },
                            Score = [(ScoreTerm)new RateTerm { MetricRef = "raw", Rate = 1 }],
                        },
                    ],
                },
            ],
        };

    private static Penalty ScopedPenalty(CompetitorId competitorRef, PenaltyScope scope) =>
        new() { InfractionType = "testInfraction", Scope = scope, CompetitorRef = competitorRef };

    [Fact]
    public void RecordPenalty_outside_the_permitted_scopes_fails_with_a_stable_code()
    {
        var (competition, competitorRef) = SeedCompetitionWith(MakeDefinition([PenaltyScope.TaskRound]));

        var result = competition.RecordPenalty(ScopedPenalty(competitorRef, PenaltyScope.Competition));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.scopeNotAllowed");
    }

    [Fact]
    public void RecordPenalty_at_a_permitted_scope_succeeds()
    {
        var (competition, competitorRef) = SeedCompetitionWith(MakeDefinition([PenaltyScope.Competition]));

        var result = competition.RecordPenalty(ScopedPenalty(competitorRef, PenaltyScope.Competition));

        result.IsSuccess.Should().BeTrue();
        result.Value.Penalty.Scope.Should().Be(PenaltyScope.Competition);
    }

    // D-2 precedence pin: scope refusal outranks payload completeness — a
    // mis-scoped Zero* record reports scopeNotAllowed, not zeroEffectRequiresTaskRound.
    [Fact]
    public void RecordPenalty_scope_refusal_outranks_the_zero_effect_coordinate_check()
    {
        var (competition, competitorRef) =
            SeedCompetitionWith(MakeDefinition([PenaltyScope.TaskRound], withZeroEffect: true));

        var result = competition.RecordPenalty(ScopedPenalty(competitorRef, PenaltyScope.Competition));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("recordPenalty.scopeNotAllowed");
    }

    // P-ScopeGate (Competition half): over the two aggregate-legal scopes and
    // permitted sets {null, [TaskRound], [Competition], [TaskRound, Competition]},
    // a record succeeds IFF the definition's PermittedScopes is null or contains
    // the recorded scope.
    [Fact]
    public void P_ScopeGate_success_iff_permitted_scopes_is_null_or_contains_the_recorded_scope()
    {
        var gen =
            from scope in Gen.OneOfConst(PenaltyScope.TaskRound, PenaltyScope.Competition)
            from permitted in Gen.OneOfConst<PenaltyScope[]?>(
                null,
                [PenaltyScope.TaskRound],
                [PenaltyScope.Competition],
                [PenaltyScope.TaskRound, PenaltyScope.Competition])
            select (scope, permitted);

        gen.Sample(tuple =>
        {
            var (scope, permitted) = tuple;
            var definition = MakeDefinition(permitted);
            var (candidate, competitorRef) = SeedCompetitionWith(definition);

            var result = candidate.RecordPenalty(ScopedPenalty(competitorRef, scope));

            var shouldSucceed = permitted is null || permitted.Contains(scope);
            result.IsSuccess.Should().Be(shouldSucceed);
            if (shouldSucceed)
            {
                result.Value.Penalty.Scope.Should().Be(scope);
            }
            else
            {
                result.Code.Should().Be("recordPenalty.scopeNotAllowed");
            }
        });
    }
}
