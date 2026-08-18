using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>Data-shape sanity suite for the Competition aggregate. No business logic lives here.</summary>
public class CompetitionTests
{
    [Fact]
    public void Competition_with_full_structure_holds_together()
    {
        var competitorA = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = DateTimeOffset.UtcNow,
        };
        var competitorB = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 2,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };

        var taskRound = new TaskRound
        {
            Ordinal = 1,
            State = TaskRoundState.Drawn,
            TaskRef = "A",
            Groups = [group],
        };

        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };

        var phase = new Phase
        {
            Type = PhaseType.Preliminary,
            Ordinal = 1,
            Draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" },
            Rounds = [round],
        };

        var adoptedRules = new AdoptedRules
        {
            Definition = SeedF3K.Definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SeedF3K.Definition.Version,
            AdoptedAt = DateTimeOffset.UtcNow,
        };

        var competition = new Competition
        {
            Id = CompetitionId.New(),
            Name = "Club Champs 2026",
            Location = "Auckland",
            StartDate = new DateOnly(2026, 3, 14),
            EndDate = new DateOnly(2026, 3, 15),
            EvaluatorVersion = "1.0.0",
            Competitors = [competitorA, competitorB],
            Phases = [phase],
            AdoptedRules = adoptedRules,
        };

        competition.Competitors.Length.Should().Be(2);
        competition.Phases.Should().ContainSingle();
        competition.Phases[0].Rounds[0].TaskRounds[0].TaskRef.Should().Be("A");
        competition.AdoptedRules.Definition.Name.Should().Be("RC Hand-Launch Gliders");
        competition.AdoptedRules.Definition.Version.Should().Be(adoptedRules.SourceVersion);
        competition.RulesAmendments.Should().BeEmpty();
        competition.ParameterBindings.Should().BeEmpty();
        competition.Finalisations.Should().BeEmpty();
        competition.Penalties.Should().BeEmpty();
    }

    [Fact]
    public void Finalisation_carries_declared_results_for_competitors()
    {
        var competitorId = CompetitorId.New();

        var finalisation = new Finalisation
        {
            Scope = FinalisationScope.Phase,
            Revision = 1,
            By = "CD",
            At = DateTimeOffset.UtcNow,
            DeclaredResults =
            [
                new DeclaredResult
                {
                    CompetitorRef = competitorId,
                    Aggregate = 987.5m,
                    Placing = 1,
                    Promoted = true,
                },
            ],
        };

        finalisation.Scope.Should().Be(FinalisationScope.Phase);
        finalisation.DeclaredResults.Should().ContainSingle();
        finalisation.DeclaredResults[0].CompetitorRef.Should().Be(competitorId);
        finalisation.DeclaredResults[0].Promoted.Should().BeTrue();
    }

    // The two predicates are asserted together on purpose: the whole reason
    // there are two is that they disagree the moment a task-round is Annulled.
    [Theory]
    [InlineData(TaskRoundState.Complete, TaskRoundState.Complete, true, true)]
    [InlineData(TaskRoundState.Complete, TaskRoundState.Annulled, true, false)]
    [InlineData(TaskRoundState.Annulled, TaskRoundState.Annulled, true, false)]
    [InlineData(TaskRoundState.Complete, TaskRoundState.Drawn, false, false)]
    [InlineData(TaskRoundState.Complete, TaskRoundState.InProgress, false, false)]
    [InlineData(TaskRoundState.Drawn, TaskRoundState.Drawn, false, false)]
    public void Round_completion_predicates_reflect_taskRound_states(
        TaskRoundState first, TaskRoundState second, bool expectedCompleteOrAnnulled, bool expectedFullyFlown)
    {
        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };

        var round = new Round
        {
            Ordinal = 1,
            TaskRounds =
            [
                new TaskRound { Ordinal = 1, State = first, TaskRef = "A", Groups = [group] },
                new TaskRound { Ordinal = 2, State = second, TaskRef = "B", Groups = [group] },
            ],
        };

        round.IsCompleteOrAnnulled.Should().Be(expectedCompleteOrAnnulled);
        round.IsFullyFlown.Should().Be(expectedFullyFlown);
    }
}
