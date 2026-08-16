// Guards CLAUDE.md's core architectural law — "the core system must not know
// about any specific competition class", stated there as "not a style
// preference". Before this test the law was held by convention only, with
// nothing to catch a regression.
//
// This test is a source scan, not an ArchUnitNET rule or a reflection test,
// because neither of those can see what actually leaks: a string literal or a
// switch arm naming a class. ArchUnitNET reasons over types and dependencies;
// reflection over a built assembly has already discarded the distinction
// between "F3K" the comment and "F3K" the branch.
//
// Comments are stripped before matching, and that is what makes the test
// viable rather than noisy. A comment citing `F3K.7` next to class-agnostic
// code is documentation of *why* the generic code is shaped as it is — it is
// the good case, and there are twenty of them in src/ today. What the law
// forbids is the core reading a class name at runtime.
//
// There is deliberately no allowlist. A legitimate hit outside a comment would
// be a design conversation, not a suppression.

using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace Soarscore.ArchitectureTests;

public sealed partial class ClassAgnosticismTests
{
    [Fact]
    public void No_production_code_names_a_specific_competition_class()
    {
        var offenders = new List<string>();

        foreach (var file in ProductionSourceFiles())
        {
            var code = StripComments(File.ReadAllText(file));

            foreach (Match match in ClassNames().Matches(code))
            {
                var line = code.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetRelativePath(SourceRoot(), file)}:{line} — '{match.Value}'");
            }
        }

        offenders.Should().BeEmpty(
            "the core system must not know about any specific competition class (CLAUDE.md). "
            + "All variance between classes belongs in the data-driven Competition Class "
            + "definition, which the core reads generically. A rule reference in a *comment* "
            + "is fine and is stripped before this check; a class name in code is not.");
    }

    // Without this, the test above would pass just as happily against zero
    // files — a broken source-root walk, a moved directory, a change to the
    // build layout. Asserting that the unstripped corpus *does* contain class
    // names is what proves the scanner is reading the real tree.
    [Fact]
    public void The_scan_reaches_real_source_files()
    {
        var files = ProductionSourceFiles().ToList();

        files.Should().HaveCountGreaterThan(20, "src/ holds four projects' worth of C#");

        files.Select(File.ReadAllText)
            .Any(text => ClassNames().IsMatch(text))
            .Should()
            .BeTrue("class names appear in rule-reference comments throughout src/; "
                    + "if none is found, the scan is not reading what it thinks it is");
    }

    private static IEnumerable<string> ProductionSourceFiles() =>
        Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// src/ only. tools/Soarscore.SeedData is where class names belong — it is
    /// the corpus of class definitions, the very data the core must stay
    /// ignorant of — and tests/ names classes to build fixtures.
    /// </summary>
    private static string SourceRoot() => Path.Combine(RepositoryRoot(), "src");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Soarscore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Walked up from '{AppContext.BaseDirectory}' without finding Soarscore.sln.");
    }

    /// <summary>
    /// Replaces comment bodies with equivalent whitespace rather than deleting
    /// them, so reported line numbers still match the file on disk. Newlines
    /// inside block comments are preserved for the same reason.
    /// </summary>
    private static string StripComments(string source) =>
        CommentSyntax().Replace(source, match =>
            string.Concat(match.Value.Select(c => c == '\n' ? '\n' : ' ')));

    [GeneratedRegex(@"\bF3B\b|\bF3F\b|\bF3J\b|\bF3K\b|\bF5J\b|\bF5K\b|\bF5L\b|\bNZMAA\b|\bALES\b|\bRadian\b")]
    private static partial Regex ClassNames();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex CommentSyntax();
}
