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
/// Reflight scoring through the pipeline — kanban/in-progress/reflight-groups.md
/// WI-6. One competitor can hold two live Entries for one task-round (the
/// reflight shape); the group can no longer key by competitor, and the
/// entitled/filler selection collapses the two to ONE task-round score (R1).
/// Mirrors ScoringServiceAnnulmentTests's synthetic-definition fixture style.
/// The task is deliberately un-normalised (raw score pass-through), so the
/// selection arithmetic is trivially readable.
/// </summary>
public class ReflightScoringTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = "raw", Kind = MeasuredKind.Number }];

    private static readonly ImmutableArray<ScoreTerm> ScoreTerms =
        [(ScoreTerm)new RateTerm { MetricRef = "raw", Rate = 1 }];

    private static TaskDefinition MakeTask() => new()
    {
        Code = "T",
        Name = "Test task",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Group = new GroupConstraint { MinPerGroup = 2 },
        Score = ScoreTerms,
    };

    private static ClassDefinition MakeClassDefinition(ReflightSelection entitled, ReflightSelection others)
    {
        var task = MakeTask();
        return new ClassDefinition
        {
            Name = "Synthetic",
            Version = "1.0",
            Reflight = new ReflightRule { EntitledScores = entitled, OthersScore = others },
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
    }

    /// <summary>Three competitors; one hand-built drawn task-round holding one group with all three.</summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors, GroupId Group) BuildCompetition(
        ClassDefinition definition)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Reflight Scoring Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15), "1.0.0", adoptedRules, Now));

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>();
        for (var i = 0; i < 3; i++)
        {
            var id = CompetitorId.New();
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
            competitors.Add(id);
        }

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = competitors.ToImmutable() };
        var taskRound = new TaskRound { Ordinal = 1, State = Competitions.TaskRoundState.Drawn, TaskRef = "T", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now));

        return (competition, competitors.ToImmutable(), group.Id);
    }

    /// <summary>Open a live entry for <paramref name="role"/> scoring <paramref name="rawScore"/> raw.</summary>
    private static Entry OpenEntry(Competition competition, GroupId group, CompetitorId competitor, ReflightRole role, decimal rawScore)
    {
        var opened = competition.OpenEntry(EntryId.New(), 0, 1, 1, group, competitor, role, Now).Value;
        var entry = Entry.Create(opened).Apply(new FlightOpened(1, Now));
        var captured = entry.CaptureMeasurement(1, "raw", MeasuredValue.Of(rawScore), Now, MetricDefs);
        captured.IsSuccess.Should().BeTrue();
        return entry.Apply(captured.Value);
    }

    private static Dictionary<EntryId, Entry> Entries(params Entry[] entries) =>
        entries.ToDictionary(e => e.Id);

    [Fact]
    public void An_entitled_reflight_in_the_same_group_replaces_the_original_without_throwing()
    {
        // Priority (c): the entitled competitor re-flies with their ORIGINAL
        // group — both entries in ONE group. This is the shape that used to
        // throw in ScoreCompetition's group loop (duplicate competitor keys);
        // it must now score, with the entitled entry replacing the original in
        // the competitor's own aggregate (decision 3).
        var definition = MakeClassDefinition(ReflightSelection.Replacement, ReflightSelection.BetterOf);
        var (competition, competitors, group) = BuildCompetition(definition);

        var original = OpenEntry(competition, group, competitors[0], ReflightRole.Original, 100m);
        var entitled = OpenEntry(competition, group, competitors[0], ReflightRole.Entitled, 400m);

        var result = ScoringService.ScoreCompetition(competition, Entries(original, entitled));

        // Replacement, un-normalised: the 400 is the official score, not the 100.
        result.IsSuccess.Should().BeTrue();
        result.Value.Scores[competitors[0].ToString()].Score.Should().Be(400m);
    }

    [Fact]
    public void A_filler_takes_the_better_of_its_two_scores()
    {
        var definition = MakeClassDefinition(ReflightSelection.Replacement, ReflightSelection.BetterOf);
        var (competition, competitors, group) = BuildCompetition(definition);

        var original = OpenEntry(competition, group, competitors[0], ReflightRole.Original, 100m);
        var filler = OpenEntry(competition, group, competitors[0], ReflightRole.Filler, 400m);
        var other = OpenEntry(competition, group, competitors[1], ReflightRole.Original, 100m);

        var result = ScoringService.ScoreCompetition(competition, Entries(original, filler, other));

        result.IsSuccess.Should().BeTrue();
        // BetterOf of 100 / 400 → 400.
        result.Value.Scores[competitors[0].ToString()].Score.Should().Be(400m);
        // The untouched competitor keeps their 100 (R3).
        result.Value.Scores[competitors[1].ToString()].Score.Should().Be(100m);
    }

    [Fact]
    public void An_annulled_original_leaves_the_lone_reflight_entry_scoring_ordinarily()
    {
        var definition = MakeClassDefinition(ReflightSelection.Replacement, ReflightSelection.BetterOf);
        var (competition, competitors, group) = BuildCompetition(definition);

        var original = OpenEntry(competition, group, competitors[0], ReflightRole.Original, 100m);
        original = original.Apply(original.AnnulEntry("withdrawn ruling", "jury", Now).Value);
        var entitled = OpenEntry(competition, group, competitors[0], ReflightRole.Entitled, 250m);

        var result = ScoringService.ScoreCompetition(competition, Entries(original, entitled));

        // A single live candidate, whatever its role, scores as itself.
        result.IsSuccess.Should().BeTrue();
        result.Value.Scores[competitors[0].ToString()].Score.Should().Be(250m);
    }

    [Fact]
    public void A_NotPermitted_class_fails_with_score_refLightNotPermitted()
    {
        var definition = MakeClassDefinition(ReflightSelection.NotPermitted, ReflightSelection.NotPermitted);
        var (competition, competitors, group) = BuildCompetition(definition);

        var original = OpenEntry(competition, group, competitors[0], ReflightRole.Original, 100m);
        var entitled = OpenEntry(competition, group, competitors[0], ReflightRole.Entitled, 100m);

        var result = ScoringService.ScoreCompetition(competition, Entries(original, entitled));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightNotPermitted");
    }

    [Fact]
    public void An_UndefinedRequiresRuling_class_fails_with_score_reflightRequiresRuling()
    {
        var definition = MakeClassDefinition(ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling);
        var (competition, competitors, group) = BuildCompetition(definition);

        var original = OpenEntry(competition, group, competitors[0], ReflightRole.Original, 100m);
        var entitled = OpenEntry(competition, group, competitors[0], ReflightRole.Entitled, 100m);

        var result = ScoringService.ScoreCompetition(competition, Entries(original, entitled));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightRequiresRuling");
    }

    [Fact]
    public void Two_same_role_live_entries_fail_with_score_reflightShapeUnsupported()
    {
        var definition = MakeClassDefinition(ReflightSelection.Replacement, ReflightSelection.BetterOf);
        var (competition, competitors, group) = BuildCompetition(definition);

        var first = OpenEntry(competition, group, competitors[0], ReflightRole.Original, 100m);
        var second = OpenEntry(competition, group, competitors[0], ReflightRole.Entitled, 100m);
        var third = OpenEntry(competition, group, competitors[0], ReflightRole.Entitled, 100m);

        var result = ScoringService.ScoreCompetition(competition, Entries(first, second, third));

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("score.reflightShapeUnsupported");
    }
}