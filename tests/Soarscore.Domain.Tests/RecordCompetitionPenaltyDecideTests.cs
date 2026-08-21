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
}
