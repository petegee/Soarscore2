using System.Collections.Immutable;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class CompetitionFoldTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static AdoptedRules SampleAdoptedRules() =>
        new()
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = DateTimeOffset.UtcNow,
        };

    private static CompetitionCreated SampleCreatedEvent(DateTimeOffset? at = null) =>
        new(
            CompetitionId.New(),
            "Club Champs 2026",
            "Auckland",
            new DateOnly(2026, 3, 14),
            new DateOnly(2026, 3, 15),
            "1.0.0",
            SampleAdoptedRules(),
            at ?? DateTimeOffset.UtcNow);

    [Fact]
    public void Created_creates_the_projection_with_empty_collections()
    {
        var @event = SampleCreatedEvent();

        var competition = Competition.Create(@event);

        Assert.NotNull(competition);
        Assert.Equal(@event.Id, competition.Id);
        Assert.Equal(@event.Name, competition.Name);
        Assert.Equal(@event.Location, competition.Location);
        Assert.Equal(@event.StartDate, competition.StartDate);
        Assert.Equal(@event.EndDate, competition.EndDate);
        Assert.Equal(@event.EvaluatorVersion, competition.EvaluatorVersion);
        Assert.Same(@event.AdoptedRules, competition.AdoptedRules);
        Assert.Empty(competition.Competitors);
        Assert.Empty(competition.Phases);
        Assert.Empty(competition.RulesAmendments);
        Assert.Empty(competition.ParameterBindings);
        Assert.Empty(competition.Finalisations);
        Assert.Empty(competition.Penalties);
    }

    [Fact]
    public void CompetitorRegistered_appends_to_the_field()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

        var updated = competition.Apply(new CompetitorRegistered(competitor, DateTimeOffset.UtcNow));

        Assert.Single(updated.Competitors);
        Assert.Equal(competitor.Id, updated.Competitors[0].Id);
        Assert.Null(updated.Competitors[0].WithdrawnAt);
    }

    [Fact]
    public void CompetitorRegistered_against_no_current_projection_throws()
    {
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

        Assert.Throws<ArgumentException>(() =>
            Competition.Apply(null, new CompetitorRegistered(competitor, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void CompetitorWithdrawn_sets_WithdrawnAt_and_leaves_others_untouched()
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

        var competition = Competition.Create(SampleCreatedEvent());
        competition = competition.Apply(new CompetitorRegistered(competitorA, DateTimeOffset.UtcNow));
        competition = competition.Apply(new CompetitorRegistered(competitorB, DateTimeOffset.UtcNow));

        var withdrawnAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var updated = competition.Apply(new CompetitorWithdrawn(competitorA.Id, withdrawnAt));

        Assert.Equal(2, updated.Competitors.Length);
        var updatedA = updated.Competitors.Single(c => c.Id == competitorA.Id);
        var updatedB = updated.Competitors.Single(c => c.Id == competitorB.Id);
        Assert.Equal(withdrawnAt, updatedA.WithdrawnAt);
        Assert.Null(updatedB.WithdrawnAt);
    }

    [Fact]
    public void PhaseDrawn_appends_a_new_phase_with_its_schedule()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var group = new Group { Id = GroupId.New(), Ordinal = 1 };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };

        var updated = competition.Apply(new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], DateTimeOffset.UtcNow));

        Assert.Single(updated.Phases);
        Assert.Equal(1, updated.Phases[0].Ordinal);
        Assert.Equal(PhaseType.Preliminary, updated.Phases[0].Type);
        Assert.Equal("A", updated.Phases[0].Rounds[0].TaskRounds[0].TaskRef);
    }

    private static Competition CompetitionWithOneTaskRound()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var group = new Group { Id = GroupId.New(), Ordinal = 1 };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };

        return competition.Apply(new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReflightGroupAppended_navigates_by_ordinal_and_appends_the_group()
    {
        var competition = CompetitionWithOneTaskRound();
        var newGroup = new Group { Id = GroupId.New(), Ordinal = 2 };

        var updated = competition.Apply(new ReflightGroupAppended(1, 1, 1, newGroup, DateTimeOffset.UtcNow));

        var taskRound = updated.Phases[0].Rounds[0].TaskRounds[0];
        Assert.Equal(2, taskRound.Groups.Length);
        Assert.Contains(taskRound.Groups, g => g.Id == newGroup.Id);
    }

    [Fact]
    public void TaskRoundCompleted_sets_state_and_leaves_other_task_rounds_untouched()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var group = new Group { Id = GroupId.New(), Ordinal = 1 };
        var taskRoundA = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var taskRoundB = new TaskRound { Ordinal = 2, State = TaskRoundState.Drawn, TaskRef = "B", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRoundA, taskRoundB] };
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };
        competition = competition.Apply(new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], DateTimeOffset.UtcNow));

        var updated = competition.Apply(new TaskRoundCompleted(1, 1, 1, DateTimeOffset.UtcNow));

        var updatedRound = updated.Phases[0].Rounds[0];
        Assert.Equal(TaskRoundState.Complete, updatedRound.TaskRounds[0].State);
        Assert.Equal(TaskRoundState.Drawn, updatedRound.TaskRounds[1].State);
    }

    [Fact]
    public void TaskRoundAnnulled_sets_state_to_annulled()
    {
        var competition = CompetitionWithOneTaskRound();

        var updated = competition.Apply(new TaskRoundAnnulled(1, 1, 1, "wind out of limits", DateTimeOffset.UtcNow));

        Assert.Equal(TaskRoundState.Annulled, updated.Phases[0].Rounds[0].TaskRounds[0].State);
    }

    [Fact]
    public void RulesAmended_appends_to_RulesAmendments()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var amendment = new RulesAmendment
        {
            Definition = SampleDefinition,
            Reason = "corrected landing table",
            By = "CD",
            At = DateTimeOffset.UtcNow,
        };

        var updated = competition.Apply(new RulesAmended(amendment));

        Assert.Single(updated.RulesAmendments);
        Assert.Same(amendment, updated.RulesAmendments[0]);
    }

    [Fact]
    public void ParameterBound_appends_to_ParameterBindings()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var binding = new ParameterBinding
        {
            ParameterName = "workingTime",
            BoundValue = MeasuredValue.Of(600m),
            By = "CD",
            At = DateTimeOffset.UtcNow,
        };

        var updated = competition.Apply(new ParameterBound(binding));

        Assert.Single(updated.ParameterBindings);
        Assert.Same(binding, updated.ParameterBindings[0]);
    }

    [Fact]
    public void Finalised_appends_to_Finalisations()
    {
        var competition = Competition.Create(SampleCreatedEvent());
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
                    CompetitorRef = CompetitorId.New(),
                    Aggregate = 987.5m,
                    Placing = 1,
                    Promoted = true,
                },
            ],
        };

        var updated = competition.Apply(new Finalised(finalisation));

        Assert.Single(updated.Finalisations);
        Assert.Same(finalisation, updated.Finalisations[0]);
    }

    [Fact]
    public void PenaltyRecorded_appends_to_Penalties()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var penalty = new Penalty { InfractionType = "late launch", Scope = PenaltyScope.TaskRound };

        var updated = competition.Apply(new PenaltyRecorded(penalty));

        Assert.Single(updated.Penalties);
        Assert.Same(penalty, updated.Penalties[0]);
    }

    [Fact]
    public void Non_creation_events_against_no_current_projection_throw()
    {
        Assert.Throws<ArgumentException>(() =>
            Competition.Apply(null, new TaskRoundCompleted(1, 1, 1, DateTimeOffset.UtcNow)));
        Assert.Throws<ArgumentException>(() =>
            Competition.Apply(null, new PenaltyRecorded(new Penalty { InfractionType = "x", Scope = PenaltyScope.Competition })));
    }

    [Fact]
    public void A_full_event_stream_folds_in_order_to_the_expected_final_state()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var created = SampleCreatedEvent(createdAt);
        var competitorA = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = createdAt,
        };
        var group = new Group { Id = GroupId.New(), Ordinal = 1 };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = createdAt, Status = "Accepted" };
        var reflightGroup = new Group { Id = GroupId.New(), Ordinal = 2 };
        var penalty = new Penalty { InfractionType = "late launch", Scope = PenaltyScope.TaskRound };

        CompetitionEvent[] stream =
        [
            created,
            new CompetitorRegistered(competitorA, createdAt),
            new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], createdAt),
            new ReflightGroupAppended(1, 1, 1, reflightGroup, createdAt),
            new TaskRoundCompleted(1, 1, 1, createdAt),
            new PenaltyRecorded(penalty),
            new CompetitorWithdrawn(competitorA.Id, createdAt.AddHours(1)),
        ];

        var final = stream.Aggregate((Competition?)null, Competition.Apply);

        Assert.NotNull(final);
        Assert.Equal(created.Id, final.Id);
        Assert.Single(final.Competitors);
        Assert.Equal(createdAt.AddHours(1), final.Competitors[0].WithdrawnAt);
        Assert.Single(final.Phases);
        var finalTaskRound = final.Phases[0].Rounds[0].TaskRounds[0];
        Assert.Equal(2, finalTaskRound.Groups.Length);
        Assert.Equal(TaskRoundState.Complete, finalTaskRound.State);
        Assert.Single(final.Penalties);
    }
}
