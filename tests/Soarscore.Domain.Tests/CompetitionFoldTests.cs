using System.Collections.Immutable;
using AwesomeAssertions;
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

        competition.Should().NotBeNull();
        competition.Id.Should().Be(@event.Id);
        competition.Name.Should().Be(@event.Name);
        competition.Location.Should().Be(@event.Location);
        competition.StartDate.Should().Be(@event.StartDate);
        competition.EndDate.Should().Be(@event.EndDate);
        competition.EvaluatorVersion.Should().Be(@event.EvaluatorVersion);
        competition.AdoptedRules.Should().BeSameAs(@event.AdoptedRules);
        competition.Competitors.Should().BeEmpty();
        competition.Phases.Should().BeEmpty();
        competition.RulesAmendments.Should().BeEmpty();
        competition.ParameterBindings.Should().BeEmpty();
        competition.Finalisations.Should().BeEmpty();
        competition.Penalties.Should().BeEmpty();
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

        updated.Competitors.Should().ContainSingle();
        updated.Competitors[0].Id.Should().Be(competitor.Id);
        updated.Competitors[0].WithdrawnAt.Should().BeNull();
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

        FluentActions.Invoking(() =>
            Competition.Apply(null, new CompetitorRegistered(competitor, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
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

        updated.Competitors.Length.Should().Be(2);
        var updatedA = updated.Competitors.Single(c => c.Id == competitorA.Id);
        var updatedB = updated.Competitors.Single(c => c.Id == competitorB.Id);
        updatedA.WithdrawnAt.Should().Be(withdrawnAt);
        updatedB.WithdrawnAt.Should().BeNull();
    }

    [Fact]
    public void PhaseDrawn_appends_a_new_phase_with_its_schedule()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };

        var updated = competition.Apply(new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], DateTimeOffset.UtcNow));

        updated.Phases.Should().ContainSingle();
        updated.Phases[0].Ordinal.Should().Be(1);
        updated.Phases[0].Type.Should().Be(PhaseType.Preliminary);
        updated.Phases[0].Rounds[0].TaskRounds[0].TaskRef.Should().Be("A");
    }

    private static Competition CompetitionWithOneTaskRound()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };

        return competition.Apply(new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReflightGroupAppended_navigates_by_ordinal_and_appends_the_group()
    {
        var competition = CompetitionWithOneTaskRound();
        var newGroup = new Group { Id = GroupId.New(), Ordinal = 2, CompetitorRefs = [CompetitorId.New()] };

        var updated = competition.Apply(new ReflightGroupAppended(1, 1, 1, newGroup, "Mid-air collision", DateTimeOffset.UtcNow));

        var taskRound = updated.Phases[0].Rounds[0].TaskRounds[0];
        taskRound.Groups.Length.Should().Be(2);
        taskRound.Groups.Should().Contain(g => g.Id == newGroup.Id);
    }

    [Fact]
    public void TaskRoundCompleted_sets_state_and_leaves_other_task_rounds_untouched()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };
        var taskRoundA = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var taskRoundB = new TaskRound { Ordinal = 2, State = TaskRoundState.Drawn, TaskRef = "B", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRoundA, taskRoundB] };
        var draw = new Draw { CreatedAt = DateTimeOffset.UtcNow, Status = "Accepted" };
        competition = competition.Apply(new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], DateTimeOffset.UtcNow));

        var updated = competition.Apply(new TaskRoundCompleted(1, 1, 1, DateTimeOffset.UtcNow));

        var updatedRound = updated.Phases[0].Rounds[0];
        updatedRound.TaskRounds[0].State.Should().Be(TaskRoundState.Complete);
        updatedRound.TaskRounds[1].State.Should().Be(TaskRoundState.Drawn);
    }

    [Fact]
    public void TaskRoundAnnulled_sets_state_to_annulled()
    {
        var competition = CompetitionWithOneTaskRound();

        var updated = competition.Apply(new TaskRoundAnnulled(1, 1, 1, "wind out of limits", DateTimeOffset.UtcNow));

        updated.Phases[0].Rounds[0].TaskRounds[0].State.Should().Be(TaskRoundState.Annulled);
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

        updated.RulesAmendments.Should().ContainSingle();
        updated.RulesAmendments[0].Should().BeSameAs(amendment);
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

        updated.ParameterBindings.Should().ContainSingle();
        updated.ParameterBindings[0].Should().BeSameAs(binding);
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

        updated.Finalisations.Should().ContainSingle();
        updated.Finalisations[0].Should().BeSameAs(finalisation);
    }

    [Fact]
    public void PenaltyRecorded_appends_to_Penalties()
    {
        var competition = Competition.Create(SampleCreatedEvent());
        var penalty = new Penalty { InfractionType = "late launch", Scope = PenaltyScope.TaskRound };

        var updated = competition.Apply(new PenaltyRecorded(penalty));

        updated.Penalties.Should().ContainSingle();
        updated.Penalties[0].Should().BeSameAs(penalty);
    }

    [Fact]
    public void Non_creation_events_against_no_current_projection_throw()
    {
        FluentActions.Invoking(() =>
            Competition.Apply(null, new TaskRoundCompleted(1, 1, 1, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
            Competition.Apply(null, new PenaltyRecorded(new Penalty { InfractionType = "x", Scope = PenaltyScope.Competition })))
            .Should().Throw<ArgumentException>();
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
        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = createdAt, Status = "Accepted" };
        var reflightGroup = new Group { Id = GroupId.New(), Ordinal = 2, CompetitorRefs = [CompetitorId.New()] };
        var penalty = new Penalty { InfractionType = "late launch", Scope = PenaltyScope.TaskRound };

        CompetitionEvent[] stream =
        [
            created,
            new CompetitorRegistered(competitorA, createdAt),
            new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], createdAt),
            new ReflightGroupAppended(1, 1, 1, reflightGroup, "Mid-air collision", createdAt),
            new TaskRoundCompleted(1, 1, 1, createdAt),
            new PenaltyRecorded(penalty),
            new CompetitorWithdrawn(competitorA.Id, createdAt.AddHours(1)),
        ];

        var final = stream.Aggregate((Competition?)null, Competition.Apply);

        final.Should().NotBeNull();
        final.Id.Should().Be(created.Id);
        final.Competitors.Should().ContainSingle();
        final.Competitors[0].WithdrawnAt.Should().Be(createdAt.AddHours(1));
        final.Phases.Should().ContainSingle();
        var finalTaskRound = final.Phases[0].Rounds[0].TaskRounds[0];
        finalTaskRound.Groups.Length.Should().Be(2);
        finalTaskRound.State.Should().Be(TaskRoundState.Complete);
        final.Penalties.Should().ContainSingle();
    }
}
