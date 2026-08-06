// docs/plans/class-definition-adoption-steel-thread-plan.md WI-1. A fixture
// just inside each ceiling passes; one just outside is rejected with a stable
// code. Corpus.All is also checked to sit inside every limit — the ceilings
// were set relative to those actuals in the first place.

using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Application.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Application.Tests.CompetitionClasses;

public class ClassDefinitionIngestionTests
{
    [Fact]
    public void Tasks_just_inside_the_per_phase_limit_pass()
    {
        var definition = DefinitionWithTasks(ClassDefinitionIngestion.MaxTasksPerPhase);
        ClassDefinitionIngestion.CheckLimits(definition).Should().BeEmpty();
    }

    [Fact]
    public void Tasks_just_outside_the_per_phase_limit_are_rejected()
    {
        var definition = DefinitionWithTasks(ClassDefinitionIngestion.MaxTasksPerPhase + 1);

        var defects = ClassDefinitionIngestion.CheckLimits(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.ingestion.too-many-tasks");
    }

    [Fact]
    public void Parameters_just_inside_the_limit_pass()
    {
        var definition = DefinitionWithParameters(ClassDefinitionIngestion.MaxParametersPerDefinition);
        ClassDefinitionIngestion.CheckLimits(definition).Should().BeEmpty();
    }

    [Fact]
    public void Parameters_just_outside_the_limit_are_rejected()
    {
        var definition = DefinitionWithParameters(ClassDefinitionIngestion.MaxParametersPerDefinition + 1);

        var defects = ClassDefinitionIngestion.CheckLimits(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.ingestion.too-many-parameters");
    }

    [Fact]
    public void Score_terms_just_inside_the_per_task_limit_pass()
    {
        var definition = DefinitionWithScoreTerms(ClassDefinitionIngestion.MaxScoreTermsPerTask);
        ClassDefinitionIngestion.CheckLimits(definition).Should().BeEmpty();
    }

    [Fact]
    public void Score_terms_just_outside_the_per_task_limit_are_rejected()
    {
        var definition = DefinitionWithScoreTerms(ClassDefinitionIngestion.MaxScoreTermsPerTask + 1);

        var defects = ClassDefinitionIngestion.CheckLimits(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.ingestion.too-many-score-terms");
    }

    [Fact]
    public void Bands_just_inside_the_per_term_limit_pass()
    {
        var definition = DefinitionWithBands(ClassDefinitionIngestion.MaxBandsPerTerm);
        ClassDefinitionIngestion.CheckLimits(definition).Should().BeEmpty();
    }

    [Fact]
    public void Bands_just_outside_the_per_term_limit_are_rejected()
    {
        var definition = DefinitionWithBands(ClassDefinitionIngestion.MaxBandsPerTerm + 1);

        var defects = ClassDefinitionIngestion.CheckLimits(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.ingestion.too-many-bands");
    }

    [Fact]
    public void Rows_just_inside_the_per_term_limit_pass()
    {
        var definition = DefinitionWithRows(ClassDefinitionIngestion.MaxRowsPerTerm);
        ClassDefinitionIngestion.CheckLimits(definition).Should().BeEmpty();
    }

    [Fact]
    public void Rows_just_outside_the_per_term_limit_are_rejected()
    {
        var definition = DefinitionWithRows(ClassDefinitionIngestion.MaxRowsPerTerm + 1);

        var defects = ClassDefinitionIngestion.CheckLimits(definition);

        defects.Should().ContainSingle(d => d.Code == "class-definition.ingestion.too-many-rows");
    }

    [Fact]
    public void Corpus_definitions_are_all_within_every_ingestion_limit()
    {
        foreach (var (fileName, definition) in Corpus.All)
        {
            ClassDefinitionIngestion.CheckLimits(definition).Should()
                .BeEmpty($"{fileName} is the baseline every ingestion ceiling was set generously above");
        }
    }

    // ---------------------------------------------------------------- fixtures

    private static TaskDefinition MinimalTask(string code) => new()
    {
        Code = code,
        Name = code,
        Metrics = [new MetricDefinition { Name = "m", Kind = MeasuredKind.Number }],
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [new RateTerm { MetricRef = "m", Rate = 1 }],
    };

    private static ClassDefinition BaseDefinition(TaskDefinition task) => new()
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

    private static ClassDefinition DefinitionWithTasks(int count)
    {
        var tasks = Enumerable.Range(0, count).Select(i => MinimalTask($"T{i}")).ToImmutableArray();
        var definition = BaseDefinition(MinimalTask("seed"));
        return definition with { Phases = [definition.Phases[0] with { Tasks = tasks }] };
    }

    private static ClassDefinition DefinitionWithParameters(int count)
    {
        var parameters = Enumerable.Range(0, count).Select(i => new Parameter { Name = $"p{i}" }).ToImmutableArray();
        return BaseDefinition(MinimalTask("A")) with { Parameters = parameters };
    }

    private static ClassDefinition DefinitionWithScoreTerms(int count)
    {
        var terms = Enumerable.Range(0, count).Select(_ => (ScoreTerm)new ConstantTerm { Value = 1 }).ToImmutableArray();
        var task = MinimalTask("A") with { Score = terms };
        return BaseDefinition(task);
    }

    private static ClassDefinition DefinitionWithBands(int count)
    {
        var bands = Enumerable.Range(0, count).Select(i => new Band(i, i + 1, 1)).ToImmutableArray();
        var task = MinimalTask("A") with { Score = [new PiecewiseTerm { MetricRef = "m", Bands = bands }] };
        return BaseDefinition(task);
    }

    private static ClassDefinition DefinitionWithRows(int count)
    {
        var rows = Enumerable.Range(0, count).Select(i => new LookupRow(i, i)).ToImmutableArray();
        var task = MinimalTask("A") with { Score = [new LookupTerm { MetricRef = "m", Rows = rows }] };
        return BaseDefinition(task);
    }
}
