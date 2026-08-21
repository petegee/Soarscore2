using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Re-expresses, at the scoring seam, the two parameterised-working-time
/// resolution proofs that moved out of OpenEntryDecideTests when the stored
/// TimeWindow was removed (kanban/in-progress/remove-stored-working-time.md
/// WI-4). Today scoring resolves the declared working time through the same
/// ParameterResolver OpenEntry once used, surfacing it as
/// ResolvedTiming.WorkingTime on the ResolvedTask that
/// ScoringService.ScoreGroup obtains via the public ParameterResolver.ResolveTask
/// (the private ResolveTiming is not directly testable). These guard that a
/// parameterised working time still resolves from a binding (420 s) and from its
/// declared default (600 s) — resolution stays load-bearing even though nothing
/// stores a window any more.
/// </summary>
public class ResolvedWorkingTimeTests
{
    private const string ParameterName = "workingTime.A";

    private static readonly ImmutableArray<Parameter> DeclaredParameters =
        [new Parameter { Name = ParameterName, Kind = MeasuredKind.Number }];

    /// <summary>The same parameter with a declared default, mirroring the two moved tests' 600 s premise.</summary>
    private static readonly ImmutableArray<Parameter> DeclaredParametersWithDefault =
        [DeclaredParameters[0] with { DefaultValue = MeasuredValue.Of(600m) }];

    /// <summary>A Fixed-working-time task whose WorkingTime is a ParameterRef, not a literal.</summary>
    private static TaskDefinition TaskWithParameterisedWorkingTime() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = [],
        Flights = new LastFlight(),
        Timing = new TaskTiming
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = NumberOrParam.Param(ParameterName),
        },
        Score = [],
    };

    [Fact]
    public void A_parameterised_working_time_resolves_from_a_binding()
    {
        var bindings = new Dictionary<string, MeasuredValue> { [ParameterName] = MeasuredValue.Of(420m) };

        var resolved = ParameterResolver.ResolveTask(TaskWithParameterisedWorkingTime(), bindings, DeclaredParameters);

        resolved.Timing.WorkingTime.Should().Be(420m);
    }

    [Fact]
    public void A_parameterised_working_time_resolves_from_its_declared_default()
    {
        var bindings = new Dictionary<string, MeasuredValue>();

        var resolved = ParameterResolver.ResolveTask(TaskWithParameterisedWorkingTime(), bindings, DeclaredParametersWithDefault);

        resolved.Timing.WorkingTime.Should().Be(600m);
    }
}
