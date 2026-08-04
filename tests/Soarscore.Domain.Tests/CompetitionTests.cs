using System.Collections.Immutable;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
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

        var group = new Group { Id = GroupId.New(), Ordinal = 1 };

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
            Type = Soarscore.Domain.CompetitionClasses.PhaseType.Preliminary,
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

        Assert.Equal(2, competition.Competitors.Length);
        Assert.Single(competition.Phases);
        Assert.Equal("A", competition.Phases[0].Rounds[0].TaskRounds[0].TaskRef);
        Assert.Equal("RC Hand-Launch Gliders", competition.AdoptedRules.Definition.Name);
        Assert.Equal(adoptedRules.SourceVersion, competition.AdoptedRules.Definition.Version);
        Assert.Empty(competition.RulesAmendments);
        Assert.Empty(competition.ParameterBindings);
        Assert.Empty(competition.Finalisations);
        Assert.Empty(competition.Penalties);
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

        Assert.Equal(FinalisationScope.Phase, finalisation.Scope);
        Assert.Single(finalisation.DeclaredResults);
        Assert.Equal(competitorId, finalisation.DeclaredResults[0].CompetitorRef);
        Assert.True(finalisation.DeclaredResults[0].Promoted);
    }

    [Theory]
    [InlineData(TaskRoundState.Complete, TaskRoundState.Complete, true)]
    [InlineData(TaskRoundState.Complete, TaskRoundState.Annulled, true)]
    [InlineData(TaskRoundState.Annulled, TaskRoundState.Annulled, true)]
    [InlineData(TaskRoundState.Complete, TaskRoundState.Drawn, false)]
    [InlineData(TaskRoundState.Complete, TaskRoundState.InProgress, false)]
    [InlineData(TaskRoundState.Drawn, TaskRoundState.Drawn, false)]
    public void Round_IsComplete_reflects_taskRound_states(
        TaskRoundState first, TaskRoundState second, bool expectedComplete)
    {
        var group = new Group { Id = GroupId.New(), Ordinal = 1 };

        var round = new Round
        {
            Ordinal = 1,
            TaskRounds =
            [
                new TaskRound { Ordinal = 1, State = first, TaskRef = "A", Groups = [group] },
                new TaskRound { Ordinal = 2, State = second, TaskRef = "B", Groups = [group] },
            ],
        };

        Assert.Equal(expectedComplete, round.IsComplete);
    }
}
