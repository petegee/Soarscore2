using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.PublishedClassDefinition;
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
            CompetitionRef = CompetitionId.New(),
            PhaseOrdinal = 1,
            RoundOrdinal = 1,
            TaskRoundOrdinal = 1,
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

        entry.Flights.Length.Should().Be(2);
        entry.Flights[0].Sequence.Should().Be(1);
        entry.Flights[0].Measurements.Length.Should().Be(2);
        entry.Annulment.Should().BeNull();
        entry.Penalties.Should().BeEmpty();
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

        measurement.Value.Number.Should().Be(180m);
        measurement.Amendments.Should().ContainSingle();
        measurement.Amendments[0].NewValue.Number.Should().Be(182m);
    }

    [Fact]
    public void Annulled_entry_carries_reason_by_and_timestamp()
    {
        var entry = new Entry
        {
            Id = EntryId.New(),
            WorkingTime = new TimeWindow { Start = Now, End = Now.AddMinutes(10) },
            CompetitionRef = CompetitionId.New(),
            PhaseOrdinal = 1,
            RoundOrdinal = 1,
            TaskRoundOrdinal = 1,
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

        entry.Annulment.Should().NotBeNull();
        entry.Annulment!.By.Should().Be("Jury");
    }

    [Fact]
    public void Reflight_roles_distinguish_entitled_from_filler_entries()
    {
        var groupRef = GroupId.New();

        var entitled = new Entry
        {
            Id = EntryId.New(),
            WorkingTime = new TimeWindow { Start = Now, End = Now.AddMinutes(10) },
            CompetitionRef = CompetitionId.New(),
            PhaseOrdinal = 1,
            RoundOrdinal = 1,
            TaskRoundOrdinal = 1,
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

        entitled.Role.Should().Be(ReflightRole.Entitled);
        filler.Role.Should().Be(ReflightRole.Filler);
        filler.GroupRef.Should().Be(entitled.GroupRef);
        filler.CompetitorRef.Should().NotBe(entitled.CompetitorRef);
    }

    [Fact]
    public void Entries_with_identical_values_are_equal_records()
    {
        var id = EntryId.New();
        var competitionRef = CompetitionId.New();
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
            Id = id, WorkingTime = window, CompetitionRef = competitionRef,
            PhaseOrdinal = 1, RoundOrdinal = 1, TaskRoundOrdinal = 1,
            GroupRef = groupRef, CompetitorRef = competitorRef,
            Role = ReflightRole.Original, Flights = flights,
        };
        var b = new Entry
        {
            Id = id, WorkingTime = window, CompetitionRef = competitionRef,
            PhaseOrdinal = 1, RoundOrdinal = 1, TaskRoundOrdinal = 1,
            GroupRef = groupRef, CompetitorRef = competitorRef,
            Role = ReflightRole.Original, Flights = flights,
        };

        b.Should().Be(a);
    }
}
