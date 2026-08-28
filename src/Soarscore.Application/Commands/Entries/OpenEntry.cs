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
//
// Destination-aware (kanban/in-progress/reflight-aggregate-destination.md
// WI-2): OpenEntry grows the optional CountsForRoundOrdinal/Reason pair (D5 —
// the reflight-groups precedent: additive optional data on the existing
// command). The reflight-role branch of the guard becomes
// destination-aware — a live reflight-role entry blocks only a second open
// for the SAME destination (a different-destination second make-up is the
// comp-135 shape and is allowed) — and a D8 destination-conflict check runs
// when counts-for is set: one extra index query for the competitor in the
// destination round, refusing a LIVE entry of theirs in the destination
// round's matching task-round (a make-up for a round the pilot also flew is
// the unwitnessed shape D3 refuses). Streams are loaded for live/annulled
// truth as before — EntrySummary stays coordinate-only (trap 9), and the
// addressed task-round's TaskRef is read from the Competition aggregate this
// handler already had to load.

using Soarscore.Application.Shared.Competitions;
using Soarscore.Application.Shared.Entries;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Commands.Entries;

// kanban/in-progress/reflight-aggregate-destination.md WI-2 (D5). The
// CountsForRoundOrdinal/Reason pair is additive and optional — null counts-for
// means the entry's own round, so every existing caller is unchanged. Reason
// is required exactly when CountsForRoundOrdinal is set (D4); the Competition
// decide owns that rule, not this record.
public sealed record OpenEntry(
    CompetitionId CompetitionRef, int PhaseOrdinal, int RoundOrdinal,
    int TaskRoundOrdinal, GroupId GroupRef, CompetitorId CompetitorRef,
    ReflightRole Role = ReflightRole.Original,
    int? CountsForRoundOrdinal = null, string? Reason = null) : ICommand<EntryId>;

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
        // WI-5), made destination-aware by reflight-aggregate-destination.md
        // WI-2. Load the stream for every entry the index returns for this
        // competitor+task-round (not just Original-role ones — the index
        // carries Role but live/annulled and counts-for need a stream load,
        // the existing stance; EntrySummary stays coordinate-only, trap 9).
        // Then:
        //   Original open: any live entry of ANY role blocks (alreadyOpen) —
        //     VERBATIM (trap 2's law: a make-up must never be openable before
        //     the competitor's Original in the same task-round).
        //   Entitled/Filler open: a live reflight-role entry blocks only for
        //   the SAME destination (reflightAlreadyOpen — a competitor not
        //   allocated the new attempt is not entitled to another working
        //   time, F3K.9.6 / 5.5.11.6 iv), each side resolved to the entry's
        //   own round when its counts-for is null; a different-destination
        //   second reflight-role open is the comp-135 shape and is allowed;
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
                var existingDestination = loadedEntry.Value.Entry.CountsForRoundOrdinal ?? command.RoundOrdinal;
                var requestedDestination = command.CountsForRoundOrdinal ?? command.RoundOrdinal;
                if (existingDestination == requestedDestination)
                {
                    return Result<EntryId>.Failure(
                        "openEntry.reflightAlreadyOpen",
                        $"This competitor already has a live reflight-role entry counting for round {existingDestination} in this task-round; "
                        + "a competitor who was not allocated the new attempt is not entitled to another working time.");
                }
            }
        }

        // The D8 destination-conflict check: when the command carries a
        // counts-for round, one extra index query for the competitor in that
        // round; a LIVE entry of theirs in the destination round's matching
        // task-round refuses the open — a make-up for a round the pilot also
        // flew is exactly the unwitnessed shape D3 refuses. Streams are
        // loaded for liveness, so annulled entries never block (standing
        // annulment stance). EntrySummary carries no task, so the matching
        // task-round is resolved through the Competition aggregate this
        // handler already loaded: the destination round's task-round with the
        // same TaskRef as the addressed one (TaskRound.TaskRef is the task's
        // Code — "the only stable handle"). If the destination round does not
        // exist, or has no task-round for this task, the check is skipped —
        // the decide's destinationNotFound/destinationNotEarlier and the
        // scoring side's D7 validations are the braces for those shapes.
        if (command.CountsForRoundOrdinal is { } destinationRound)
        {
            var phase = competition.Phases.FirstOrDefault(p => p.Ordinal == command.PhaseOrdinal);
            var addressedTaskRound = phase?.Rounds
                .FirstOrDefault(r => r.Ordinal == command.RoundOrdinal)?.TaskRounds
                .FirstOrDefault(tr => tr.Ordinal == command.TaskRoundOrdinal);
            var destinationTaskRound = phase?.Rounds
                .FirstOrDefault(r => r.Ordinal == destinationRound)?.TaskRounds
                .FirstOrDefault(tr => tr.TaskRef == addressedTaskRound?.TaskRef);

            if (destinationTaskRound is not null)
            {
                var destinationEntries = await entryQuery.FindAsync(
                    command.CompetitionRef,
                    command.PhaseOrdinal,
                    destinationRound,
                    destinationTaskRound.Ordinal,
                    groupRef: null,
                    command.CompetitorRef,
                    cancellationToken);

                foreach (var destinationEntry in destinationEntries)
                {
                    var loadedDestinationEntry = await EntryLoader.LoadAsync(eventStore, destinationEntry.Id, cancellationToken);
                    if (loadedDestinationEntry.IsFailure)
                    {
                        return Result<EntryId>.Failure(
                            loadedDestinationEntry.Code!, loadedDestinationEntry.Message!, loadedDestinationEntry.Defects);
                    }

                    if (loadedDestinationEntry.Value.Entry.Annulment is null)
                    {
                        return Result<EntryId>.Failure(
                            "openEntry.reflightDestinationTaken",
                            $"This competitor already has a live entry in round {destinationRound}'s task-round for this task — "
                            + "a make-up cannot count for a round the competitor also flew.");
                    }
                }
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
            clock.UtcNow,
            command.CountsForRoundOrdinal,
            command.Reason);
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
