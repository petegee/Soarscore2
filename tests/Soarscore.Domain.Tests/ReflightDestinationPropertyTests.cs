// Property tests for the destination-aware scoring law — named invariants
// R1′, R5, R6 and R7 of kanban/in-progress/reflight-aggregate-destination.md
// WI-1 (CLAUDE.md: "a named invariant is what makes the property test
// meaningful"). CsCheck, in ReflightSelectionPropertyTests' style: a small
// synthetic class whose task is un-normalised (raw pass-through), so totals
// and aggregated cell sums are directly comparable to the emitted cells.
//
// R1′ supersedes reflight-groups.md's R1, which is its all-null special case.

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

public class ReflightDestinationPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableArray<MetricDefinition> MetricDefs =
        [new MetricDefinition { Name = "raw", Kind = MeasuredKind.Number }];

    private static readonly ImmutableArray<ScoreTerm> ScoreTerms =
        [(ScoreTerm)new RateTerm { MetricRef = "raw", Rate = 1 }];

    private static TaskDefinition MakeTask(string code) => new()
    {
        Code = code,
        Name = $"Test task {code}",
        Metrics = MetricDefs,
        Flights = new LastFlight(),
        Timing = new TaskTiming { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = ScoreTerms,
    };

    private static ClassDefinition MakeClassDefinition(ImmutableArray<TaskDefinition> tasks) => new()
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
                Tasks = tasks,
            },
        ],
    };

    /// <summary>
    /// Three registered competitors; one Drawn group per round holding all of
    /// them, one task-round (ordinal 1, task "T") per round 1..<paramref name="totalRounds"/>.
    /// </summary>
    private static (Competition Competition, List<CompetitorId> Competitors, IReadOnlyDictionary<int, GroupId> GroupByRound) BuildCompetition(
        int totalRounds)
    {
        var adoptedRules = new AdoptedRules
        {
            Definition = MakeClassDefinition([MakeTask("T")]),
            SourceClassId = "content-hash-synthetic",
            SourceVersion = "1.0",
            AdoptedAt = Now,
        };

        var competition = Competition.Create(new CompetitionCreated(
            CompetitionId.New(), "Make-up Destination Property Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15), "1.0.0", adoptedRules, Now));

        var competitors = new List<CompetitorId>();
        for (var i = 0; i < 3; i++)
        {
            var id = CompetitorId.New();
            competition = competition.Apply(competition.RegisterCompetitor(id, PersonId.New(), Now).Value);
            competitors.Add(id);
        }

        var groupByRound = new Dictionary<int, GroupId>();
        var rounds = ImmutableArray.CreateBuilder<Round>();
        for (var ordinal = 1; ordinal <= totalRounds; ordinal++)
        {
            var group = new Group { Id = GroupId.New(), Ordinal = 1, CompetitorRefs = [.. competitors] };
            groupByRound[ordinal] = group.Id;
            rounds.Add(new Round
            {
                Ordinal = ordinal,
                TaskRounds =
                [
                    new TaskRound
                    {
                        Ordinal = 1,
                        State = Soarscore.Domain.Competitions.TaskRoundState.Drawn,
                        TaskRef = "T",
                        Groups = [group],
                    },
                ],
            });
        }

        competition = competition.Apply(new PhaseDrawn(
            0, PhaseType.Preliminary, new Draw { CreatedAt = Now, Status = "drawn" },
            rounds.ToImmutable(), Now));
        // Entries open only against an accepted draw (D4) — arrangement here.
        competition = competition.Apply(new DrawAccepted(0, Now));

        return (competition, competitors, groupByRound);
    }

    private static Entry CaptureEntry(
        Competition competition,
        GroupId group,
        CompetitorId competitor,
        int roundOrdinal,
        decimal raw,
        ReflightRole role = ReflightRole.Original,
        int? countsForRoundOrdinal = null)
    {
        var opened = competition.OpenEntry(
            EntryId.New(), 0, roundOrdinal, 1, group, competitor, role, Now,
            countsForRoundOrdinal,
            countsForRoundOrdinal is null ? null : "make-up for a missed round").Value;
        var entry = Entry.Create(opened).Apply(new FlightOpened(1, Now));
        var captured = entry.CaptureMeasurement(1, "raw", MeasuredValue.Of(raw), Now, MetricDefs);
        captured.IsSuccess.Should().BeTrue();
        return entry.Apply(captured.Value);
    }

    private static decimal Total(Result<CompetitionResult> result, CompetitorId competitor) =>
        result.Value.Scores[competitor.ToString()].Score;

    // ========================================================== R1′ — one score per destination slot

    /// <summary>
    /// R1′: for any live-entry multiset, the pipeline yields at most one cell
    /// per (competitor, task-round, destination) and one cell per distinct
    /// destination — one make-up per missed round, each collapsed exactly per
    /// the class rule, and the own-round candidates (Original ± its ordinary
    /// reflight companion) collapsing to exactly ONE hosting-round cell. The
    /// competitor misses every round but the last; each missed round's slot is
    /// filled by its make-up's real score (no synthesised zero anywhere), so
    /// the total is exactly the per-destination sum the law prescribes.
    /// </summary>
    [Fact]
    public void One_cell_per_destination_and_the_own_round_candidates_collapse_to_one()
    {
        (from totalRounds in Gen.Int[2, 3]
         from companion in Gen.OneOfConst<ReflightRole?>(null, ReflightRole.Entitled, ReflightRole.Filler)
         from originalCents in Gen.Int[100, 100000]
         from companionCents in Gen.Int[100, 100000]
         select (totalRounds, companion, originalCents, companionCents))
        .Sample(t =>
        {
            // Cent-bounded raws keep every summation exact, so the total pins
            // the cell multiset rather than decimal-association artefacts.
            var originalRaw = t.originalCents / 100m;
            var companionRaw = t.companionCents / 100m;

            var (competition, competitors, groups) = BuildCompetition(t.totalRounds);
            var pilot = competitors[0];

            var entries = new Dictionary<EntryId, Entry>();
            // The pilot's hosting task-round: his Original, an optional
            // ordinary reflight companion (null destination), and one make-up
            // per missed round with a distinct destination. Distinct raws
            // (200 + 37·destination) keep every cell's contribution
            // identifiable in the sum.
            var original = CaptureEntry(competition, groups[t.totalRounds], pilot, t.totalRounds, originalRaw);
            entries[original.Id] = original;
            if (t.companion is { } role)
            {
                var companionEntry = CaptureEntry(
                    competition, groups[t.totalRounds], pilot, t.totalRounds, companionRaw, role);
                entries[companionEntry.Id] = companionEntry;
            }

            for (var destination = 1; destination < t.totalRounds; destination++)
            {
                var makeUp = CaptureEntry(
                    competition, groups[t.totalRounds], pilot, t.totalRounds, 200m + 37m * destination,
                    ReflightRole.Entitled, countsForRoundOrdinal: destination);
                entries[makeUp.Id] = makeUp;
            }

            // The field flies every round, so every destination round is walked.
            foreach (var competitor in competitors.Skip(1))
            {
                for (var round = 1; round <= t.totalRounds; round++)
                {
                    var entry = CaptureEntry(competition, groups[round], competitor, round, 100m);
                    entries[entry.Id] = entry;
                }
            }

            var result = ScoringService.ScoreCompetition(competition, entries);

            result.IsSuccess.Should().BeTrue();

            // The hosting destination's cell is the old law applied verbatim to
            // the own-round candidates (R6's identical-selection guarantee).
            var hostingCandidates = new List<(ReflightRole, decimal)> { (ReflightRole.Original, originalRaw) };
            if (t.companion is { } companionRole)
                hostingCandidates.Add((companionRole, companionRaw));
            var rule = new ReflightRule
            {
                EntitledScores = ReflightSelection.Replacement,
                OthersScore = ReflightSelection.BetterOf,
            };
            var hostingScore = ReflightSelector.Select(hostingCandidates, rule).Value;

            var expectedMakeUps = Enumerable.Range(1, t.totalRounds - 1).Sum(d => 200m + 37m * d);
            Total(result, pilot).Should().Be(hostingScore + expectedMakeUps,
                "exactly one cell per distinct destination: the collapsed hosting slot plus one make-up per missed round");
        });
    }

    // ========================================================== R5 — destination conservation

    /// <summary>
    /// R5: through the drop walk (PhaseAggregator — deliberately unchanged,
    /// D8), Σ AllScores equals the sum of the destination-keyed cells emitted;
    /// the make-up fills the destination round's slot with its real score; the
    /// aggregate plus the dropped scores conserves that sum; and every dropped
    /// cell is a real AllScores cell keyed to its destination round — when the
    /// destination round is dropped, the make-up itself is what drops.
    /// </summary>
    [Fact]
    public void The_drop_walk_conserves_destination_keyed_cells()
    {
        (from makeUpCents in Gen.Int[100, 1000000]
         from round2Cents in Gen.Int[100, 1000000]
         from round3Cents in Gen.Int[100, 1000000]
         select (makeUpCents, round2Cents, round3Cents))
        .Sample(t =>
        {
            // Cent-bounded raws keep every summation exact.
            var makeUp = t.makeUpCents / 100m;
            var round2 = t.round2Cents / 100m;
            var round3 = t.round3Cents / 100m;

            // The competitor missed round 1; the make-up cell keys to round 1
            // while rounds 2 and 3 carry ordinary cells. All three walked.
            var allScores = new Dictionary<string, TaskRoundScore>
            {
                ["1|1|0"] = new("T", 1, 1, makeUp),
                ["2|1|1"] = new("T", 2, 1, round2),
                ["3|1|2"] = new("T", 3, 1, round3),
            };
            var rounds = ImmutableArray.Create(
                new RoundData(1, [new TaskRoundData(1, "T", Scoring.TaskRoundState.Complete)]),
                new RoundData(2, [new TaskRoundData(1, "T", Scoring.TaskRoundState.Complete)]),
                new RoundData(3, [new TaskRoundData(1, "T", Scoring.TaskRoundState.Complete)]));
            var phase = new PhaseDefinition
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new ValidityRule { MinRounds = 1 },
                Drops = [new DropPolicy { Dimension = DropDimension.ByRound, DropCount = 1 }],
                Tasks = [MakeTask("T")],
            };

            var scores = ScoringService.Aggregate("pilot", phase, rounds, allScores);

            scores.AllScores.Length.Should().Be(3,
                "every walked slot has a real cell — no synthesised zero");
            scores.AllScores.Sum(s => s.Score).Should().Be(makeUp + round2 + round3,
                "Σ AllScores equals Σ destination-keyed cells");
            scores.AllScores.Should().ContainSingle(s =>
                s.RoundOrdinal == 1 && s.TaskCode == "T" && s.TaskOrdinal == 1 && s.Score == makeUp,
                "the make-up fills the destination slot with its real score");

            (scores.Aggregate + scores.DroppedScores.Sum(s => s.Score))
                .Should().Be(scores.AllScores.Sum(s => s.Score),
                    "the drop walk conserves the destination-keyed total");

            foreach (var dropped in scores.DroppedScores)
            {
                scores.AllScores.Should().Contain(s =>
                    s.RoundOrdinal == dropped.RoundOrdinal
                    && s.TaskCode == dropped.TaskCode
                    && s.TaskOrdinal == dropped.TaskOrdinal
                    && s.Score == dropped.Score,
                    "every dropped cell is a real cell keyed to its destination round");
            }

            if (scores.DroppedScores.Any(d => d.RoundOrdinal == 1))
            {
                scores.DroppedScores.Should().ContainSingle(d =>
                    d.RoundOrdinal == 1 && d.Score == makeUp,
                    "when the destination round drops, the make-up itself is the dropped cell");
            }
        });
    }

    // ========================================================== R6 — no-destination equivalence

    /// <summary>
    /// R6: when every live entry's counts-for is null, the destination-aware
    /// law accepts exactly the shapes the old two-role law accepted — every
    /// entry resolves to the same destination (the task-round's own round), so
    /// the per-destination law degenerates to the old whole-list law, for any
    /// role multiset and any round ordinal.
    /// </summary>
    [Fact]
    public void With_no_counts_for_set_the_destination_aware_law_equals_the_old_law()
    {
        (from roles in Gen.OneOfConst(
                ReflightRole.Original, ReflightRole.Entitled, ReflightRole.Filler).Array[1, 4]
         from roundOrdinal in Gen.Int[1, 3]
         select (roles, roundOrdinal))
        .Sample(t =>
        {
            var withNullDestinations = t.roles
                .Select(role => (role, (int?)null))
                .ToList();

            ReflightSelector.ShapePermits(t.roundOrdinal, withNullDestinations)
                .Should().Be(ReflightSelector.ShapePermits(t.roles));
        });
    }

    // ========================================================== R7 — loud failures

    /// <summary>
    /// R7: a destination that is not an earlier round of the phase — zero,
    /// negative, the entry's own round, or any later round — is refused by the
    /// destination-aware shape law with score.reflightShapeUnsupported, never
    /// scored silently (the write side's openEntry.destinationNotEarlier is
    /// WI-2's belt over the same law).
    /// </summary>
    [Fact]
    public void A_non_earlier_destination_fails_loudly()
    {
        (from hostingRound in Gen.Int[2, 3]
         from kind in Gen.Int[0, 6]
         select (hostingRound, kind))
        .Sample(t =>
        {
            var destination = t.kind switch
            {
                0 => 0,
                1 => -2,
                _ => t.hostingRound + (t.kind - 2), // own round or any later round
            };

            var (competition, competitors, groups) = BuildCompetition(3);
            var pilot = competitors[0];

            var entries = new Dictionary<EntryId, Entry>();
            var original = CaptureEntry(competition, groups[t.hostingRound], pilot, t.hostingRound, 500m);
            entries[original.Id] = original;
            var makeUp = CaptureEntry(competition, groups[t.hostingRound], pilot, t.hostingRound, 400m,
                ReflightRole.Entitled, countsForRoundOrdinal: destination);
            entries[makeUp.Id] = makeUp;
            foreach (var competitor in competitors.Skip(1))
            {
                for (var round = 1; round <= 3; round++)
                {
                    var entry = CaptureEntry(competition, groups[round], competitor, round, 100m);
                    entries[entry.Id] = entry;
                }
            }

            var result = ScoringService.ScoreCompetition(competition, entries);

            result.IsFailure.Should().BeTrue();
            result.Code.Should().Be("score.reflightShapeUnsupported",
                $"destination {destination} is not an earlier round of the phase than {t.hostingRound}");
        });
    }
}
