// kanban/in-progress/should-level-minima-warn-dont-refuse.md WI-1.
//
// The hardness datum (GroupConstraint.MinEnforcement) is vocabulary +
// resolution + JSON only: absent behaves exactly as today (Shall), present
// resolves and round-trips. No decide reads it yet (WI-2), no seed sets it
// yet (WI-3).

using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

public class GroupConstraintHardnessTests
{
    private const string ParameterName = "minPerGroup";

    private static readonly ImmutableArray<Parameter> DeclaredParameters =
        [new Parameter { Name = ParameterName, Kind = MeasuredKind.Number, DefaultValue = MeasuredValue.Of(6m) }];

    private static TaskDefinition TaskWithGroup(GroupConstraint group) => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = [],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Group = group,
        Score = [],
    };

    private static ResolvedGroupConstraint Resolve(GroupConstraint group, Dictionary<string, MeasuredValue>? bindings = null) =>
        ParameterResolver.ResolveTask(TaskWithGroup(group), bindings ?? [], DeclaredParameters).Group!;

    // ---------------------------------------------------------- resolution

    [Fact]
    public void Absent_hardness_on_a_literal_minimum_resolves_to_Shall()
    {
        var resolved = Resolve(new GroupConstraint { MinPerGroup = 6 });

        resolved.MinPerGroup.Should().Be(6m);
        resolved.MinEnforcement.Should().Be(MinEnforcement.Shall);
    }

    [Fact]
    public void Should_hardness_on_a_literal_minimum_resolves_through()
    {
        var resolved = Resolve(new GroupConstraint { MinPerGroup = 6, MinEnforcement = MinEnforcement.Should });

        resolved.MinPerGroup.Should().Be(6m);
        resolved.MinEnforcement.Should().Be(MinEnforcement.Should);
    }

    [Fact]
    public void Absent_hardness_on_a_parameterised_minimum_resolves_to_Shall_with_the_bound_number()
    {
        var bindings = new Dictionary<string, MeasuredValue> { [ParameterName] = MeasuredValue.Of(8m) };

        var resolved = Resolve(new GroupConstraint { MinPerGroup = NumberOrParam.Param(ParameterName) }, bindings);

        resolved.MinPerGroup.Should().Be(8m);
        resolved.MinEnforcement.Should().Be(MinEnforcement.Shall);
    }

    [Fact]
    public void Should_hardness_on_a_parameterised_minimum_is_inherited_by_the_bound_number()
    {
        // The CD's bound number carries no verb of its own: the constraint's
        // hardness governs it, resolved here from the declared default.
        var resolved = Resolve(new GroupConstraint
        {
            MinPerGroup = NumberOrParam.Param(ParameterName),
            MinEnforcement = MinEnforcement.Should,
        });

        resolved.MinPerGroup.Should().Be(6m);
        resolved.MinEnforcement.Should().Be(MinEnforcement.Should);
    }

    // ------------------------------------------------------- JSON round-trip

    [Fact]
    public void Absent_hardness_is_omitted_from_canonical_JSON()
    {
        var json = JsonSerializer.Serialize(
            new GroupConstraint { MinPerGroup = 6 }, SoarscoreJson.Canonical);

        json.Should().NotContain("minEnforcement");
    }

    [Fact]
    public void Should_hardness_round_trips_through_canonical_JSON()
    {
        var group = new GroupConstraint { MinPerGroup = 6, MinEnforcement = MinEnforcement.Should };

        var json = JsonSerializer.Serialize(group, SoarscoreJson.Canonical);

        json.Should().Contain("\"minEnforcement\": \"Should\"");
        JsonSerializer.Deserialize<GroupConstraint>(json, SoarscoreJson.Ingestion)!
            .MinEnforcement.Should().Be(MinEnforcement.Should);
    }

    [Fact]
    public void An_old_payload_without_the_datum_reads_as_absent_hardness()
    {
        var reread = JsonSerializer.Deserialize<GroupConstraint>("{\"minPerGroup\": 6}", SoarscoreJson.Ingestion)!;

        reread.MinPerGroup.Should().Be(new NumberOrParam.Literal(6m));
        reread.MinEnforcement.Should().BeNull();
        Resolve(reread).MinEnforcement.Should().Be(MinEnforcement.Shall);
    }
}
