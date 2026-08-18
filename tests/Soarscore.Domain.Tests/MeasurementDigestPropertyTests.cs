using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property test for <see cref="MeasurementDigest.Resolve"/> — WI-5 invariant 1
/// (kanban/completed/scoring-steel-thread-plan.md): the effective value of a
/// Measurement is its most recent Amendment's NewValue by At, ties broken by
/// last-appended, and the original Value when there are no amendments.
/// </summary>
public class MeasurementDigestPropertyTests
{
    private static readonly DateTimeOffset Base = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly Gen<Amendment> AmendmentGen =
        // A narrow offset range (0..20 s) deliberately raises the chance of
        // colliding At values, so the tie-break half of the invariant
        // ("last appended" wins, not just "greatest At") gets exercised, not
        // just the strict-inequality half.
        from atOffsetSeconds in Gen.Int[0, 20]
        from number in Gen.Int[0, 100_000].Select(i => i / 100m)
        select new Amendment
        {
            NewValue = MeasuredValue.Of(number),
            Reason = "correction",
            By = "scorer",
            At = Base.AddSeconds(atOffsetSeconds),
        };

    [Fact]
    public void Amendment_resolution_is_last_write_wins()
    {
        (from originalValue in Gen.Int[0, 100_000].Select(i => i / 100m)
         from amendments in AmendmentGen.Array[0, 8]
         select (originalValue, amendments))
        .Sample(t =>
        {
            var measurement = new Measurement
            {
                Metric = "flightTime",
                Value = MeasuredValue.Of(t.originalValue),
                CapturedAt = Base,
                Amendments = t.amendments.ToImmutableArray(),
            };
            var flight = new Flight { Sequence = 1, Measurements = ImmutableArray.Create(measurement) };

            var resolved = MeasurementDigest.Resolve(flight);

            // Independent oracle, not a re-statement of MeasurementDigest's own
            // loop: order candidates by At descending, then by their original
            // append index descending, and take the first — greatest At wins,
            // and among ties the one appended last (highest index) wins.
            var expected = t.amendments.Length == 0
                ? measurement.Value
                : t.amendments
                    .Select((a, i) => (Amendment: a, Index: i))
                    .OrderByDescending(x => x.Amendment.At)
                    .ThenByDescending(x => x.Index)
                    .First().Amendment.NewValue;

            resolved.Metrics["flightTime"].Should().Be(expected);
        });
    }
}
