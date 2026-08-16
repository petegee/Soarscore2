// The `competitions` read model — docs/plans/create-competition-steel-thread-plan.md
// WI-1, LADR-0001 §3/§4.3. One of the four read models the ADR permits; it
// exists solely so a list view can show every competition (name, location,
// dates, class) without folding a stream per row. Mirrors People/PeopleProjection.cs.
//
// Deliberately excludes a State column this thread — every row's state is
// identically "created" right now, since only CompetitionCreated is
// producible. Adding one before a second state exists would be a column with
// no variance to justify it.

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
    string ClassContentHash);
