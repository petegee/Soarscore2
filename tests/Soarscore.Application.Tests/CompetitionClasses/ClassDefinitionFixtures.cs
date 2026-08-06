// Shared minimal ClassDefinition baseline for validation tests, example-based
// (ClassDefinitionValidationTests.cs) and property-based
// (ClassDefinitionValidationPropertyTests.cs) alike — one construction, not
// duplicated per file.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Tests.CompetitionClasses;

internal static class ClassDefinitionFixtures
{
    public static ClassDefinition Minimal() => new()
    {
        Name = "Test Class",
        Version = "v1",
        Reflight = new ReflightRule { EntitledScores = ReflightSelection.BetterOf, OthersScore = ReflightSelection.BetterOf },
        Phases =
        [
            new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Tasks =
                [
                    new TaskDefinition
                    {
                        Code = "A",
                        Name = "Task A",
                        Metrics = [new MetricDefinition { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s" }],
                        Flights = new LastFlight(),
                        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
                        Score = [new RateTerm { MetricRef = "flightTime", Rate = 1 }],
                    },
                ],
            },
        ],
    };

    public static ClassDefinition WithSingleTask(ClassDefinition definition, TaskDefinition task) =>
        definition with { Phases = [definition.Phases[0] with { Tasks = [task] }] };

    /// <summary>N copies of the baseline's single phase, ordinals 1..count — generalises the old fixed TwoPhases() helper.</summary>
    public static ClassDefinition NPhases(int count)
    {
        var definition = Minimal();
        var basePhase = definition.Phases[0];
        var phases = Enumerable.Range(1, count).Select(i => basePhase with { Ordinal = i }).ToImmutableArray();
        return definition with { Phases = phases };
    }
}
