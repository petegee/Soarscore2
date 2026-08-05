using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Soarscore.Application;
using Soarscore.Domain.People;
using Soarscore.Infrastructure.CompetitionClasses;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

// Not "Soarscore.Architecture.Tests": a namespace segment literally named
// "Architecture" shadows ArchUnitNET.Domain.Architecture for every unqualified
// reference in this file, even behind a using-alias. The project/folder name
// stays as WI-2 specifies; only the C# namespace differs.
namespace Soarscore.ArchitectureTests;

// WI-2 (docs/plans/command-side-steel-thread-plan.md): guards the hexagonal
// layering CLAUDE.md and LADR-0001 §4.2 state in prose. Soarscore.Api (WI-8) is
// not loaded into the Architecture below — the rules here only constrain
// Domain/Application/Infrastructure's outbound dependencies, and the
// Infrastructure-does-not-depend-on-Api rule matches on an assembly-name
// pattern rather than a project reference, so it holds without loading Api.
// RouteShapeTests.cs is the rule that exercises Soarscore.Api itself.
public sealed class LayerRuleTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(Person).Assembly, typeof(SoarscoreEventJson).Assembly, typeof(ClassDefinitionStreamId).Assembly)
        .Build();

    [Fact]
    public void Domain_depends_on_nothing_outside_the_BCL()
    {
        IArchRule rule = Types().That().ResideInAssembly(typeof(Person).Assembly)
            .Should().NotDependOnAny(Types(includeReferenced: true).That()
                .ResideInAssemblyMatching(@"^(Soarscore\.Application|Soarscore\.Infrastructure|Soarscore\.Api|Marten|Npgsql)(\.|,)"))
            .Because("CLAUDE.md's core architectural law and LADR-0001 §4 require the Domain layer to have zero dependencies outside the BCL.");

        rule.Check(Architecture);
    }

    [Fact]
    public void Application_depends_on_Domain_only()
    {
        IArchRule rule = Types().That().ResideInAssembly(typeof(SoarscoreEventJson).Assembly)
            .Should().NotDependOnAny(Types(includeReferenced: true).That()
                .ResideInAssemblyMatching(@"^(Soarscore\.Infrastructure|Soarscore\.Api|Marten|Npgsql)(\.|,)"))
            .Because("LADR-0001 §4.2: hexagonal dependencies point inward — Application defines ports, Infrastructure implements them, and IDocumentSession must never appear in Application.");

        rule.Check(Architecture);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly(typeof(ClassDefinitionStreamId).Assembly)
            .Should().NotDependOnAny(Types(includeReferenced: true).That()
                .ResideInAssemblyMatching(@"^Soarscore\.Api(\.|,)"))
            .Because("Api is the outermost adapter (composition root); nothing beneath it may depend back on it.");

        rule.Check(Architecture);
    }
}
