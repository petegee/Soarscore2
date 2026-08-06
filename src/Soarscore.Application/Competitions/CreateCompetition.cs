// docs/plans/create-competition-steel-thread-plan.md WI-3. A new handler
// shape, not the WI-6 read-fold-decide-append template (RegisterPerson.cs) or
// the idempotent-creation template (PublishClassDefinition.cs): this is the
// first command whose decide function needs data folded from a *different*
// aggregate's stream — the published class-definition library — before it
// can run.
//
// This is a cross-aggregate READ (folding PublishedClassDefinition's stream
// to copy its content into AdoptedRules), not a cross-aggregate write or a
// read-check-write against Competition's own invariants — LADR-0001 §4.4
// forbids only the latter. Copying the class definition at creation is
// exactly what AdoptedRules being "a complete copy... taken at creation"
// (aggregate-roots.md §3) requires; there is no foreign-key alternative in an
// event-sourced model.
//
// evaluatorVersion has no existing source anywhere in the codebase (confirmed
// by grep) — EvaluatorVersion below is a simple stable literal for this
// thread; choosing its real long-term source is a separate decision.

using Soarscore.Application.CompetitionClasses;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Competitions;

public sealed record CreateCompetition(
    string Name,
    string Location,
    DateOnly StartDate,
    DateOnly EndDate,
    string ClassContentHash) : ICommand<CompetitionId>;

public sealed class CreateCompetitionHandler(IEventStore eventStore, IClock clock) : ICommandHandler<CreateCompetition, CompetitionId>
{
    private const string EvaluatorVersion = "1";

    public async Task<Result<CompetitionId>> HandleAsync(CreateCompetition command, CancellationToken cancellationToken)
    {
        var classStreamId = ClassDefinitionStreamId.From(command.ClassContentHash);
        var read = await eventStore.ReadStreamAsync(classStreamId, 0, cancellationToken);
        if (read.IsFailure)
        {
            return Result<CompetitionId>.Failure(
                "createCompetition.classDefinitionNotFound",
                $"No class definition found with content hash {command.ClassContentHash}.");
        }

        var events = read.Value;
        if (events.Count == 0)
        {
            return Result<CompetitionId>.Failure(
                "createCompetition.classDefinitionNotFound",
                $"No class definition found with content hash {command.ClassContentHash}.");
        }

        var folded = events.Aggregate(
            (PublishedClassDefinition?)null,
            (current, e) => PublishedClassDefinition.Apply(current, (ClassDefinitionEvent)e));
        if (folded is null)
        {
            return Result<CompetitionId>.Failure(
                "createCompetition.classDefinitionNotFound",
                $"No class definition found with content hash {command.ClassContentHash}.");
        }

        if (folded.RetiredAt is not null)
        {
            return Result<CompetitionId>.Failure(
                "createCompetition.classDefinitionRetired",
                $"Class definition {command.ClassContentHash} has been retired.");
        }

        var id = CompetitionId.New();
        var adoptedRules = new AdoptedRules
        {
            Definition = folded.Definition,
            SourceClassId = folded.ContentHash,
            SourceVersion = folded.Definition.Version,
            AdoptedAt = clock.UtcNow,
        };

        var decision = Competition.Decide(
            id, command.Name, command.Location, command.StartDate, command.EndDate, EvaluatorVersion, adoptedRules, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<CompetitionId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<CompetitionId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<CompetitionId>.Success(id);
    }
}
