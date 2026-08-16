// kanban/completed/class-definition-adoption-steel-thread-plan.md WI-1. A fixture
// just inside each ceiling passes; one just outside is rejected with a stable
// code. Corpus.All is also checked to sit inside every limit — the ceilings
// were set relative to those actuals in the first place.

using AwesomeAssertions;
using Soarscore.Application.Commands.CompetitionClasses;
using Soarscore.SeedData;
using Xunit;
using static Soarscore.Application.Tests.Commands.CompetitionClasses.ClassDefinitionIngestionFixtures;

namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

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

}
