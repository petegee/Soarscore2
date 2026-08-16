// kanban/completed/capture-a-score-steel-thread-plan.md WI-8. The first command in
// the repo whose handler appends to a NEW stream (a minted EntryId, keyed
// with ExpectedVersion.NoStream) after reading a DIFFERENT aggregate's
// stream to decide — Competition.OpenEntry (WI-2) owns every rule check, so
// the Competition read is required, not advisory.
//
// The second read, against IEntryQuery, is advisory rather than a
// concurrency arbiter — same class of race RegisterCompetitor.cs documents
// and accepts. The `entry_index` projection is Inline, so this is
// read-your-own-writes consistent; the residual race is two simultaneous
// opens for one pilot, which a single scorer at a single task-round does not
// produce. It is bookkeeping, not a rule (the plan's "What the rules do and
// do not say", silence 2) — a re-flight really is a second Entry for the
// same task-round/competitor, so the failure message below must not imply
// the rules forbid a second one, only that an Original one is already open.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

public sealed record OpenEntry(
    CompetitionId CompetitionRef, int PhaseOrdinal, int RoundOrdinal,
    int TaskRoundOrdinal, GroupId GroupRef, CompetitorId CompetitorRef) : ICommand<EntryId>;

public sealed class OpenEntryHandler(IEventStore eventStore, IEntryQuery entryQuery, IClock clock)
    : ICommandHandler<OpenEntry, EntryId>
{
    public async Task<Result<EntryId>> HandleAsync(OpenEntry command, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, command.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<EntryId>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var (competition, _) = loaded.Value;

        var existing = await entryQuery.FindAsync(
            command.CompetitionRef,
            command.PhaseOrdinal,
            command.RoundOrdinal,
            command.TaskRoundOrdinal,
            groupRef: null,
            command.CompetitorRef,
            cancellationToken);
        if (existing.Any(e => e.Role == ReflightRole.Original))
        {
            return Result<EntryId>.Failure(
                "openEntry.alreadyOpen",
                "An entry is already open for this competitor in this task-round.");
        }

        var id = EntryId.New();
        var decision = competition.OpenEntry(
            id,
            command.PhaseOrdinal,
            command.RoundOrdinal,
            command.TaskRoundOrdinal,
            command.GroupRef,
            command.CompetitorRef,
            clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result<EntryId>.Failure(decision.Code!, decision.Message!, decision.Defects);
        }

        var append = await eventStore.AppendAsync(id.Value, ExpectedVersion.NoStream, [decision.Value], cancellationToken);
        return append.IsFailure
            ? Result<EntryId>.Failure(append.Code!, append.Message!, append.Defects)
            : Result<EntryId>.Success(id);
    }
}
