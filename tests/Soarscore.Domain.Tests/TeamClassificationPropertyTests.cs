using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property tests for TeamClassificationEngine — the WI-5 invariants named in
/// teams-mvp.md §Property invariants (1, 5, 6, 7), checked across generated
/// inputs rather than examples:
///
/// 5. Contributor selection — a team's contributors are exactly its three
///    highest-scoring eligible members with a competition placing under the
///    declared source classification, whichever order the inputs arrive in.
/// 6. Classification determinism — permuting teams, members, or the
///    individual-result input cannot change contributors, tie-break values, or
///    team order.
/// 7. Partial-result monotonic availability — adding a newly available
///    individual result recomputes the affected standing, and absence never
///    prevents a standing being returned.
/// 1. Individual-score independence — the engine is a pure read of the
///    individual result: arbitrary team/membership/config metadata changes
///    leave every individual score and placing unchanged.
///
/// Competitors are Guid-based CompetitorIds whose stringified value is the
/// scoring CompetitorRef, exactly as production wiring does
/// (entry.CompetitorRef.ToString()); the individual input is ranked by
/// RankingEngine itself — the engine's real upstream — so placings always
/// carry the shared-place convention. Expected contributors are computed by an
/// independent oracle, never by the engine.
/// </summary>
public class TeamClassificationPropertyTests
{
    private static CompetitorId Comp(int n) => new(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(n + 1)));

    private static ScoringTeamId TeamId(int n) => new(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(n + 1)));

    private sealed record Row(CompetitorId Competitor, decimal Score, bool Disqualified);

    private sealed record Scenario(
        ImmutableArray<Row> Field,
        int TeamCount,
        ImmutableArray<int> Assignments,   // competitor index → team index, or -1 for no team
        ImmutableArray<bool> Contributes,
        int Split,                         // how many field results exist "so far"
        int Seed)
    {
        public ImmutableArray<Row> Present => Field.Take(Split).ToImmutableArray();
    }

    private static readonly Gen<Row> RowGen =
        from index in Gen.Int[0, 39]
        from score in Gen.Int[-200_000, 200_000].Select(i => i / 100m)
        from disqualified in Gen.Bool
        select new Row(Comp(index), score, disqualified);

    private static readonly Gen<Scenario> ScenarioGen =
        from field in FieldGen
        from teamCount in Gen.Int[1, 4]
        from assignments in Gen.Int[-1, teamCount - 1].Array[field.Length].Select(a => a.ToImmutableArray())
        from contributes in Gen.Bool.Array[field.Length].Select(a => a.ToImmutableArray())
        from split in Gen.Int[0, field.Length]
        from seed in Gen.Int[0, int.MaxValue]
        select new Scenario(field, teamCount, assignments, contributes, split, seed);

    private static Gen<ImmutableArray<Row>> FieldGen =>
        from raw in RowGen.Array[1, 30]
        select raw
            .GroupBy(r => r.Competitor) // one final score per competitor
            .Select(g => g.First())
            .ToImmutableArray();

    private static readonly TeamClassificationConfiguration Config = new()
    {
        Enabled = true,
        Method = TeamClassificationEngine.MethodBestThreeScoreSum,
    };

    private static CompetitionResult Rank(ImmutableArray<Row> rows) =>
        RankingEngine.Rank(
            rows.Select(r => new FinalCompetitorScore(
                    r.Competitor.ToString(), r.Score, r.Score, 0m, r.Disqualified))
                .ToImmutableArray(),
            null, null, TieBreakContext.Display);

    private static ImmutableArray<ScoringTeam> Teams(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new ScoringTeam { Id = TeamId(i), Name = $"Team {i:00}" })
            .ToImmutableArray();

    private static (ImmutableArray<ScoringTeam> Teams, ImmutableArray<ScoringTeamMembership> Memberships)
        MembershipsOf(Scenario scenario)
    {
        var memberships = new List<ScoringTeamMembership>();
        for (var j = 0; j < scenario.Field.Length; j++)
        {
            if (scenario.Assignments[j] < 0)
                continue;
            memberships.Add(new ScoringTeamMembership
            {
                CompetitorRef = scenario.Field[j].Competitor,
                TeamRef = TeamId(scenario.Assignments[j]),
                Contributes = scenario.Contributes[j],
            });
        }

        return (Teams(scenario.TeamCount), memberships.ToImmutableArray());
    }

    /// <summary>The expected contributors, computed independently of the engine.</summary>
    private static List<(CompetitorId Competitor, decimal Score, int Placing)> ExpectedContributors(
        CompetitionResult individual, IEnumerable<ScoringTeamMembership> members)
    {
        var candidates = new List<(CompetitorId Competitor, decimal Score, int Placing)>();
        foreach (var member in members)
        {
            if (!member.Contributes)
                continue;
            var refr = member.CompetitorRef.ToString();
            if (!individual.Scores.TryGetValue(refr, out var score) || score.Disqualified)
                continue;
            if (!individual.Placings.TryGetValue(refr, out var placing))
                continue;
            candidates.Add((member.CompetitorRef, score.Score, placing));
        }

        return candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Placing)
            .ThenBy(c => c.Competitor.Value)
            .Take(3)
            .ToList();
    }

    private static void AssertStandingEvidence(
        TeamStanding standing, CompetitionResult individual, ImmutableArray<ScoringTeamMembership> memberships)
    {
        var expected = ExpectedContributors(
            individual, memberships.Where(m => m.TeamRef == standing.TeamRef));

        standing.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing))
            .Should().Equal(expected);
        standing.Total.Should().Be(expected.Sum(c => c.Score));
        standing.PlacingSum.Should().Be(expected.Sum(c => c.Placing));
        standing.BestIndividualPlacing.Should()
            .Be(expected.Count == 0 ? null : expected.Min(c => c.Placing));
    }

    private static ImmutableArray<T> Shuffled<T>(IEnumerable<T> items, Random rnd) =>
        items.OrderBy(_ => rnd.Next()).ToImmutableArray();

    // ------------------------------ Invariant 5: contributor selection

    [Fact]
    public void Contributors_are_exactly_the_top_three_eligible_members_with_placings_whatever_the_input_order()
    {
        ScenarioGen.Sample(testCase =>
        {
            var (teams, memberships) = MembershipsOf(testCase);
            var individual = Rank(testCase.Field);

            var result = TeamClassificationEngine.Classify(individual, teams, memberships, Config);
            result.IsSuccess.Should().BeTrue();

            foreach (var standing in result.Value.Standings)
                AssertStandingEvidence(standing, individual, memberships);

            // Input order cannot change them: permute the membership array.
            var rnd = new Random(testCase.Seed);
            var permuted = TeamClassificationEngine.Classify(
                individual, teams, Shuffled(memberships, rnd), Config);
            permuted.IsSuccess.Should().BeTrue();
            permuted.Value.Should().BeEquivalentTo(result.Value, o => o.WithStrictOrdering());
        });
    }

    // ------------------------------ Invariant 6: classification determinism

    [Fact]
    public void Permuting_teams_members_and_individual_rows_cannot_change_contributors_tie_breaks_or_team_order()
    {
        ScenarioGen.Sample(testCase =>
        {
            var (teams, memberships) = MembershipsOf(testCase);
            var individual = Rank(testCase.Field);
            var rnd = new Random(testCase.Seed);

            var baseline = TeamClassificationEngine.Classify(individual, teams, memberships, Config);
            baseline.IsSuccess.Should().BeTrue();

            for (var permutation = 0; permutation < 3; permutation++)
            {
                var permutedIndividual = Rank(Shuffled(testCase.Field, rnd));
                var permutedTeams = Shuffled(teams, rnd);
                var permutedMemberships = Shuffled(memberships, rnd);

                var permuted = TeamClassificationEngine.Classify(
                    permutedIndividual, permutedTeams, permutedMemberships, Config);
                permuted.IsSuccess.Should().BeTrue();

                permuted.Value.Should().BeEquivalentTo(baseline.Value, o => o.WithStrictOrdering());
            }
        });
    }

    // ------------------- Invariant 7: partial-result monotonic availability

    [Fact]
    public void Absence_never_prevents_a_standing_and_adding_results_recomputes_it()
    {
        ScenarioGen.Sample(testCase =>
        {
            var (teams, memberships) = MembershipsOf(testCase);
            var present = testCase.Present;

            // Absence never prevents a standing: a standing exists for every
            // defined team however few results exist yet (NFR-4), contributors
            // name only scored competitors, and not-yet-scored members are
            // visible with the no-score-yet state rather than missing.
            var partial = TeamClassificationEngine.Classify(
                Rank(present), teams, memberships, Config);
            partial.IsSuccess.Should().BeTrue();
            // A standing for every defined team, whatever has been scored
            // (NFR-4); ordering follows the rungs, with tied standings
            // name-ordered (pinned by the unit tests).
            partial.Value.Standings.Select(s => s.TeamRef).Should()
                .BeEquivalentTo(teams.Select(t => t.Id));

            var presentRefs = present.Select(r => r.Competitor.ToString()).ToHashSet();
            foreach (var standing in partial.Value.Standings)
            {
                foreach (var contributor in standing.Contributors)
                    presentRefs.Should().Contain(contributor.CompetitorRef.ToString());

                var teamMemberRefs = memberships
                    .Where(m => m.TeamRef == standing.TeamRef)
                    .Select(m => m.CompetitorRef.ToString())
                    .ToHashSet();
                standing.Members.Select(m => m.CompetitorRef.ToString())
                    .Should().BeEquivalentTo(teamMemberRefs);
                foreach (var member in standing.Members)
                    if (!presentRefs.Contains(member.CompetitorRef.ToString()))
                        member.State.Should().Be(TeamContributionState.NoScoreYet);
            }

            // Adding the remaining results recomputes: contributors and
            // evidence match the independent expectation over the full field,
            // and no scored member is left in the no-score-yet state.
            var fullIndividual = Rank(testCase.Field);
            var full = TeamClassificationEngine.Classify(
                fullIndividual, teams, memberships, Config);
            full.IsSuccess.Should().BeTrue();
            // A standing per team, however the rungs order them.
            full.Value.Standings.Select(s => s.TeamRef).Should()
                .BeEquivalentTo(teams.Select(t => t.Id));

            var fullRefs = testCase.Field.Select(r => r.Competitor.ToString()).ToHashSet();
            foreach (var standing in full.Value.Standings)
            {
                AssertStandingEvidence(standing, fullIndividual, memberships);
                foreach (var member in standing.Members)
                    if (fullRefs.Contains(member.CompetitorRef.ToString()))
                        member.State.Should().NotBe(TeamContributionState.NoScoreYet);
            }
        });
    }

    // --------------------- Invariant 1: individual-score independence (purity)

    [Fact]
    public void Team_membership_and_config_metadata_changes_leave_the_individual_result_untouched()
    {
        ScenarioGen.Sample(testCase =>
        {
            var (teams, memberships) = MembershipsOf(testCase);
            var individual = Rank(testCase.Field);

            var beforeScores = individual.Scores.ToDictionary(p => p.Key, p => p.Value);
            var beforePlacings = individual.Placings.ToDictionary(p => p.Key, p => p.Value);

            // Metadata-only variations: renamed teams, every contribution flag
            // flipped, shuffled memberships, disabled config, null config, and
            // the unknown-method failure path. None may touch the individual
            // classification, and only team-owned fields may move in output.
            var rnd = new Random(testCase.Seed);
            var renamedTeams = teams
                .Select(t => t with { Name = $"Renamed {t.Name}" })
                .ToImmutableArray();
            var flipped = memberships
                .Select(m => m with { Contributes = !m.Contributes })
                .ToImmutableArray();
            var disabledConfig = Config with { Enabled = false };
            var unknownMethodConfig = Config with { Method = "someFutureMethod" };

            TeamClassificationEngine.Classify(individual, teams, Shuffled(memberships, rnd), Config)
                .IsSuccess.Should().BeTrue();
            TeamClassificationEngine.Classify(individual, renamedTeams, memberships, Config)
                .IsSuccess.Should().BeTrue();
            TeamClassificationEngine.Classify(individual, teams, flipped, Config)
                .IsSuccess.Should().BeTrue();

            var disabled = TeamClassificationEngine.Classify(
                individual, teams, memberships, disabledConfig);
            disabled.IsSuccess.Should().BeTrue();
            disabled.Value.Standings.Should().BeEmpty();

            var nullConfig = TeamClassificationEngine.Classify(
                individual, teams, memberships, null);
            nullConfig.IsSuccess.Should().BeTrue();
            nullConfig.Value.Standings.Should().BeEmpty();

            TeamClassificationEngine.Classify(individual, teams, memberships, unknownMethodConfig)
                .IsFailure.Should().BeTrue();

            // With every contribution flag off, no team total can move —
            // metadata alone never manufactures a score.
            var noneContribute = memberships
                .Select(m => m with { Contributes = false })
                .ToImmutableArray();
            var none = TeamClassificationEngine.Classify(
                individual, teams, noneContribute, Config);
            none.IsSuccess.Should().BeTrue();
            none.Value.Standings.Should().OnlyContain(s => s.Total == 0m && s.Contributors.IsEmpty);

            // Purity: the individual classification is structurally unchanged.
            individual.Scores.Keys.Should().BeEquivalentTo(beforeScores.Keys);
            foreach (var (refr, score) in beforeScores)
                individual.Scores[refr].Should().Be(score);
            individual.Placings.Keys.Should().BeEquivalentTo(beforePlacings.Keys);
            foreach (var (refr, placing) in beforePlacings)
                individual.Placings[refr].Should().Be(placing);
        });
    }
}
