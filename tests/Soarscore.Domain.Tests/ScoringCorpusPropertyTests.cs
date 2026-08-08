using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// WI-5 invariant 7 (docs/plans/scoring-steel-thread-plan.md): every DRAWABLE
/// seed class scores end to end, without throwing, and every competitor with
/// at least one Entry receives a placing. This is the test that would catch a
/// class-specific assumption leaking into the pipeline (CLAUDE.md's core
/// architectural law).
///
/// "Drawable" excludes F3K, F5K and F3B: Competition.DrawPhase rejects their
/// round composition today (drawPhase.unsupportedRoundComposition — the check
/// on phaseDefinition.Tasks.Length/Rounds.Kind/Rounds.TasksPerRound in
/// Competition.cs's DrawPhase), so those three cannot reach ScoreCompetition
/// regardless of what this thread does — see the plan's Out of scope. That
/// leaves 8 of the 11 corpus classes.
///
/// A fixed enumeration, not a CsCheck generator over the class list — the
/// "property" here is universality over the corpus, which enumerating every
/// drawable member already demonstrates (mirrors EntryCapturePropertyTests'
/// corpus-generic invariants 3/4, whose own doc comment makes the same call).
///
/// Drives the Domain decide functions directly — Soarscore.Domain.Tests
/// cannot reference Soarscore.Application, so there is no handler/dispatcher
/// to call — mirroring OpenEntryDecideTests's and BindParameterPropertyTests's
/// style in this project, and DrawPhasePropertyTests's in
/// Soarscore.Application.Tests: Competition.Create -> RegisterCompetitor
/// (repeated) -> [BindParameter where a class needs it] -> DrawPhase ->
/// OpenEntry per drawn competitor -> Entry.Create/OpenFlight/CaptureMeasurement
/// per flight, folding each aggregate by hand. No event store, no handlers.
/// </summary>
public class ScoringCorpusPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    // Uniform across all eight classes: at or above every literal MinPerGroup
    // in the set (F3F's 10 is the highest), and within every MaxRounds the
    // set declares (NZ-N-ALES123 / NZ-P-Radian's 3 is the lowest) at Rounds=2.
    private const int FieldSize = 10;
    private const int Rounds = 2;

    private static readonly ImmutableHashSet<string> DrawableFileNames = ImmutableHashSet.Create(
        "30-f5j", "50-f3j", "60-f5l", "70-f3f",
        "80-nz-m-ales200", "81-nz-m-ndc", "83-nz-n-ales123", "85-nz-p-radian");

    /// <summary>
    /// FileName -> the parameter bindings that class's first phase/task needs
    /// resolved before it can be drawn/opened. Only F5L and NZ-M-ALES200
    /// parameterise Group.MinPerGroup (BindParameterPropertyTests' property 5
    /// discovers the same two, plus F5K, which is excluded here as
    /// undrawable); only NZ-N-ALES123 and NZ-P-Radian parameterise a Fixed
    /// task's WorkingTime with no declared default
    /// (OpenEntryDecideTests.OpenEntry_against_an_unbound_undefaulted_parameterised_WorkingTime_fails_with_a_stable_code
    /// is the same NZ-N shape). Every other class in the set resolves its
    /// phase-0 task from literals alone.
    /// </summary>
    private static readonly ImmutableDictionary<string, ImmutableArray<(string Name, MeasuredValue Value)>> RequiredBindings =
        new Dictionary<string, ImmutableArray<(string, MeasuredValue)>>
        {
            ["60-f5l"] = [("groupSize", MeasuredValue.Of((decimal)FieldSize))],
            ["80-nz-m-ales200"] = [("groupSize", MeasuredValue.Of((decimal)FieldSize))],
            ["83-nz-n-ales123"] = [("roundDuration", MeasuredValue.Of(360m))],
            ["85-nz-p-radian"] = [("roundDuration", MeasuredValue.Of(420m))],
        }.ToImmutableDictionary();

    [Fact]
    public void Every_drawable_corpus_class_scores_without_throwing_and_every_flown_competitor_is_placed()
    {
        var drawable = Corpus.All.Where(c => DrawableFileNames.Contains(c.FileName)).ToImmutableArray();

        // Guards the premise: exactly the plan's stated 8 (11 corpus classes
        // minus F3K/F5K/F3B). A corpus change that alters this set should
        // fail here loudly, rather than silently under- or over-testing.
        drawable.Length.Should().Be(8);
        drawable.Select(c => c.FileName).Should().BeEquivalentTo(DrawableFileNames);

        foreach (var seedClass in drawable)
        {
            ScoreEndToEnd(seedClass);
        }
    }

    private static void ScoreEndToEnd(SeedClass seedClass)
    {
        var definition = seedClass.Definition;

        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), $"Corpus Scoring Test — {seedClass.FileName}", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, Now);

        var competition = Competition.Create(created);

        for (var i = 0; i < FieldSize; i++)
        {
            var registered = competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now);
            registered.IsSuccess.Should().BeTrue(seedClass.FileName);
            competition = competition.Apply(registered.Value);
        }

        if (RequiredBindings.TryGetValue(seedClass.FileName, out var bindings))
        {
            foreach (var (name, value) in bindings)
            {
                var bound = competition.BindParameter(name, value, "cd", Now);
                bound.IsSuccess.Should().BeTrue($"{seedClass.FileName}/{name}: {bound.Code}");
                competition = competition.Apply(bound.Value);
            }
        }

        var drawn = competition.DrawPhase(Rounds, Now);
        drawn.IsSuccess.Should().BeTrue($"{seedClass.FileName}: {drawn.Code}");
        competition = competition.Apply(drawn.Value);

        var task = definition.Phases[0].Tasks[0];
        var entries = new Dictionary<EntryId, Entry>();
        var flownCompetitors = new HashSet<CompetitorId>();

        foreach (var round in competition.Phases[0].Rounds)
        {
            var taskRound = round.TaskRounds[0];
            foreach (var group in taskRound.Groups)
            {
                foreach (var competitorRef in group.CompetitorRefs)
                {
                    var opened = competition.OpenEntry(
                        EntryId.New(), 0, round.Ordinal, taskRound.Ordinal, group.Id, competitorRef, Now);
                    opened.IsSuccess.Should().BeTrue($"{seedClass.FileName}: {opened.Code}");

                    var entry = Entry.Create(opened.Value);

                    var flightOpened = entry.OpenFlight(1, Now, maxLaunches: null, at: Now);
                    flightOpened.IsSuccess.Should().BeTrue($"{seedClass.FileName}: {flightOpened.Code}");
                    entry = entry.Apply(flightOpened.Value);

                    // Capture SOMETHING for every declared metric — not
                    // realistic flight physics, just presence with the right
                    // Kind, which is what keeps FlightInterpreter /
                    // PredicateEvaluator from throwing on a missing key. See
                    // TaskDefinition.Metrics and this class's Score /
                    // ValidWhen for what a real capture would need to name.
                    foreach (var metric in task.Metrics)
                    {
                        var value = metric.Kind == MeasuredKind.Number
                            ? MeasuredValue.Of(100m)
                            : MeasuredValue.Of(true);

                        var captured = entry.CaptureMeasurement(1, metric.Name, value, Now, task.Metrics);
                        captured.IsSuccess.Should().BeTrue($"{seedClass.FileName}/{metric.Name}: {captured.Code}");
                        entry = entry.Apply(captured.Value);
                    }

                    entries[entry.Id] = entry;
                    flownCompetitors.Add(competitorRef);
                }
            }
        }

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsSuccess.Should().BeTrue($"{seedClass.FileName}: {result.Code}");

        foreach (var competitorRef in flownCompetitors)
        {
            result.Value.Placings.Should().ContainKey(
                competitorRef.ToString(),
                $"{seedClass.FileName}: competitor {competitorRef} flew but received no placing");
        }
    }
}
