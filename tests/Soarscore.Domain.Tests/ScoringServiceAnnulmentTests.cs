using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// The F3F.1.5 provisional re-flight shape, exercised through the scoring
/// pipeline — kanban/in-progress/annul-and-penalise-the-second-entry-thread.md.
/// One competitor has two Entries for the same task-round: an annulled first
/// attempt and a live replacement. The annulled Entry must contribute nothing
/// and must not collide with the replacement when group entries are keyed by
/// competitor. Pins the WI-10 fix (annulled entries excluded from group scoring).
/// </summary>
public class ScoringServiceAnnulmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] MetricNames = ["alpha", "bravo", "charlie", "delta"];

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [.. MetricNames.Select(n => new MetricDefinition { Name = n, Kind = MeasuredKind.Number })];

    private static readonly ImmutableArray<ScoreTerm> ScoreTerms =
        [.. MetricNames.Select(n => (ScoreTerm)new RateTerm { MetricRef = n, Rate = 1 })];

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = ScoreTerms,
    };

    private static ClassDefinition MakeClassDefinition(TaskDefinition task) => new()
    {
        Name = "Synthetic",
        Version = "1.0",
        Reflight = new ReflightRule
        {
            EntitledScores = ReflightSelection.Replacement,
            OthersScore = ReflightSelection.BetterOf,
        },
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

    private static (Competition Competition, List<CompetitorId> Competitors, GroupId Group) BuildCompetition()
    {
        var classDefinition = MakeClassDefinition(MakeTask());
        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Annul Replacement Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15), "1.0.0", adoptedRules, Now));

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 2; i++)
        {
            var id = CompetitorId.New();
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
            competitors.Add(id);
        }

        competition = competition.Apply(competition.DrawPhase(1, [], Now).Value);
        var group = competition.Phases[0].Rounds[0].TaskRounds[0].Groups[0];

        return (competition, competitors, group.Id);
    }

    private static Entry CaptureEntry(
        Competition competition, GroupId group, CompetitorId competitorRef, int multiplier)
    {
        var opened = competition.OpenEntry(EntryId.New(), 0, 1, 1, group, competitorRef, Now).Value;
        var entry = Entry.Create(opened).Apply(new FlightOpened(1, Now));

        foreach (var metric in MetricNames)
        {
            var value = (Array.IndexOf(MetricNames, metric) + 1) * 10m * multiplier;
            var captured = entry.CaptureMeasurement(1, metric, MeasuredValue.Of(value), Now, MetricDefs);
            captured.IsSuccess.Should().BeTrue();
            entry = entry.Apply(captured.Value);
        }

        return entry;
    }

    [Fact]
    public void An_annulled_entry_contributes_nothing_and_its_replacement_scores()
    {
        var (competition, competitors, group) = BuildCompetition();

        // The competitor's first attempt scores 100 raw (1x), then is annulled.
        var firstAttempt = CaptureEntry(competition, group, competitors[0], multiplier: 1);
        firstAttempt = firstAttempt.Apply(firstAttempt.AnnulEntry("re-flew under protest", "jury", Now).Value);

        // The replacement scores 400 raw (4x). The other competitor scores 100.
        var replacement = CaptureEntry(competition, group, competitors[0], multiplier: 4);
        var other = CaptureEntry(competition, group, competitors[1], multiplier: 1);

        var entries = new Dictionary<EntryId, Entry>
        {
            [firstAttempt.Id] = firstAttempt,
            [replacement.Id] = replacement,
            [other.Id] = other,
        };

        var result = ScoringService.ScoreCompetition(competition, entries);

        result.IsSuccess.Should().BeTrue();
        result.Value.Scores[competitors[0].ToString()].Score.Should().Be(400m);
        result.Value.Scores[competitors[1].ToString()].Score.Should().Be(100m);
    }
}
