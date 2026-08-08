// RoundingSupport — extracted per docs/plans/capture-a-score-steel-thread-plan.md
// finding 4. ApplyRounding was a private method duplicated identically in
// FlightSelector.cs and NormalisationEngine.cs; Entry.CaptureMeasurement
// (Soarscore.Domain.Entries) needed a third copy, so it is a shared internal
// helper instead. No behaviour change — both prior call sites are unchanged
// in effect, only in where the code lives.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

internal static class RoundingSupport
{
    public static decimal ApplyRounding(decimal value, Rounding rounding) =>
        rounding.Mode switch
        {
            RoundingMode.Truncate => Truncate(value, rounding.Precision),
            RoundingMode.HalfUp => HalfUp(value, rounding.Precision),
            RoundingMode.Ceiling => Ceiling(value, rounding.Precision),
            _ => value,
        };

    private static decimal Truncate(decimal value, decimal precision)
    {
        decimal factor = 1m / precision;
        return Math.Truncate(value * factor) / factor;
    }

    private static decimal HalfUp(decimal value, decimal precision)
    {
        decimal factor = 1m / precision;
        return Math.Round(value * factor, MidpointRounding.AwayFromZero) / factor;
    }

    private static decimal Ceiling(decimal value, decimal precision)
    {
        decimal factor = 1m / precision;
        return Math.Ceiling(value * factor) / factor;
    }
}
