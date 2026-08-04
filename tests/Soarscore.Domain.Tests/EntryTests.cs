using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Data-shape sanity suite for the Entry aggregate (aggregate-roots.md §4).
/// No business logic lives on Entry yet, so these are construction/equality
/// checks: a realistic Entry with several flights and measurements builds,
/// an amendment is carried alongside (not applied), an annulled Entry is
/// representable, and the two reflight roles are distinguishable.
/// </summary>
public class EntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Entry_with_several_flights_and_measurements_constructs()
    {
        var entry = new Entry
        {
            Id = EntryId.New(),
            WorkingTime = new TimeWindow { Start = Now, End = Now.AddMinutes(10) },
            GroupRef = GroupId.New(),
            CompetitorRef = CompetitorId.New(),
            Role = ReflightRole.Original,
            Flights = ImmutableArray.Create(
                new Flight
                {
                    Sequence = 1,
                    LaunchAt = Now.AddMinutes(1),
                    Measurements = ImmutableArray.Create(
                        new Measurement
                        {
                            Metric = "flightTime",
                            Value = MeasuredValue.Of(180m),
                            CapturedAt = Now.AddMinutes(4),
                        },
                        new Measurement
                        {
                            Metric = "landedInDefinedArea",
                            Value = MeasuredValue.Of(true),
                            CapturedAt = Now.AddMinutes(4),
                        }
                    ),
                },
                new Flight
                {
                    Sequence = 2,
                    LaunchAt = Now.AddMinutes(5),
                    Measurements = ImmutableArray.Create(
                        new Measurement
                        {
                            Metric = "flightTime",
                            Value = MeasuredValue.Of(210m),
                            CapturedAt = Now.AddMinutes(8),
                        },
                        new Measurement
                        {
                            Metric = "landedInDefinedArea",
                            Value = MeasuredValue.Of(false),
                            CapturedAt = Now.AddMinutes(8),
                        }
                    ),
                }
            ),
        };

        Assert.Equal(2, entry.Flights.Length);
        Assert.Equal(1, entry.Flights[0].Sequence);
        Assert.Equal(2, entry.Flights[0].Measurements.Length);
        Assert.Null(entry.Annulment);
        Assert.Empty(entry.Penalties);
    }

    [Fact]
    public void Measurement_carries_an_amendment_without_overwriting_the_original_value()
    {
        var measurement = new Measurement
        {
            Metric = "flightTime",
            Value = MeasuredValue.Of(180m),
            CapturedAt = Now,
            Amendments = ImmutableArray.Create(
                new Amendment
                {
                    NewValue = MeasuredValue.Of(182m),
                    Reason = "Timing card transcription error",
                    By = "CD",
                    At = Now.AddMinutes(30),
                }
            ),
        };

        Assert.Equal(180m, measurement.Value.Number);
        Assert.Single(measurement.Amendments);
        Assert.Equal(182m, measurement.Amendments[0].NewValue.Number);
    }

    [Fact]
    public void Annulled_entry_carries_reason_by_and_timestamp()
    {
        var entry = new Entry
        {
            Id = EntryId.New(),
            WorkingTime = new TimeWindow { Start = Now, End = Now.AddMinutes(10) },
            GroupRef = GroupId.New(),
            CompetitorRef = CompetitorId.New(),
            Role = ReflightRole.Original,
            Annulment = new Annulment
            {
                Reason = "Provisional re-flight under protest; jury ruled original stands",
                By = "Jury",
                At = Now.AddHours(1),
            },
            Flights = ImmutableArray.Create(
                new Flight
                {
                    Sequence = 1,
                    LaunchAt = Now.AddMinutes(1),
                    Measurements = ImmutableArray.Create(
                        new Measurement
                        {
                            Metric = "flightTime",
                            Value = MeasuredValue.Of(150m),
                            CapturedAt = Now.AddMinutes(4),
                        }
                    ),
                }
            ),
        };

        Assert.NotNull(entry.Annulment);
        Assert.Equal("Jury", entry.Annulment!.By);
    }

    [Fact]
    public void Reflight_roles_distinguish_entitled_from_filler_entries()
    {
        var groupRef = GroupId.New();

        var entitled = new Entry
        {
            Id = EntryId.New(),
            WorkingTime = new TimeWindow { Start = Now, End = Now.AddMinutes(10) },
            GroupRef = groupRef,
            CompetitorRef = CompetitorId.New(),
            Role = ReflightRole.Entitled,
            Flights = ImmutableArray.Create(
                new Flight
                {
                    Sequence = 1,
                    LaunchAt = Now.AddMinutes(1),
                    Measurements = ImmutableArray.Create(
                        new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(90m), CapturedAt = Now }
                    ),
                }
            ),
        };

        var filler = entitled with
        {
            Id = EntryId.New(),
            CompetitorRef = CompetitorId.New(),
            Role = ReflightRole.Filler,
        };

        Assert.Equal(ReflightRole.Entitled, entitled.Role);
        Assert.Equal(ReflightRole.Filler, filler.Role);
        Assert.Equal(entitled.GroupRef, filler.GroupRef);
        Assert.NotEqual(entitled.CompetitorRef, filler.CompetitorRef);
    }

    [Fact]
    public void Entries_with_identical_values_are_equal_records()
    {
        var id = EntryId.New();
        var groupRef = GroupId.New();
        var competitorRef = CompetitorId.New();
        var window = new TimeWindow { Start = Now, End = Now.AddMinutes(10) };
        var flights = ImmutableArray.Create(
            new Flight
            {
                Sequence = 1,
                LaunchAt = Now.AddMinutes(1),
                Measurements = ImmutableArray.Create(
                    new Measurement { Metric = "flightTime", Value = MeasuredValue.Of(120m), CapturedAt = Now }
                ),
            }
        );

        var a = new Entry
        {
            Id = id, WorkingTime = window, GroupRef = groupRef, CompetitorRef = competitorRef,
            Role = ReflightRole.Original, Flights = flights,
        };
        var b = new Entry
        {
            Id = id, WorkingTime = window, GroupRef = groupRef, CompetitorRef = competitorRef,
            Role = ReflightRole.Original, Flights = flights,
        };

        Assert.Equal(a, b);
    }
}
