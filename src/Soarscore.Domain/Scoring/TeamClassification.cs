// TeamClassificationEngine — teams-mvp.md WI-5 (Option 2 MVP).
//
// Pure, downstream of individual ranking (paper design principle 2): it reads
// the competition-scope final aggregate — exactly what
// ScoringService.ScoreCompetition produces — and never participates in flight
// scoring, normalisation, or phase aggregation. All class variance it needs is
// the competition-level TeamClassificationConfiguration; it never branches on
// a competition class (NFR-1/NFR-2).
//
// The MVP method is bestThreeScoreSum: each team's Total is the sum of its
// three highest individual aggregate scores among eligible members holding a
// competition placing; ties between teams break on the placing sum of the
// contributors, then best individual placing, then a shared place ordered by
// team name. Disqualified members hold no placing and cannot contribute;
// membership is not auto-cleared on withdrawal, so a withdrawn member whose
// score and placing still stand in the individual classification keeps
// contributing — the engine sees neither withdrawal nor anything else about
// the Competition aggregate, only the individual result and membership data.

using System.Collections.Immutable;
using Soarscore.Domain.Competitions;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Why a team member is or is not counting toward the team total — design
/// principle 8's "counting members and tie-break evidence, not an unexplained
/// team total".
/// </summary>
public enum TeamContributionState
{
    /// <summary>One of the (up to three) members whose scores form the Total.</summary>
    Contributor,

    /// <summary>Eligible (<c>Contributes</c>) with a competition placing, but outside the top three.</summary>
    EligibleNotCounting,

    /// <summary>
    /// Membership says <c>Contributes == false</c> — the defending-champion-style
    /// member who competes alongside their team without contributing to it.
    /// </summary>
    Ineligible,

    /// <summary>The individual classification carries no score for this member yet.</summary>
    NoScoreYet,

    /// <summary>The member's final score is flagged disqualified — no placing, cannot contribute.</summary>
    Disqualified,
}

/// <summary>One counting member: their individual aggregate and competition placing.</summary>
public sealed record TeamContributor
{
    public required CompetitorId CompetitorRef { get; init; }

    public required decimal Score { get; init; }

    public required int Placing { get; init; }
}

/// <summary>Every team member, with the state explaining their (non-)contribution.</summary>
public sealed record TeamMemberContribution
{
    public required CompetitorId CompetitorRef { get; init; }

    public required TeamContributionState State { get; init; }
}

/// <summary>One team's derived standing in the classification.</summary>
public sealed record TeamStanding
{
    public required ScoringTeamId TeamRef { get; init; }

    public required string Name { get; init; }

    /// <summary>Sum of contributor scores; 0 when the team has no contributors.</summary>
    public required decimal Total { get; init; }

    /// <summary>
    /// Shared-place convention identical to <see cref="RankingEngine"/>: teams
    /// equal on every rung below share the place, and the next place skips the
    /// group size.
    /// </summary>
    public required int Placing { get; init; }

    /// <summary>Sum of contributor placings — the first team tie-break rung; 0 when there are no contributors.</summary>
    public required int PlacingSum { get; init; }

    /// <summary>The best (lowest) contributor placing — the second tie-break rung; null when there are no contributors.</summary>
    public required int? BestIndividualPlacing { get; init; }

    /// <summary>The counting members, best first.</summary>
    public required ImmutableArray<TeamContributor> Contributors { get; init; }

    /// <summary>Every member of the team, ordered by competitor id.</summary>
    public required ImmutableArray<TeamMemberContribution> Members { get; init; }
}

/// <summary>The full derived team classification for a competition.</summary>
public sealed record TeamClassificationResult
{
    /// <summary>
    /// Every defined team, in classification order. Empty when classification
    /// is disabled or never configured — a state, not an error.
    /// </summary>
    public required ImmutableArray<TeamStanding> Standings { get; init; }

    /// <summary>The classification method the standings were computed with (result metadata, paper §Option 2 risk table).</summary>
    public required string Method { get; init; }

    /// <summary>
    /// Which individual classification the standings derive from. The MVP has
    /// exactly one source — the competition-scope final aggregate (post-drops,
    /// post-aggregate-penalties) — named by
    /// <see cref="TeamClassificationEngine.SourceCompetitionFinalAggregate"/>;
    /// the label is data so Option 3 can add phase sources without reshaping
    /// this record.
    /// </summary>
    public required string SourceClassification { get; init; }
}

/// <summary>
/// Classifies scoring teams from an already-ranked individual result.
/// </summary>
public static class TeamClassificationEngine
{
    /// <summary>The one MVP member of the closed method vocabulary (owner decision 7).</summary>
    public const string MethodBestThreeScoreSum = "bestThreeScoreSum";

    /// <summary>The competition-scope final aggregate — the MVP's only classification source.</summary>
    public const string SourceCompetitionFinalAggregate = "competitionFinalAggregate";

    /// <summary>How many of a team's eligible members count toward the Total (MVP: fixed at three).</summary>
    private const int ContributorCount = 3;

    private readonly record struct Member(CompetitorId Ref, bool Eligible);

    private readonly record struct Candidate(CompetitorId Ref, decimal Score, int Placing);

    /// <summary>
    /// Derive team standings from the individual classification.
    /// </summary>
    /// <param name="individual">
    /// The competition-scope final aggregate — post-drops, post-aggregate-penalties,
    /// ranked: exactly what <c>ScoringService.ScoreCompetition</c> produces.
    /// </param>
    /// <param name="teams">Every scoring team defined for the competition.</param>
    /// <param name="memberships">At most one per competitor (0..1 scoring team each).</param>
    /// <param name="config">The competition-level classification policy; null until first configured.</param>
    public static Result<TeamClassificationResult> Classify(
        CompetitionResult individual,
        ImmutableArray<ScoringTeam> teams,
        ImmutableArray<ScoringTeamMembership> memberships,
        TeamClassificationConfiguration? config)
    {
        // Disabled or never configured: empty standings are a state, not an
        // error — and the unknown-method guard belongs to the running
        // classification, so it is not raised here (WI-6's standings query
        // surfaces this state as derived = null, never as a failure).
        if (config is null || !config.Enabled)
        {
            return Result<TeamClassificationResult>.Success(new TeamClassificationResult
            {
                Standings = [],
                Method = config?.Method ?? string.Empty,
                SourceClassification = SourceCompetitionFinalAggregate,
            });
        }

        // Forward-compat guard: a token this build does not know must never be
        // silently misinterpreted as some other policy.
        if (!string.Equals(config.Method, MethodBestThreeScoreSum, StringComparison.Ordinal))
        {
            return Result<TeamClassificationResult>.Failure(
                "teamClassification.unknownMethod",
                $"Team classification method '{config.Method}' is not known to this build; "
                + $"the only supported method is '{MethodBestThreeScoreSum}'.");
        }

        // Eligibility: Contributes is the defending-champion switch. The decide
        // functions and fold guarantee at most one membership per competitor,
        // so duplicates cannot arrive from the aggregate; grouping is purely
        // defensive, with OR so no input order can leak into the outcome.
        var membersByTeam = memberships
            .GroupBy(m => m.TeamRef)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(m => m.CompetitorRef)
                    .Select(c => new Member(c.Key, Eligible: c.Any(m => m.Contributes)))
                    .ToList());

        var standings = new List<TeamStanding>(teams.Length);

        foreach (var team in teams)
        {
            var members = membersByTeam.TryGetValue(team.Id, out var found)
                ? found
                : [];

            // Contributors: the three highest aggregate scores among eligible
            // members holding a competition placing. Equal scores are refined
            // by the better competition placing (the individual ranking's own
            // ladder already separated what it could), then competitor id as
            // the deterministic last resort — so no input order can decide.
            var candidates = new List<Candidate>();
            foreach (var member in members)
            {
                var refString = member.Ref.ToString();
                if (!individual.Scores.TryGetValue(refString, out var final))
                    continue;
                if (final.Disqualified)
                    continue;
                if (!member.Eligible)
                    continue;
                if (!individual.Placings.TryGetValue(refString, out var placing))
                    continue;
                candidates.Add(new Candidate(member.Ref, final.Score, placing));
            }

            candidates.Sort((a, b) =>
            {
                var c = b.Score.CompareTo(a.Score);
                if (c != 0) return c;
                c = a.Placing.CompareTo(b.Placing);
                if (c != 0) return c;
                return a.Ref.Value.CompareTo(b.Ref.Value);
            });

            var chosen = candidates.Take(ContributorCount).ToList();

            var contributorStates = chosen
                .Select(c => new TeamContributor
                {
                    CompetitorRef = c.Ref,
                    Score = c.Score,
                    Placing = c.Placing,
                })
                .ToImmutableArray();

            var chosenRefs = chosen.Select(c => c.Ref).ToHashSet();

            var memberStates = members
                .OrderBy(m => m.Ref.Value)
                .Select(m => new TeamMemberContribution
                {
                    CompetitorRef = m.Ref,
                    State = StateOf(individual, m, chosenRefs),
                })
                .ToImmutableArray();

            standings.Add(new TeamStanding
            {
                TeamRef = team.Id,
                Name = team.Name,
                Total = chosen.Sum(c => c.Score),
                Placing = 0, // assigned below, shared-place convention
                PlacingSum = chosen.Sum(c => c.Placing),
                BestIndividualPlacing = chosen.Count == 0 ? null : chosen.Min(c => c.Placing),
                Contributors = contributorStates,
                Members = memberStates,
            });
        }

        // Team order: Total DESC → PlacingSum ASC → BestIndividualPlacing ASC
        // (nulls last) → team Name ASC as the deterministic display order for
        // shared places. The name is not a rung: teams equal on the three
        // rungs share a place, name-ordered within it — mirroring
        // RankingEngine's skip-ahead numbering. The trailing team id keeps the
        // sort total (the aggregate guarantees unique names; the engine does
        // not assume them).
        standings.Sort((a, b) =>
        {
            var c = b.Total.CompareTo(a.Total);
            if (c != 0) return c;
            c = a.PlacingSum.CompareTo(b.PlacingSum);
            if (c != 0) return c;
            c = CompareBestPlacing(a.BestIndividualPlacing, b.BestIndividualPlacing);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Name, b.Name);
            if (c != 0) return c;
            return a.TeamRef.Value.CompareTo(b.TeamRef.Value);
        });

        var placed = ImmutableArray.CreateBuilder<TeamStanding>(standings.Count);
        int place = 1;
        int i = 0;
        while (i < standings.Count)
        {
            int j = i + 1;
            while (j < standings.Count && SameRungs(standings[i], standings[j]))
                j++;

            for (int k = i; k < j; k++)
                placed.Add(standings[k] with { Placing = place });

            place += j - i;
            i = j;
        }

        return Result<TeamClassificationResult>.Success(new TeamClassificationResult
        {
            Standings = placed.MoveToImmutable(),
            Method = config.Method,
            SourceClassification = SourceCompetitionFinalAggregate,
        });
    }

    private static TeamContributionState StateOf(
        CompetitionResult individual, Member member, HashSet<CompetitorId> chosen)
    {
        if (!individual.Scores.TryGetValue(member.Ref.ToString(), out var final))
            return TeamContributionState.NoScoreYet;

        if (final.Disqualified)
            return TeamContributionState.Disqualified;

        if (!member.Eligible)
            return TeamContributionState.Ineligible;

        return chosen.Contains(member.Ref)
            ? TeamContributionState.Contributor
            : TeamContributionState.EligibleNotCounting;
    }

    /// <summary>ASC with nulls last — null means "no contributor placing to compare".</summary>
    private static int CompareBestPlacing(int? a, int? b) =>
        a.HasValue
            ? b.HasValue ? a.Value.CompareTo(b.Value) : -1
            : b.HasValue ? 1 : 0;

    private static bool SameRungs(TeamStanding a, TeamStanding b) =>
        a.Total == b.Total
        && a.PlacingSum == b.PlacingSum
        && a.BestIndividualPlacing.Equals(b.BestIndividualPlacing);
}
