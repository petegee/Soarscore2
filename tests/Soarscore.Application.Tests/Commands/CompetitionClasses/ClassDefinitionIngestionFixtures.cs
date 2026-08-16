// Shared fixture builders for ingestion-limit tests, example-based
// (ClassDefinitionIngestionTests.cs) and property-based
// (ClassDefinitionIngestionPropertyTests.cs) alike — one construction, not
// duplicated per file.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

internal static class ClassDefinitionIngestionFixtures
{
    public static TaskDefinition MinimalTask(string code) => new()
    {
        Code = code,
        Name = code,
        Metrics = [new MetricDefinition { Name = "m", Kind = MeasuredKind.Number }],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [new RateTerm { MetricRef = "m", Rate = 1 }],
    };

    public static ClassDefinition BaseDefinition(TaskDefinition task) => new()
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
                Tasks = [task],
            },
        ],
    };

    public static ClassDefinition DefinitionWithTasks(int count)
    {
        var tasks = Enumerable.Range(0, count).Select(i => MinimalTask($"T{i}")).ToImmutableArray();
        var definition = BaseDefinition(MinimalTask("seed"));
        return definition with { Phases = [definition.Phases[0] with { Tasks = tasks }] };
    }

    public static ClassDefinition DefinitionWithParameters(int count)
    {
        var parameters = Enumerable.Range(0, count).Select(i => new Parameter { Name = $"p{i}" }).ToImmutableArray();
        return BaseDefinition(MinimalTask("A")) with { Parameters = parameters };
    }

    public static ClassDefinition DefinitionWithScoreTerms(int count)
    {
        var terms = Enumerable.Range(0, count).Select(_ => (ScoreTerm)new ConstantTerm { Value = 1 }).ToImmutableArray();
        var task = MinimalTask("A") with { Score = terms };
        return BaseDefinition(task);
    }

    public static ClassDefinition DefinitionWithBands(int count)
    {
        var bands = Enumerable.Range(0, count).Select(i => new Band(i, i + 1, 1)).ToImmutableArray();
        var task = MinimalTask("A") with { Score = [new PiecewiseTerm { MetricRef = "m", Bands = bands }] };
        return BaseDefinition(task);
    }

    public static ClassDefinition DefinitionWithRows(int count)
    {
        var rows = Enumerable.Range(0, count).Select(i => new LookupRow(i, i)).ToImmutableArray();
        var task = MinimalTask("A") with { Score = [new LookupTerm { MetricRef = "m", Rows = rows }] };
        return BaseDefinition(task);
    }
}
