// ss_entry-measurements-read: one entry's flights and measurements, folded
// from its stream. Mirrors Competitions/GetCompetition.cs (fold, never the
// entry_index read model) and People/GetPerson.cs (record struct bound via
// [AsParameters], EntryId.Parse above).
//
// Per-measurement fields follow the settled decisions: value is the
// MeasurementDigest effective value (latest-by-At, last-appended tiebreak),
// instrument is Measurement.EffectiveInstrument (last-appended restatement),
// original is the initial capture Value, amendments ride in stream order.
// Flights ascend by Sequence (the fold already holds that; sorted
// defensively), measurements keep capture order, empty flights included.
// Annulment is the full ruling or null; penalties are out of scope.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;

namespace Soarscore.Application.Queries.Entries;

/// <summary>One measurement's current value, capture, and correction history.</summary>
public sealed record EntryMeasurementView(
    string Metric,
    MeasuredValue Value,
    MeasuredValue Original,
    string? Instrument,
    DateTimeOffset CapturedAt,
    ImmutableArray<Amendment> Amendments);

/// <summary>One flight with every captured measurement, in capture order.</summary>
public sealed record EntryFlightView(
    int Sequence,
    ImmutableArray<EntryMeasurementView> Measurements);

/// <summary>The GET /entry response shape: the folded entry plus per-flight measurements.</summary>
public sealed record EntryView(
    EntryId Id,
    CompetitionId CompetitionRef,
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    GroupId GroupRef,
    CompetitorId CompetitorRef,
    ReflightRole Role,
    int? CountsForRoundOrdinal,
    string? Reason,
    Annulment? Annulment,
    ImmutableArray<EntryFlightView> Flights);

public readonly record struct GetEntry(EntryId Id) : IQuery<EntryView>;

public sealed class GetEntryHandler(IEventStore eventStore) : IQueryHandler<GetEntry, EntryView>
{
    public async Task<Result<EntryView>> HandleAsync(GetEntry query, CancellationToken cancellationToken)
    {
        var loaded = await EntryLoader.LoadAsync(eventStore, query.Id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<EntryView>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var entry = loaded.Value.Entry;

        var flights = entry.Flights
            .OrderBy(f => f.Sequence)
            .Select(flight =>
            {
                // The canonical amendment resolution (MeasurementDigest):
                // value follows latest-by-At, instrument follows
                // EffectiveInstrument. Resolved once per flight, not
                // re-derived per measurement.
                var resolved = MeasurementDigest.Resolve(flight);
                var measurements = flight.Measurements
                    .Select(m => new EntryMeasurementView(
                        m.Metric,
                        resolved.Metrics[m.Metric],
                        m.Value,
                        m.EffectiveInstrument,
                        m.CapturedAt,
                        m.Amendments))
                    .ToImmutableArray();
                return new EntryFlightView(flight.Sequence, measurements);
            })
            .ToImmutableArray();

        return Result<EntryView>.Success(new EntryView(
            entry.Id,
            entry.CompetitionRef,
            entry.PhaseOrdinal,
            entry.RoundOrdinal,
            entry.TaskRoundOrdinal,
            entry.GroupRef,
            entry.CompetitorRef,
            entry.Role,
            entry.CountsForRoundOrdinal,
            entry.Reason,
            entry.Annulment,
            flights));
    }
}
