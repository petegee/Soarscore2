// Property tests — kanban/in-progress/reflight-groups.md WI-8, named
// invariants R1..R3 (CLAUDE.md: "a named invariant is what makes the property
// test meaningful"). CsCheck, in ScoringServicePropertyTests's style: drives
// ScoringService.ScoreCompetition / ReflightSelector directly with a small
// synthetic class, so a reflight-role competitor's two live Entries in one
// task-round cannot corrupt the aggregate.
//
// The task is un-normalised (raw pass-through), so the aggregate score IS the
// selected score — making the selection law (R2) directly comparable.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

public class ReflightSelectionPropertyTests
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

    private static ClassDefinition MakeClassDefinition(ReflightSelection entitled, ReflightSelection others, decimal minNewGroupSize)
    {
        var task = MakeTask();
        return new ClassDefinition
        {
            Name = "Synthetic",
            Version = "1.0",
            Reflight = new ReflightRule
            {
                EntitledScores = entitled,
                OthersScore = others,
                MinNewGroupSize = minNewGroupSize,
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
    }

    private static Entry OpenAndCapture(Competition competition, GroupId group, CompetitorId competitor, ReflightRole role, decimal rawScore)
    {
        var opened = competition.OpenEntry(EntryId.New(), 0, 1, 1, group, competitor, role, Now).Value;
        var entry = Entry.Create(opened).Apply(new FlightOpened(1, Now));
        var captured = entry.CaptureMeasurement(1, "raw", MeasuredValue.Of(rawScore), Now, MetricDefs);
        captured.IsSuccess.Should().BeTrue();
        return entry.Apply(captured.Value);
    }

    /// <summary>
    /// <paramref name="competitorCount"/> competitors in one Drawn group, each
    /// with a single live Original at <paramref name="baseRaw"/>; the shop at
    /// index 0 is reserved for the reflight-role entries passed in
    /// <paramref name="reflightEntries"/>. Returns the competition, the
    /// competitors, and the entries dict.
    /// </summary>
    private static (Competition Competition, ImmutableArray<CompetitorId> Competitors, Dictionary<EntryId, Entry> Entries) BuildCompetition(
        ReflightSelection entitled,
        ReflightSelection others,
        IReadOnlyList<(ReflightRole Role, decimal Raw)> reflightEntries,
        int competitorCount,
        decimal baseRaw = 100m)
    {
        var classDefinition = MakeClassDefinition(entitled, others, minNewGroupSize: 2);
        var adoptedRules = new AdoptedRules
        {
            Definition = classDefinition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = classDefinition.Version,
            AdoptedAt = Now,
        };
        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Reflight Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15), "1.0.0", adoptedRules, Now));

        var competitors = ImmutableArray.CreateBuilder<CompetitorId>();
        for (var i = 0; i < competitorCount; i++)
        {
            var id = CompetitorId.New();
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
            competitors.Add(id);
        }

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = competitors.ToImmutable() };
        var taskRound = new TaskRound { Ordinal = 1, State = Soarscore.Domain.Competitions.TaskRoundState.Drawn, TaskRef = "T", Groups = [group] };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };
        competition = competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now));

        var entries = new Dictionary<EntryId, Entry>();
        foreach (var competitorRef in competitors)
        {
            var original = OpenAndCapture(competition, group.Id, competitorRef, ReflightRole.Original, baseRaw);
            entries[original.Id] = original;
        }

        // The reflight-role entries, all against the same (original) group —
        // the priority-(c) shape, both entries in one group.
        foreach (var (role, raw) in reflightEntries)
        {
            var entry = OpenAndCapture(competition, group.Id, competitors[0], role, raw);
            entries[entry.Id] = entry;
        }

        return (competition, competitors.ToImmutable(), entries);
    }

    // ========================================================== R1 — one score per task-round

    /// <summary>
    /// R1: however many live Entries a competitor holds in one task-round (the
    /// legal reflight shape: Original + exactly one reflight-role entry), ScoreComposition
    /// produces exactly ONE aggregate score for that competitor — the selection
    /// collapsed the candidates (finding 9; the aggregate keying at the phase
    /// close stays safe only if a duplicate never reaches it).
    /// </summary>
    [Fact]
    public void A_competitor_with_two_live_entries_gets_exactly_one_score()
    {
        (from reflightRole in Gen.OneOfConst(ReflightRole.Entitled, ReflightRole.Filler)
         from reflightRaw in Gen.Decimal[1, 1000]
         select (reflightRole, reflightRaw))
        .Sample(t =>
        {
            var (competition, competitors, entries) =
                BuildCompetition(ReflightSelection.Replacement, ReflightSelection.BetterOf,
                    [(t.reflightRole, t.reflightRaw)], competitorCount: 4);

            var result = ScoringService.ScoreCompetition(competition, entries);

            result.IsSuccess.Should().BeTrue();
            // R1: exactly one score for the two-entry competitor (and one each
            // for the other three).
            result.Value.Scores.Should().HaveCount(4);
            result.Value.Scores.Values.Count(s => s.CompetitorRef == competitors[0].ToString()).Should().Be(1);
        });
    }

    // ========================================================== R2 — the selection law

    /// <summary>
    /// R2: under BetterOf the selected score is exactly the max of the
    /// competitor's candidate normalised scores; under Replacement it is
    /// exactly the reflight-role candidate's. The selector output is checked
    /// against the definition directly, candidate pair by candidate pair.
    /// </summary>
    [Fact]
    public void The_selection_law_holds_for_generated_score_pairs()
    {
        (from original in Gen.Decimal[1, 1000]
         from reflight in Gen.Decimal[1, 1000]
         from selection in Gen.OneOfConst(ReflightSelection.Replacement, ReflightSelection.BetterOf)
         select (original, reflight, selection))
        .Sample(t =>
        {
            var rule = new ReflightRule { EntitledScores = t.selection, OthersScore = ReflightSelection.BetterOf };
            var candidates = new List<(ReflightRole, decimal)>
            {
                (ReflightRole.Original, t.original),
                (ReflightRole.Entitled, t.reflight),
            };

            var result = ReflightSelector.Select(candidates, rule);

            result.IsSuccess.Should().BeTrue();
            var expected = t.selection == ReflightSelection.BetterOf
                ? Math.Max(t.original, t.reflight)
                : t.reflight;
            result.Value.Should().Be(expected);
        });
    }

    // ========================================================== R3 — reflight groups are additive

    /// <summary>
    /// R3: appending a reflight group — folding AppendReflightGroup and opening
    /// the reflow entries into the new group — changes no task-round score of
    /// any competitor who holds no reflight-role entry in that task-round.
    /// Score, append, fold, open, score; the untouched competitors' scores are
    /// identical (the regression guard for WI-6's group-loop restructure).
    /// </summary>
    [Fact]
    public void Appending_a_reflight_group_leaves_untouched_competitors_scoring_the_same()
    {
        (from reflightRaw in Gen.Decimal[1, 1000]
         from fillerRaw in Gen.Decimal[1, 1000]
         select (reflightRaw, fillerRaw))
        .Sample(t =>
        {
            // 6 competitors in one group; [0] and [1] get openings into an
            // APPENDED reflight group, while [2..5] hold only their Original
            // and must score identically after the append.
            var (competition, competitors, beforeEntries) =
                BuildCompetition(ReflightSelection.Replacement, ReflightSelection.BetterOf,
                    [], competitorCount: 6);

            var before = ScoringService.ScoreCompetition(competition, beforeEntries);
            before.IsSuccess.Should().BeTrue();

            var appended = competition.AppendReflightGroup(0, 1, 1, [competitors[0], competitors[1]], "Mid-air collision", Now);
            appended.IsSuccess.Should().BeTrue();
            var afterCompetition = competition.Apply(appended.Value);
            var reflightGroup = appended.Value.Group.Id;

            var afterEntries = beforeEntries.ToDictionary(kv => kv.Key, kv => kv.Value);
            var entitled = OpenAndCapture(afterCompetition, reflightGroup, competitors[0], ReflightRole.Entitled, t.reflightRaw);
            afterEntries[entitled.Id] = entitled;
            var filler = OpenAndCapture(afterCompetition, reflightGroup, competitors[1], ReflightRole.Filler, t.fillerRaw);
            afterEntries[filler.Id] = filler;

            var after = ScoringService.ScoreCompetition(afterCompetition, afterEntries);
            after.IsSuccess.Should().BeTrue();

            foreach (var untouched in competitors.Skip(2))
            {
                var beforeScore = before.Value.Scores.Values.Single(s => s.CompetitorRef == untouched.ToString()).Score;
                var afterScore = after.Value.Scores.Values.Single(s => s.CompetitorRef == untouched.ToString()).Score;
                afterScore.Should().Be(beforeScore);
            }
        });
    }

    // ========================================================== RR1..RR3 — the ruling laws
    // reflight-scoring-rulings.md WI-4. The invariants are named in the story's
    // plan so these tests prove stated laws rather than discover behaviour.

    /// <summary>
    /// RR1 — a ruling fills silences only. For any candidate pair and any ruled
    /// selection: when the role-applicable class slot is NOT
    /// UndefinedRequiresRuling, the selector's outcome is identical to the
    /// no-ruling call. The rulebook always beats the CD.
    /// </summary>
    [Fact]
    public void A_ruled_selection_never_changes_the_outcome_where_the_class_rule_speaks()
    {
        (from original in Gen.Decimal[1, 1000]
         from reflight in Gen.Decimal[1, 1000]
         from role in Gen.OneOfConst(ReflightRole.Entitled, ReflightRole.Filler)
         from slot in Gen.OneOfConst(ReflightSelection.Replacement, ReflightSelection.BetterOf)
         from ruled in Gen.OneOfConst(
             ReflightSelection.Replacement,
             ReflightSelection.BetterOf,
             ReflightSelection.NotPermitted,
             ReflightSelection.UndefinedRequiresRuling)
         select (original, reflight, role, slot, ruled))
        .Sample(t =>
        {
            // The generated slot lands on the generated ROLE's own rule slot;
            // the other slot stays defined too, so both calls always succeed.
            var rule = t.role == ReflightRole.Entitled
                ? new ReflightRule { EntitledScores = t.slot, OthersScore = ReflightSelection.BetterOf }
                : new ReflightRule { EntitledScores = ReflightSelection.BetterOf, OthersScore = t.slot };
            var candidates = new List<(ReflightRole, decimal)>
            {
                (ReflightRole.Original, t.original),
                (t.role, t.reflight),
            };

            var withoutRuling = ReflightSelector.Select(candidates, rule);
            var withRuling = ReflightSelector.Select(candidates, rule, t.ruled);

            withRuling.IsSuccess.Should().BeTrue();
            withRuling.Value.Should().Be(withoutRuling.Value);
        });
    }

    /// <summary>
    /// RR2′ — the ruled selection law. Where the role-applicable slot IS silent
    /// and a ruled selection applies, the output is exactly the ruled
    /// application: Replacement → the reflight-role candidate's score; BetterOf
    /// → the max of both candidates' scores. (Extension of R2.)
    /// </summary>
    [Fact]
    public void A_ruled_selection_over_a_silent_rule_is_exactly_the_ruled_application()
    {
        (from original in Gen.Decimal[1, 1000]
         from reflight in Gen.Decimal[1, 1000]
         from role in Gen.OneOfConst(ReflightRole.Entitled, ReflightRole.Filler)
         from ruled in Gen.OneOfConst(ReflightSelection.Replacement, ReflightSelection.BetterOf)
         select (original, reflight, role, ruled))
        .Sample(t =>
        {
            var rule = new ReflightRule
            {
                EntitledScores = ReflightSelection.UndefinedRequiresRuling,
                OthersScore = ReflightSelection.UndefinedRequiresRuling,
            };
            var candidates = new List<(ReflightRole, decimal)>
            {
                (ReflightRole.Original, t.original),
                (t.role, t.reflight),
            };

            var result = ReflightSelector.Select(candidates, rule, t.ruled);

            result.IsSuccess.Should().BeTrue();
            var expected = t.ruled == ReflightSelection.BetterOf
                ? Math.Max(t.original, t.reflight)
                : t.reflight;
            result.Value.Should().Be(expected);
        });
    }

    /// <summary>
    /// RR3 — last ruling wins. Folding any sequence of ReflightRulingRecorded
    /// events yields, per (task-round, competitor) key, the selection of the
    /// sequence's FINAL element. Log order is truth.
    /// </summary>
    [Fact]
    public void The_folded_rulings_lookup_per_key_equals_the_final_logged_selection()
    {
        (from keys in Gen.Int[0, 2].Array[2, 8]
         from selections in Gen.OneOfConst(ReflightSelection.Replacement, ReflightSelection.BetterOf).Array[keys.Length]
         select (keys, selections))
        .Sample(t =>
        {
            var competition = BuildRuledCompetition();
            var competitors = RegisteredCompetitors(competition);

            for (var i = 0; i < t.keys.Length; i++)
            {
                var recorded = competition.RecordReflightRuling(new ReflightRuling
                {
                    TaskRound = new TaskRoundCoordinate(0, 1, 1),
                    CompetitorRef = competitors[t.keys[i]],
                    Selection = t.selections[i],
                    Reason = $"Ruling {i + 1}",
                    At = Now.AddMinutes(i),
                });
                recorded.IsSuccess.Should().BeTrue();
                competition = competition.Apply(recorded.Value);
            }

            competition.Rulings.Length.Should().Be(t.keys.Length);

            foreach (var key in t.keys.Distinct())
            {
                var lastAt = t.keys.Select((k, i) => (k, i)).Last(p => p.k == key).i;
                var expected = t.selections[lastAt];

                competition.Rulings.Last(r => r.CompetitorRef == competitors[key]).Selection.Should().Be(expected);
            }
        });
    }

    /// <summary>A minimal NZ-Class-M-shaped competition: a silent × 2 rule and three registered competitors.</summary>
    private static Competition BuildRuledCompetition()
    {
        var definition = MakeClassDefinition(
            ReflightSelection.UndefinedRequiresRuling, ReflightSelection.UndefinedRequiresRuling, minNewGroupSize: 2);
        var adoptedRules = new AdoptedRules
        {
            Definition = definition,
            SourceClassId = "content-hash-synthetic",
            SourceVersion = definition.Version,
            AdoptedAt = Now,
        };
        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Ruling Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15), "1.0.0", adoptedRules, Now));

        for (var i = 0; i < 3; i++)
        {
            competition = competition.Apply(
                competition.RegisterCompetitor(CompetitorId.New(), PersonId.New(), Now).Value);
        }

        var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [] };
        var taskRound = new TaskRound
        {
            Ordinal = 1,
            State = Soarscore.Domain.Competitions.TaskRoundState.Drawn,
            TaskRef = "T",
            Groups = [group],
        };
        var round = new Round { Ordinal = 1, TaskRounds = [taskRound] };
        var draw = new Draw { CreatedAt = Now, Status = "drawn" };

        return competition.Apply(new PhaseDrawn(0, PhaseType.Preliminary, draw, [round], Now));
    }

    private static ImmutableArray<CompetitorId> RegisteredCompetitors(Competition competition) =>
        competition.Competitors.Select(c => c.Id).ToImmutableArray();
}