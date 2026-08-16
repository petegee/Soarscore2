// docs/plans/class-definition-adoption-steel-thread-plan.md WI-4. Deviates
// from the WI-6 handler template (command-side-steel-thread-plan.md: read
// stream -> fold -> decide -> append Exact(version)) because there is no
// prior stream to read and no decide function to call: creation only, and
// creation is idempotent by design (ClassDefinitionEvents.cs: "republishing
// identical content targets the same stream and is a safe no-op").
//
// Pipeline, in order (LADR-0002 §4, ADR-0002 §5):
//   1. ClassDefinitionIngestion.CheckLimits — bounds how much work Validate
//      itself does on adversarial input (WI-1).
//   2. ClassDefinitionValidation.Validate — the sixteen adoption checks (WI-2).
//   3. ClassDefinitionHashing.ComputeContentHash — identity is the content
//      hash, not a minted id (PublishedClassDefinition.cs: "there is no
//      ClassDefinitionId to mint").
//   4. ClassDefinitionStreamId.From(hash) — the deterministic Marten stream key.
//   5. Append ClassDefinitionPublished with ExpectedVersion.NoStream.
//   6. An append failure with code "eventStore.streamAlreadyExists" succeeds
//      anyway with the same hash: identical content already published is a
//      safe no-op at the domain level. Any OTHER append failure still
//      propagates as Result.Failure.

using Soarscore.Application.Shared.CompetitionClasses;
using Soarscore.Domain;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.CompetitionClasses;

public sealed record PublishClassDefinition(ClassDefinition Definition) : ICommand<string>;

public sealed class PublishClassDefinitionHandler(IEventStore eventStore, IClock clock) : ICommandHandler<PublishClassDefinition, string>
{
    public async Task<Result<string>> HandleAsync(PublishClassDefinition command, CancellationToken cancellationToken)
    {
        var limitDefects = ClassDefinitionIngestion.CheckLimits(command.Definition);
        if (limitDefects.Count > 0)
        {
            return Result<string>.Failure("class-definition.ingestion.limitsExceeded", "The definition exceeds one or more ingestion limits.", limitDefects);
        }

        var defects = ClassDefinitionValidation.Validate(command.Definition);
        if (defects.Count > 0)
        {
            return Result<string>.Failure("class-definition.invalid", "The definition failed one or more adoption checks.", defects);
        }

        var hash = ClassDefinitionHashing.ComputeContentHash(command.Definition);
        var streamId = ClassDefinitionStreamId.From(hash);
        var published = new ClassDefinitionPublished(hash, command.Definition, clock.UtcNow);

        var append = await eventStore.AppendAsync(streamId, ExpectedVersion.NoStream, [published], cancellationToken);
        if (append.IsSuccess || append.Code == "eventStore.streamAlreadyExists")
        {
            return Result<string>.Success(hash);
        }

        return Result<string>.Failure(append.Code!, append.Message!, append.Defects);
    }
}
