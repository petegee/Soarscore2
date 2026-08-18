// The `competitions` read model — kanban/completed/create-competition-steel-thread-plan.md
// WI-1, LADR-0001 §3/§4.3. One of the four read models the ADR permits; it
// exists solely so a list view can show every competition (name, location,
// dates, class) without folding a stream per row. Mirrors People/PeopleProjection.cs.
//
// A State column was deliberately excluded at first — every row's state was
// identically "created", since only CompetitionCreated was producible, and a
// column with no variance justifies nothing. task-round-lifecycle.md WI-8
// added it once PhaseDrawn and Finalised gave it two more values to hold.

using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>
/// The projected row for one competition. A read-side denormalisation of
/// <see cref="Competition"/> — nothing here is authoritative; GetCompetition
/// (WI-3) resolves by folding the stream, never from this document.
/// <see cref="ClassName"/> and <see cref="ClassContentHash"/> are denormalised
/// from <see cref="AdoptedRules"/> at creation, so a list view never needs to
/// fold every stream just to show what class each competition runs.
/// </summary>
public sealed record CompetitionSummary(
    CompetitionId Id,
    string Name,
    string Location,
    DateOnly StartDate,
    DateOnly EndDate,
    string ClassName,
    string ClassContentHash,
    /// <summary>
    /// "created" | "drawn" | "finalised" — a list-view label, not a lifecycle
    /// enum: the authoritative state is the folded stream, and a task-round
    /// reopening after finalisation does not walk this back. A plain string
    /// for the same reason Draw.Status is one — no doc source states the set.
    /// </summary>
    string State);
