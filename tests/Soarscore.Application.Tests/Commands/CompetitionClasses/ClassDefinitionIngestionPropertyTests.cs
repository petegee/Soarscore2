// docs/plans/class-definition-adoption-steel-thread-plan.md WI-1's "a
// definition just inside each limit passes; just outside is rejected" checked
// as a property across random counts either side of each of CheckLimits' five
// ceilings, rather than the one hand-picked pair per ceiling
// ClassDefinitionIngestionTests.cs already covers.

using CsCheck;
using Soarscore.Application.Commands.CompetitionClasses;
using Xunit;
using static Soarscore.Application.Tests.Commands.CompetitionClasses.ClassDefinitionIngestionFixtures;

namespace Soarscore.Application.Tests.Commands.CompetitionClasses;

public class ClassDefinitionIngestionPropertyTests
{
    [Fact]
    public void Task_count_within_the_per_phase_limit_always_passes_and_beyond_it_is_always_flagged()
    {
        (from within in Gen.Int[1, ClassDefinitionIngestion.MaxTasksPerPhase]
         from over in Gen.Int[ClassDefinitionIngestion.MaxTasksPerPhase + 1, ClassDefinitionIngestion.MaxTasksPerPhase + 100]
         select (within, over))
        .Sample(t =>
            ClassDefinitionIngestion.CheckLimits(DefinitionWithTasks(t.within)).Count == 0
            && ClassDefinitionIngestion.CheckLimits(DefinitionWithTasks(t.over)) is [{ Code: "class-definition.ingestion.too-many-tasks" }]);
    }

    [Fact]
    public void Parameter_count_within_the_limit_always_passes_and_beyond_it_is_always_flagged()
    {
        (from within in Gen.Int[1, ClassDefinitionIngestion.MaxParametersPerDefinition]
         from over in Gen.Int[ClassDefinitionIngestion.MaxParametersPerDefinition + 1, ClassDefinitionIngestion.MaxParametersPerDefinition + 100]
         select (within, over))
        .Sample(t =>
            ClassDefinitionIngestion.CheckLimits(DefinitionWithParameters(t.within)).Count == 0
            && ClassDefinitionIngestion.CheckLimits(DefinitionWithParameters(t.over)) is [{ Code: "class-definition.ingestion.too-many-parameters" }]);
    }

    [Fact]
    public void Score_term_count_within_the_per_task_limit_always_passes_and_beyond_it_is_always_flagged()
    {
        (from within in Gen.Int[1, ClassDefinitionIngestion.MaxScoreTermsPerTask]
         from over in Gen.Int[ClassDefinitionIngestion.MaxScoreTermsPerTask + 1, ClassDefinitionIngestion.MaxScoreTermsPerTask + 100]
         select (within, over))
        .Sample(t =>
            ClassDefinitionIngestion.CheckLimits(DefinitionWithScoreTerms(t.within)).Count == 0
            && ClassDefinitionIngestion.CheckLimits(DefinitionWithScoreTerms(t.over)) is [{ Code: "class-definition.ingestion.too-many-score-terms" }]);
    }

    [Fact]
    public void Band_count_within_the_per_term_limit_always_passes_and_beyond_it_is_always_flagged()
    {
        (from within in Gen.Int[1, ClassDefinitionIngestion.MaxBandsPerTerm]
         from over in Gen.Int[ClassDefinitionIngestion.MaxBandsPerTerm + 1, ClassDefinitionIngestion.MaxBandsPerTerm + 100]
         select (within, over))
        .Sample(t =>
            ClassDefinitionIngestion.CheckLimits(DefinitionWithBands(t.within)).Count == 0
            && ClassDefinitionIngestion.CheckLimits(DefinitionWithBands(t.over)) is [{ Code: "class-definition.ingestion.too-many-bands" }]);
    }

    [Fact]
    public void Row_count_within_the_per_term_limit_always_passes_and_beyond_it_is_always_flagged()
    {
        (from within in Gen.Int[1, ClassDefinitionIngestion.MaxRowsPerTerm]
         from over in Gen.Int[ClassDefinitionIngestion.MaxRowsPerTerm + 1, ClassDefinitionIngestion.MaxRowsPerTerm + 100]
         select (within, over))
        .Sample(t =>
            ClassDefinitionIngestion.CheckLimits(DefinitionWithRows(t.within)).Count == 0
            && ClassDefinitionIngestion.CheckLimits(DefinitionWithRows(t.over)) is [{ Code: "class-definition.ingestion.too-many-rows" }]);
    }
}
