// kanban/completed/create-competition-steel-thread-plan.md WI-1's pass-through
// property: CompetitionProjection.Apply's default arm (`_ => current`) must
// hold for every one of the ten CompetitionEvent subtypes other than
// CompetitionCreated, not just the one hand-picked example
// CompetitionProjectionTests.cs already covers. Complements that file's fixed
// example the same way ClassDefinitionProjectionPropertyTests.cs complements
// ClassDefinitionProjectionTests.cs.

using System.Collections.Immutable;
using CsCheck;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;

namespace Soarscore.Application.Tests.Queries.Competitions;

public class CompetitionProjectionPropertyTests
{
    // One minimal-but-valid instance per non-CompetitionCreated event type —
    // Gen.OneOfConst samples across these rather than needing a full
    // generator per event shape, per WI-1's own suggestion.
    private static readonly CompetitionEvent[] OtherEventTypes = BuildOtherEventTypes();

    private static readonly Gen<CompetitionEvent> AnyOtherEvent = Gen.OneOfConst(OtherEventTypes);

    private static readonly Gen<CompetitionSummary> AnySummary =
        from id in Gen.Guid
        from name in Gen.String[1, 40]
        from location in Gen.String[1, 40]
        from startOffset in Gen.Int[0, 3650]
        from endOffset in Gen.Int[0, 3650]
        from className in Gen.String[1, 40]
        from classContentHash in Gen.String[1, 64]
        select new CompetitionSummary(
            new CompetitionId(id),
            name,
            location,
            new DateOnly(2020, 1, 1).AddDays(startOffset),
            new DateOnly(2020, 1, 1).AddDays(endOffset),
            className,
            classContentHash);

    [Fact]
    public void Any_out_of_scope_event_type_against_any_summary_leaves_it_unchanged()
    {
        (from summary in AnySummary from @event in AnyOtherEvent select (summary, @event))
        .Sample(t => CompetitionProjection.Apply(t.summary, t.@event) == t.summary);
    }

    private static CompetitionEvent[] BuildOtherEventTypes()
    {
        var at = DateTimeOffset.UtcNow;
        var competitor = new Competitor
        {
            Id = CompetitorId.New(),
            PersonRef = PersonId.New(),
            CompetitorNumber = 1,
            RegisteredAt = at,
        };
        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [CompetitorId.New()] };
        var taskRound = new TaskRound { Ordinal = 1, State = TaskRoundState.Drawn, TaskRef = "A", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = at, Status = "Accepted" };
        var penalty = new Penalty { InfractionType = "late launch", Scope = PenaltyScope.TaskRound };
        var rulesAmendment = new RulesAmendment
        {
            Definition = Soarscore.SeedData.Corpus.All[0].Definition,
            Reason = "corrected landing table",
            By = "CD",
            At = at,
        };
        var parameterBinding = new ParameterBinding
        {
            ParameterName = "workingTime",
            BoundValue = MeasuredValue.Of(600m),
            By = "CD",
            At = at,
        };
        var finalisation = new Finalisation
        {
            Scope = FinalisationScope.Phase,
            Revision = 1,
            By = "CD",
            At = at,
            DeclaredResults =
            [
                new DeclaredResult
                {
                    CompetitorRef = competitor.Id,
                    Aggregate = 987.5m,
                    Placing = 1,
                    Promoted = true,
                },
            ],
        };

        return
        [
            new CompetitorRegistered(competitor, at),
            new CompetitorWithdrawn(competitor.Id, at),
            new PhaseDrawn(1, PhaseType.Preliminary, draw, [round], at),
            new ReflightGroupAppended(1, 1, 1, group, at),
            new TaskRoundCompleted(1, 1, 1, at),
            new TaskRoundAnnulled(1, 1, 1, "wind out of limits", at),
            new RulesAmended(rulesAmendment),
            new ParameterBound(parameterBinding),
            new Finalised(finalisation),
            new PenaltyRecorded(penalty),
        ];
    }
}
