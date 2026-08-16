// MeasurementDigest — kanban/completed/scoring-steel-thread-plan.md WI-1.
//
// Resolves the effective value of every Measurement on a Flight: the most
// recent Amendment's NewValue by At, or the original Measurement.Value when
// no amendments exist. Ties on At resolve to the last-appended amendment —
// the event log's own order is the tiebreak, since two amendments bearing the
// same instant are still ordered facts.
//
// This is the only place amendment resolution happens (finding 2): nothing
// else in the tree computes an effective value from a Measurement's
// Amendments.

using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

public static class MeasurementDigest
{
    /// <summary>The effective, amendment-resolved measurements for one flight.</summary>
    public static ResolvedMeasurements Resolve(Flight flight)
    {
        var metrics = new Dictionary<string, MeasuredValue>();

        foreach (var measurement in flight.Measurements)
        {
            metrics[measurement.Metric] = EffectiveValue(measurement);
        }

        return new ResolvedMeasurements(metrics);
    }

    private static MeasuredValue EffectiveValue(Measurement measurement)
    {
        if (measurement.Amendments.IsDefaultOrEmpty)
            return measurement.Value;

        var latest = measurement.Amendments[0];

        for (int i = 1; i < measurement.Amendments.Length; i++)
        {
            var candidate = measurement.Amendments[i];
            if (candidate.At >= latest.At)
                latest = candidate;
        }

        return latest.NewValue;
    }
}
