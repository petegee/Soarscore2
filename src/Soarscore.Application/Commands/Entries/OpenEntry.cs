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
//
// Annulment-aware (kanban/in-progress/annul-and-penalise-the-second-entry-thread.md
// WI-6): the index deliberately carries "the coordinate and nothing else"
// (EntrySummary.cs's header) — no annulled flag, because a stream load already
// answers that question. So when the index reports an Original-role Entry, the
// handler loads that stream (typically one) and refuses only if it is live —
// i.e. not annulled. The F3F.1.5 provisional shape (annul the first attempt,
// re-open a second) is exactly what this permits. The index stays
// coordinate-only; EntryProjectionTests's assertion that EntryAnnulled leaves
// the summary unchanged stays true.
//
// Reflight-aware (kanban/in-progress/reflight-groups.md WI-5): OpenEntry
// gains an optional ReflightRole (default Original — every existing caller is
// unchanged), and the guard loads the stream for every entry the index returns
// for the coordinator, whatever their role. Original opens block on any live
// entry; Entitled/Filler opens block only on a live reflight-role entry, so
// the ORIGINAL + one reflight-role pairing is the permitted reflight shape.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

public sealed record OpenEntry(
    CompetitionId CompetitionRef, int PhaseOrdinal, int RoundOrdinal,
    int TaskRoundOrdinal, GroupId GroupRef, CompetitorId CompetitorRef,
    ReflightRole Role = ReflightRole.Original) : ICommand<EntryId>;

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

        // The reflight-aware guard (kanban/in-progress/reflight-groups.md
        // WI-5). Load the stream for every entry the index returns for this
        // competitor+task-round (not just Original-role ones — the index
        // carries Role but live/annulled needs a stream load, the existing
        // stance). Then:
        //   Original open: any live entry of ANY role blocks (alreadyOpen).
        //   Entitled/Filler open: any live reflight-role entry blocks
        //   (reflightAlreadyOpen — a competitor not allocated the new attempt
        //   is not entitled to another working time, F3K.9.6 / 5.5.11.6 iv);
        //   a live Original does NOT block — that is the reflight shape.
        // Annulled entries of any role never block (unchanged).
        foreach (var existingEntry in existing)
        {
            var loadedEntry = await EntryLoader.LoadAsync(eventStore, existingEntry.Id, cancellationToken);
            if (loadedEntry.IsFailure)
            {
                return Result<EntryId>.Failure(loadedEntry.Code!, loadedEntry.Message!, loadedEntry.Defects);
            }

            if (loadedEntry.Value.Entry.Annulment is not null)
            {
                continue;
            }

            if (command.Role == ReflightRole.Original)
            {
                return Result<EntryId>.Failure(
                    "openEntry.alreadyOpen",
                    "An entry is already open for this competitor in this task-round.");
            }

            if (existingEntry.Role is ReflightRole.Entitled or ReflightRole.Filler)
            {
                return Result<EntryId>.Failure(
                    "openEntry.reflightAlreadyOpen",
                    "This competitor already has a live reflight-role entry in this task-round; "
                    + "a competitor who was not allocated the new attempt is not entitled to another working time.");
            }
        }

        var id = EntryId.New();
        var decision = competition.OpenEntry(
            id,
            command.PhaseOrdinal,
            command.RoundOrdinal,
            command.TaskRoundOrdinal,
            command.GroupRef,
            command.CompetitorRef,
            command.Role,
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
