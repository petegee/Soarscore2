// kanban/in-progress/teams-mvp.md WI-6. Derived in-handler from the folded
// Competition aggregate — no new read-model document, no projection change
// (teams-mvp.md §Store and validation discipline). The response's two section
// types are deliberately unrelated: the scoring/protection separation is
// structural (teams-mvp.md §Application queries — a scoring team's roster can
// never be accidentally read as a protection group's, mirroring the two
// membership records' separate types on the aggregate). Members are ordered by
// competitor id so the view is deterministic for identical aggregate state.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>One scoring team's roster — members carry their contribution eligibility.</summary>
public sealed record ScoringTeamRosterView(
    ScoringTeamId TeamRef,
    string Name,
    ImmutableArray<ScoringTeamMemberView> Members);

public sealed record ScoringTeamMemberView(CompetitorId CompetitorRef, bool Contributes);

/// <summary>
/// One protection group's roster. Members are bare competitor refs: protection
/// is a draw-only concept with no per-member scoring data (its glossary
/// definition), so there is nothing for a member row to carry.
/// </summary>
public sealed record ProtectionGroupRosterView(
    ProtectionGroupId GroupRef,
    string Name,
    ImmutableArray<CompetitorId> Members);

/// <summary>
/// The GET /competition-teams response shape — two separate sections. Scoring
/// teams and protection groups never share a collection: a competitor holds at
/// most one scoring team but any number of protection groups, and the two
/// vocabularies are unrelated even when a name coincides across kinds.
/// </summary>
public sealed record TeamRostersView(
    ImmutableArray<ScoringTeamRosterView> ScoringTeams,
    ImmutableArray<ProtectionGroupRosterView> ProtectionGroups);

public readonly record struct GetTeamRosters(CompetitionId CompetitionRef) : IQuery<TeamRostersView>;

public sealed class GetTeamRostersHandler(IEventStore eventStore) : IQueryHandler<GetTeamRosters, TeamRostersView>
{
    public async Task<Result<TeamRostersView>> HandleAsync(GetTeamRosters query, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<TeamRostersView>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var competition = loaded.Value.Competition;

        var scoringTeams = competition.ScoringTeams
            .Select(team => new ScoringTeamRosterView(
                team.Id,
                team.Name,
                competition.ScoringTeamMemberships
                    .Where(m => m.TeamRef == team.Id)
                    .OrderBy(m => m.CompetitorRef.Value)
                    .Select(m => new ScoringTeamMemberView(m.CompetitorRef, m.Contributes))
                    .ToImmutableArray()))
            .ToImmutableArray();

        var protectionGroups = competition.ProtectionGroups
            .Select(group => new ProtectionGroupRosterView(
                group.Id,
                group.Name,
                competition.ProtectionGroupMemberships
                    .Where(m => m.GroupRef == group.Id)
                    .Select(m => m.CompetitorRef)
                    .OrderBy(c => c.Value)
                    .ToImmutableArray()))
            .ToImmutableArray();

        return Result<TeamRostersView>.Success(new TeamRostersView(scoringTeams, protectionGroups));
    }
}
