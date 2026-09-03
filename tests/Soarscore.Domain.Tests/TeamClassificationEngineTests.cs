using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Black-box unit tests for TeamClassificationEngine (teams-mvp.md WI-5):
/// eligibility and omission (the defending-champion case), partial teams,
/// teams with no scored members, zero and negative scores, disqualified
/// members, team-order tie-breaks (placing sum, best individual placing,
/// shared places), the disabled/null configuration states and the
/// teamClassification.unknownMethod forward-compat guard.
/// The individual input is produced by RankingEngine itself — the engine's
/// real upstream, so placings always carry the shared-place convention.
/// </summary>
public class TeamClassificationEngineTests
{
    private static ScoringTeamId TeamId(int n) => new(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)n));

    private static CompetitorId Comp(int n) => new(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)n));

    private static readonly ScoringTeamId AlphaId = TeamId(1);
    private static readonly ScoringTeamId BravoId = TeamId(2);
    private static readonly ScoringTeamId CharlieId = TeamId(3);

    private static ScoringTeam Team(ScoringTeamId id, string name) => new() { Id = id, Name = name };

    private static ScoringTeamMembership Member(CompetitorId competitor, ScoringTeamId team, bool contributes = true) =>
        new() { CompetitorRef = competitor, TeamRef = team, Contributes = contributes };

    private static TeamClassificationConfiguration Config(bool enabled = true) => new()
    {
        Enabled = enabled,
        Method = TeamClassificationEngine.MethodBestThreeScoreSum,
    };

    private static Result<TeamClassificationResult> Classify(
        CompetitionResult individual, ScoringTeam[] teams, ScoringTeamMembership[] memberships,
        TeamClassificationConfiguration? config) =>
        TeamClassificationEngine.Classify(
            individual, teams.ToImmutableArray(), memberships.ToImmutableArray(), config);

    private static CompetitionResult Individual(
        params (CompetitorId Ref, decimal Score, decimal? PreDrop, bool Disqualified)[] rows)
    {
        var scores = rows
            .Select(r => new FinalCompetitorScore(
                r.Ref.ToString(), r.Score, r.PreDrop ?? r.Score, 0m, r.Disqualified))
            .ToImmutableArray();
        return RankingEngine.Rank(scores, null, null, TieBreakContext.Display);
    }

    // ------------------------------------------- Disabled / null configuration

    [Fact]
    public void Null_config_returns_empty_standings_as_a_state_not_an_error()
    {
        var individual = Individual((Comp(1), 1000m, null, false));

        var result = Classify(individual, [Team(AlphaId, "Alpha")], [Member(Comp(1), AlphaId)], null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Standings.Should().BeEmpty();
        result.Value.Method.Should().BeEmpty();
        result.Value.SourceClassification.Should().Be(TeamClassificationEngine.SourceCompetitionFinalAggregate);
    }

    [Fact]
    public void Disabled_config_returns_empty_standings_even_when_the_method_is_unknown()
    {
        // The unknown-method guard belongs to the RUNNING classification; a
        // disabled one never interprets the token, so this is a state first.
        var config = new TeamClassificationConfiguration { Enabled = false, Method = "someFutureMethod" };

        var result = Classify(Individual(), [Team(AlphaId, "Alpha")], [], config);

        result.IsSuccess.Should().BeTrue();
        result.Value.Standings.Should().BeEmpty();
    }

    // ---------------------------------------------------- Unknown method guard

    [Fact]
    public void Unknown_method_on_an_enabled_configuration_is_a_defect()
    {
        var individual = Individual((Comp(1), 1000m, null, false));
        var config = new TeamClassificationConfiguration { Enabled = true, Method = "placingSum" };

        var result = Classify(individual, [Team(AlphaId, "Alpha")], [Member(Comp(1), AlphaId)], config);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("teamClassification.unknownMethod");
        result.Message.Should().Contain("placingSum");
    }

    [Fact]
    public void Method_comparison_is_ordinal_so_a_wrong_case_token_is_unknown()
    {
        var config = new TeamClassificationConfiguration { Enabled = true, Method = "BestThreeScoreSum" };

        var result = Classify(Individual(), [Team(AlphaId, "Alpha")], [], config);

        result.IsFailure.Should().BeTrue();
        result.Code.Should().Be("teamClassification.unknownMethod");
    }

    // ------------------------------------------- Eligibility and omission (WI-5)

    [Fact]
    public void Defending_champion_with_contributes_false_is_omitted_from_the_total()
    {
        // C1 outscored everyone but competes alongside their team without
        // contributing to it — the defending-champion case.
        var individual = Individual(
            (Comp(1), 1000m, null, false),
            (Comp(2), 900m, null, false),
            (Comp(3), 800m, null, false),
            (Comp(4), 700m, null, false));
        var memberships = new[]
        {
            Member(Comp(1), AlphaId, contributes: false),
            Member(Comp(2), AlphaId),
            Member(Comp(3), AlphaId),
            Member(Comp(4), AlphaId),
        };

        var result = Classify(individual, [Team(AlphaId, "Alpha")], memberships, Config());

        result.IsSuccess.Should().BeTrue();
        var standing = result.Value.Standings.Should().ContainSingle().Subject;
        standing.Total.Should().Be(2400m); // 900 + 800 + 700 — never the champion's 1000
        standing.Contributors.Select(c => c.CompetitorRef).Should().Equal(Comp(2), Comp(3), Comp(4));
        standing.Members.Single(m => m.CompetitorRef == Comp(1)).State
            .Should().Be(TeamContributionState.Ineligible);
    }

    [Fact]
    public void Fourth_eligible_member_is_eligible_not_counting()
    {
        var individual = Individual(
            (Comp(1), 1000m, null, false),
            (Comp(2), 900m, null, false),
            (Comp(3), 800m, null, false),
            (Comp(4), 700m, null, false));
        var memberships = new[]
        {
            Member(Comp(1), AlphaId), Member(Comp(2), AlphaId),
            Member(Comp(3), AlphaId), Member(Comp(4), AlphaId),
        };

        var result = Classify(individual, [Team(AlphaId, "Alpha")], memberships, Config());

        var standing = result.Value.Standings.Should().ContainSingle().Subject;
        standing.Total.Should().Be(2700m); // 1000 + 900 + 800
        standing.Contributors.Select(c => c.CompetitorRef).Should().Equal(Comp(1), Comp(2), Comp(3));
        standing.Members.Single(m => m.CompetitorRef == Comp(4)).State
            .Should().Be(TeamContributionState.EligibleNotCounting);
        result.Value.Method.Should().Be(TeamClassificationEngine.MethodBestThreeScoreSum);
    }

    // --------------------------------------------------------- Partial teams

    [Fact]
    public void Two_eligible_members_both_contribute()
    {
        var individual = Individual(
            (Comp(1), 900m, null, false),
            (Comp(2), 800m, null, false));

        var result = Classify(
            individual, [Team(AlphaId, "Alpha")], [Member(Comp(1), AlphaId), Member(Comp(2), AlphaId)], Config());

        var standing = result.Value.Standings.Should().ContainSingle().Subject;
        standing.Total.Should().Be(1700m);
        standing.PlacingSum.Should().Be(3); // placings 1 + 2
        standing.BestIndividualPlacing.Should().Be(1);
        standing.Contributors.Should().HaveCount(2);
    }

    [Fact]
    public void Single_member_team_contributes_alone()
    {
        var individual = Individual(
            (Comp(1), 900m, null, false),
            (Comp(2), 800m, null, false));

        var result = Classify(individual, [Team(AlphaId, "Alpha")], [Member(Comp(2), AlphaId)], Config());

        var standing = result.Value.Standings.Should().ContainSingle().Subject;
        standing.Total.Should().Be(800m);
        standing.PlacingSum.Should().Be(2);
        standing.BestIndividualPlacing.Should().Be(2);
        standing.Contributors.Should().ContainSingle().Which.CompetitorRef.Should().Be(Comp(2));
    }

    // ----------------------------------------------- Teams with nothing scored

    [Fact]
    public void Teams_with_no_or_unscored_members_still_get_a_standing()
    {
        // Absence never prevents a standing: the unscored team and the empty
        // team both appear, with zero contributors and null best placing.
        var individual = Individual();
        var teams = new[] { Team(AlphaId, "Alpha"), Team(BravoId, "Bravo") };
        var memberships = new[] { Member(Comp(1), AlphaId), Member(Comp(2), AlphaId) };

        var result = Classify(individual, teams, memberships, Config());

        result.IsSuccess.Should().BeTrue();
        result.Value.Standings.Should().HaveCount(2);

        var alpha = result.Value.Standings.Single(s => s.TeamRef == AlphaId);
        alpha.Total.Should().Be(0m);
        alpha.PlacingSum.Should().Be(0);
        alpha.BestIndividualPlacing.Should().BeNull();
        alpha.Contributors.Should().BeEmpty();
        alpha.Members.Select(m => m.State).Should().Equal(
            TeamContributionState.NoScoreYet, TeamContributionState.NoScoreYet);

        var bravo = result.Value.Standings.Single(s => s.TeamRef == BravoId);
        bravo.Total.Should().Be(0m);
        bravo.Members.Should().BeEmpty();
    }

    // ------------------------------------------------- Zero and negative scores

    [Fact]
    public void Zero_and_negative_scores_contribute_normally()
    {
        var individual = Individual(
            (Comp(3), 50m, null, false),
            (Comp(1), 0m, null, false),
            (Comp(2), -100m, null, false));
        var teams = new[] { Team(AlphaId, "Alpha"), Team(BravoId, "Bravo") };
        var memberships = new[]
        {
            Member(Comp(1), AlphaId), Member(Comp(2), AlphaId), Member(Comp(3), BravoId),
        };

        var result = Classify(individual, teams, memberships, Config());

        var alpha = result.Value.Standings.Single(s => s.TeamRef == AlphaId);
        alpha.Total.Should().Be(-100m); // 0 + (-100)
        alpha.Contributors.Select(c => c.Score).Should().Equal(0m, -100m);

        var bravo = result.Value.Standings.Single(s => s.TeamRef == BravoId);
        bravo.Total.Should().Be(50m);

        result.Value.Standings.Select(s => s.TeamRef).Should().Equal(BravoId, AlphaId);
    }

    // ------------------------------------------------------ Disqualified members

    [Fact]
    public void Disqualified_member_cannot_contribute_and_reports_disqualified()
    {
        var individual = Individual(
            (Comp(1), 1000m, null, Disqualified: true), // highest score, but flagged
            (Comp(2), 900m, null, false),
            (Comp(3), 800m, null, false),
            (Comp(4), 700m, null, false));
        var memberships = new[]
        {
            Member(Comp(1), AlphaId), Member(Comp(2), AlphaId),
            Member(Comp(3), AlphaId), Member(Comp(4), AlphaId),
        };

        var result = Classify(individual, [Team(AlphaId, "Alpha")], memberships, Config());

        var standing = result.Value.Standings.Should().ContainSingle().Subject;
        standing.Total.Should().Be(2400m); // the disqualified 1000 is out
        standing.Contributors.Select(c => c.CompetitorRef).Should().Equal(Comp(2), Comp(3), Comp(4));
        standing.Members.Single(m => m.CompetitorRef == Comp(1)).State
            .Should().Be(TeamContributionState.Disqualified);
    }

    // --------------------------------------------------- Tie-break evidence

    [Fact]
    public void Placing_sum_and_best_individual_placing_report_the_contributors_evidence()
    {
        var individual = Individual(
            (Comp(10), 1000m, null, false), // placing 1, in no team
            (Comp(1), 900m, null, false),   // placing 2
            (Comp(11), 800m, null, false),  // placing 3, in no team
            (Comp(2), 700m, null, false),   // placing 4
            (Comp(12), 600m, null, false),  // placing 5, in no team
            (Comp(3), 500m, null, false));  // placing 6

        var result = Classify(
            individual, [Team(AlphaId, "Alpha")],
            [Member(Comp(1), AlphaId), Member(Comp(2), AlphaId), Member(Comp(3), AlphaId)], Config());

        var standing = result.Value.Standings.Should().ContainSingle().Subject;
        standing.Total.Should().Be(2100m);
        standing.PlacingSum.Should().Be(12); // 2 + 4 + 6
        standing.BestIndividualPlacing.Should().Be(2);
        standing.Contributors.Select(c => (c.CompetitorRef, c.Score, c.Placing)).Should().Equal(
            (Comp(1), 900m, 2), (Comp(2), 700m, 4), (Comp(3), 500m, 6));
    }

    // ------------------------------------------------------- Team-order tie-breaks

    [Fact]
    public void Equal_totals_break_on_the_lower_placing_sum()
    {
        // Alpha: 1000 (placing 1) + 800 (placing 4) = 1800, placing sum 5.
        // Bravo: two members tied on 900 (both placing 2)  = 1800, placing sum 4.
        var individual = Individual(
            (Comp(1), 1000m, null, false),
            (Comp(2), 900m, null, false),
            (Comp(3), 900m, null, false),
            (Comp(4), 800m, null, false));
        var teams = new[] { Team(AlphaId, "Alpha"), Team(BravoId, "Bravo") };
        var memberships = new[]
        {
            Member(Comp(1), AlphaId), Member(Comp(4), AlphaId),
            Member(Comp(2), BravoId), Member(Comp(3), BravoId),
        };

        var result = Classify(individual, teams, memberships, Config());

        result.Value.Standings.Select(s => s.TeamRef).Should().Equal(BravoId, AlphaId);
        result.Value.Standings[0].PlacingSum.Should().Be(4);
        result.Value.Standings[1].PlacingSum.Should().Be(5);
    }

    [Fact]
    public void Equal_totals_and_placing_sums_break_on_the_best_individual_placing()
    {
        // Alpha: 900 (placing 2) + 400 (placing 6) = 1300, sum 8, best 2.
        // Bravo: 850 (placing 3) + 450 (placing 5) = 1300, sum 8, best 3.
        var individual = Individual(
            (Comp(10), 1000m, null, false),
            (Comp(1), 900m, null, false),
            (Comp(2), 850m, null, false),
            (Comp(11), 800m, null, false),
            (Comp(4), 450m, null, false),
            (Comp(3), 400m, null, false));
        var teams = new[] { Team(AlphaId, "Alpha"), Team(BravoId, "Bravo") };
        var memberships = new[]
        {
            Member(Comp(1), AlphaId), Member(Comp(3), AlphaId),
            Member(Comp(2), BravoId), Member(Comp(4), BravoId),
        };

        var result = Classify(individual, teams, memberships, Config());

        result.Value.Standings.Select(s => s.TeamRef).Should().Equal(AlphaId, BravoId);
        result.Value.Standings[0].PlacingSum.Should().Be(8);
        result.Value.Standings[1].PlacingSum.Should().Be(8);
        result.Value.Standings[0].BestIndividualPlacing.Should().Be(2);
        result.Value.Standings[1].BestIndividualPlacing.Should().Be(3);
    }

    [Fact]
    public void Fully_equal_teams_share_a_place_and_the_next_place_skips()
    {
        // All four 900s tie at placing 2, so Alpha and Bravo are equal on every
        // rung: shared place 1, name-ordered. Totals 1800 put the pair ahead of
        // Charlie's single 1000, whose place skips 2 — shared-place convention,
        // identical to RankingEngine.
        var individual = Individual(
            (Comp(1), 900m, null, false),
            (Comp(2), 900m, null, false),
            (Comp(3), 900m, null, false),
            (Comp(4), 900m, null, false),
            (Comp(9), 1000m, null, false));
        var teams = new[]
        {
            Team(BravoId, "Bravo"), Team(CharlieId, "Charlie"), Team(AlphaId, "Alpha"),
        };
        var memberships = new[]
        {
            Member(Comp(1), AlphaId), Member(Comp(2), AlphaId),
            Member(Comp(3), BravoId), Member(Comp(4), BravoId),
            Member(Comp(9), CharlieId),
        };

        var result = Classify(individual, teams, memberships, Config());

        result.Value.Standings.Select(s => s.TeamRef).Should().Equal(AlphaId, BravoId, CharlieId);
        result.Value.Standings[0].Placing.Should().Be(1);
        result.Value.Standings[1].Placing.Should().Be(1);
        result.Value.Standings[2].Placing.Should().Be(3);
    }

    // ------------------------------------------------- Defensive input handling

    [Fact]
    public void Membership_naming_an_unknown_team_produces_no_standing()
    {
        var individual = Individual((Comp(1), 1000m, null, false));
        var unknownTeam = TeamId(99);
        var teams = new[] { Team(AlphaId, "Alpha") };
        var memberships = new[] { Member(Comp(1), AlphaId), Member(Comp(2), unknownTeam) };

        var result = Classify(individual, teams, memberships, Config());

        result.Value.Standings.Select(s => s.TeamRef).Should().Equal(AlphaId);
    }

    [Fact]
    public void No_teams_produces_empty_standings()
    {
        var result = Classify(Individual((Comp(1), 1000m, null, false)), [], [], Config());

        result.IsSuccess.Should().BeTrue();
        result.Value.Standings.Should().BeEmpty();
    }
}
