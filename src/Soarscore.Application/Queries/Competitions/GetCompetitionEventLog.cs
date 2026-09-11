// kanban/in-progress/competition-event-log-endpoint.md WI-2. The trust model's
// audit view: the immutable event log, read as JSON. Not a fold into an
// aggregate — deliberately the *log*, every event in order, so questions
// GET /competition cannot answer (e.g. why a draw was rejected — DrawRejected
// events are invisible to that fold) are answerable here.
//
// Scope (owner decisions, 2026-09-11): the competition stream plus every entry
// stream. People and class-definition streams stay out; person names are
// resolved through IPeopleQuery only to label events. ReadAllAsync is
// deliberately not used — the log is assembled per stream, ordered by stream
// version within each, competition stream first. A global cross-stream
// timeline is not reconstructible (entry-scoped events carry no timestamp),
// so the response groups events under their stream rather than pretending a
// global order.
//
// Ids: query-by-id must fold the stream (high-level-architecture.md) — the
// competition stream is folded here for existence and the competitor map, and
// every other stream is read raw. No read model is consulted for anything but
// the cross-stream lookups a single stream cannot answer (LADR-0001 §3): which
// entry streams exist (IEntryQuery) and person names (IPeopleQuery).

using System.Collections.Immutable;
using System.Text.Json;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>The GET /competition-event-log response shape.</summary>
public sealed record CompetitionEventLogView(
    CompetitionId Id,
    string Name,
    ImmutableArray<EventLogStreamView> Streams);

/// <summary>
/// One stream's events. <see cref="Kind"/> is <c>competition</c> or
/// <c>entry</c>; <see cref="Label"/> names the entry stream (competitor and
/// task-round) and is null for the competition stream. Events are in stream
/// version order — there is no cross-stream timeline, because entry-scoped
/// events carry no timestamp; the grouping is the honest statement of that.
/// </summary>
public sealed record EventLogStreamView(
    Guid StreamId,
    string Kind,
    string? Label,
    ImmutableArray<EventLogEventView> Events);

/// <summary>
/// One event. <see cref="Version"/> is its position in its own stream
/// (1-based). <see cref="Summary"/> is null where a summary adds nothing —
/// including for event kinds this reader predates, which degrade to name-only.
/// <see cref="Payload"/> is the event's full JSON, present only when the query
/// asked for it.
/// </summary>
public sealed record EventLogEventView(
    long Version,
    string Name,
    string? Summary,
    JsonElement? Payload);

/// <param name="IncludePayload">Opt-in — compact (name + summary) by default; true adds the full event payload.</param>
public readonly record struct GetCompetitionEventLog(CompetitionId Id, bool IncludePayload = false) : IQuery<CompetitionEventLogView>;

public sealed class GetCompetitionEventLogHandler(
    IEventStore eventStore,
    IEntryQuery entryQuery,
    IPeopleQuery peopleQuery) : IQueryHandler<GetCompetitionEventLog, CompetitionEventLogView>
{
    public async Task<Result<CompetitionEventLogView>> HandleAsync(GetCompetitionEventLog query, CancellationToken cancellationToken)
    {
        // Existence and the competitor map come from the fold, never a read
        // model — the same competition.notFound CompetitionLoader produces.
        var competitionRead = await eventStore.ReadStreamAsync(query.Id.Value, 0, cancellationToken);
        if (competitionRead.IsFailure)
        {
            return Result<CompetitionEventLogView>.Failure(competitionRead.Code!, competitionRead.Message!, competitionRead.Defects);
        }

        var competitionEvents = competitionRead.Value;
        if (competitionEvents.Count == 0)
        {
            return Result<CompetitionEventLogView>.Failure(
                "competition.notFound", $"No competition found with id {query.Id}.");
        }

        var competition = competitionEvents.Aggregate(
            (Competition?)null, (current, e) => Competition.Apply(current, (CompetitionEvent)e))!;

        // CompetitorId → display label. The competitor number is the fold's;
        // the name is the person's — resolved in one batch, missing names
        // degrading to the number.
        var numbersByCompetitor = competition.Competitors.ToDictionary(c => c.Id, c => c.CompetitorNumber);
        var namesByPerson = (await peopleQuery.FindByIdsAsync(
                competition.Competitors.Select(c => c.PersonRef).ToImmutableArray(), cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);
        var names = new EventLogNames(competition.Competitors.ToDictionary(
            c => c.Id,
            c => namesByPerson.TryGetValue(c.PersonRef, out var name)
                ? $"{name} (#{c.CompetitorNumber})"
                : $"competitor #{c.CompetitorNumber}"));

        // Entry streams, in contest order — the entry_index is the one place
        // "which entry streams exist" is answerable (LADR-0001 §3); the
        // competition fold supplies the competitor numbers FindAsync cannot.
        var entries = await entryQuery.FindAsync(query.Id, null, null, null, null, null, cancellationToken);
        var orderedEntries = entries
            .OrderBy(e => e.PhaseOrdinal)
            .ThenBy(e => e.RoundOrdinal)
            .ThenBy(e => e.TaskRoundOrdinal)
            .ThenBy(e => numbersByCompetitor.GetValueOrDefault(e.CompetitorRef, int.MaxValue))
            .ThenBy(e => e.Id.Value)
            .ToImmutableArray();

        var streams = ImmutableArray.CreateBuilder<EventLogStreamView>();
        streams.Add(StreamView(
            query.Id.Value, "competition", null, competitionEvents, query.IncludePayload, names));

        foreach (var entry in orderedEntries)
        {
            var entryRead = await eventStore.ReadStreamAsync(entry.Id.Value, 0, cancellationToken);
            if (entryRead.IsFailure)
            {
                return Result<CompetitionEventLogView>.Failure(entryRead.Code!, entryRead.Message!, entryRead.Defects);
            }

            streams.Add(StreamView(
                entry.Id.Value, "entry", LabelFor(entry, names), entryRead.Value, query.IncludePayload, names));
        }

        return Result<CompetitionEventLogView>.Success(
            new CompetitionEventLogView(query.Id, competition.Name, streams.ToImmutable()));
    }

    private EventLogStreamView StreamView(
        Guid streamId,
        string kind,
        string? label,
        IReadOnlyList<IDomainEvent> events,
        bool includePayload,
        EventLogNames names)
    {
        var builder = ImmutableArray.CreateBuilder<EventLogEventView>(events.Count);
        for (var i = 0; i < events.Count; i++)
        {
            var @event = events[i];
            builder.Add(new EventLogEventView(
                i + 1,
                EventName.Of(@event),
                EventLogSummariser.Summarise(@event, names),
                includePayload ? Payload(@event) : null));
        }

        return new EventLogStreamView(streamId, kind, label, builder.MoveToImmutable());
    }

    private static JsonElement Payload(IDomainEvent @event)
    {
        // Serialised through the event's union base — the declared type is what
        // carries the [JsonPolymorphic] discriminator, so this is the path that
        // emits "$kind", matching how the Domain's own JSON round-trips events.
        return JsonSerializer.SerializeToElement(@event, @event.GetType().BaseType!, SoarscoreEventJson.Options);
    }

    private static string LabelFor(EntrySummary entry, EventLogNames names) =>
        $"{names.Competitor(entry.CompetitorRef)} — task-round {entry.PhaseOrdinal}.{entry.RoundOrdinal}.{entry.TaskRoundOrdinal}"
        + (entry.Role is ReflightRole.Original ? "" : $" ({entry.Role})");
}