// The `entry_index` read model's fold — kanban/completed/capture-a-score-steel-thread-plan.md
// WI-7, LADR-0001 §4.3. Plain static function, portable if the store is ever
// swapped; the Marten IProjection shim wrapping it is Infrastructure's
// concern (WI-9, mirroring Competitions/CompetitionSummaryProjection.cs).

using Soarscore.Domain.Entries;

namespace Soarscore.Application.Queries.Entries;

public static class EntryProjection
{
    /// <summary>
    /// Folds one <see cref="EntryEvent"/> onto the current summary, or creates
    /// it from <see cref="EntryOpened"/>. Every other subtype passes through
    /// unchanged — LADR-0001 §3 is explicit that scores are never projected,
    /// and <see cref="EntrySummary"/> carries the coordinate only, so nothing
    /// past the creation event has anything to add to it. Same shape as
    /// <see cref="Competitions.CompetitionProjection.Apply"/>'s deliberate
    /// pass-through default arm, and for the same reason: an Inline projection
    /// that throws on a recognised-but-uninteresting event type would crash
    /// for every Entry as soon as FlightOpened/MeasurementCaptured start
    /// landing on its stream, which this thread's own commands do (WI-8).
    /// </summary>
    public static EntrySummary? Apply(EntrySummary? current, EntryEvent @event) =>
        @event switch
        {
            EntryOpened e => new EntrySummary(
                e.Id, e.CompetitionRef, e.PhaseOrdinal, e.RoundOrdinal, e.TaskRoundOrdinal, e.GroupRef, e.CompetitorRef, e.Role),
            _ => current,
        };
}
