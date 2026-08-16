// The `entry_index` read model — kanban/completed/capture-a-score-steel-thread-plan.md
// WI-7, LADR-0001 §3/§4.3. One of exactly four read models the ADR permits;
// it exists solely so "which Entry streams exist where" can be answered
// without folding every Entry stream in the log, which no single stream can
// answer for itself (Domain/Entries/Entry.cs). Mirrors People/PeopleProjection.cs's
// PersonSummary and Competitions/CompetitionSummary.cs.
//
// The coordinate and nothing else — no flight count, no capture timestamp, no
// annulled flag. LADR-0001 §3 is explicit that scores are never projected;
// every one of those fields would make the projection fire on the
// highest-volume event in the system (MeasurementCaptured) to store something
// a stream load already answers.
//
// No unique index: two Entries can legitimately share a (task-round,
// competitor) pair once reflights exist, so the uniqueness PersonSummary.Email
// gets would be wrong here.

using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;

namespace Soarscore.Application.Queries.Entries;

/// <summary>
/// The projected row for one Entry. A read-side denormalisation — nothing
/// here is authoritative; GetEntry (a future work item, mirroring GetCompetition)
/// resolves by folding the stream, never from this document.
/// </summary>
public sealed record EntrySummary(
    EntryId Id,
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId GroupRef,
    CompetitorId CompetitorRef,
    ReflightRole Role);
