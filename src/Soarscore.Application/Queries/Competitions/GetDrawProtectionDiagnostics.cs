// kanban/in-progress/teams-mvp.md WI-6. Owner decision 5's read side: the draw
// returns the least-bad draw (minimum protection violations) and the
// violations become visible here instead of blocking the draw — the CD accepts
// or rejects through the existing draw accept/reject path with this in hand.
//
// Purely derived, never stored: for every LIVE phase (Phases holds only live
// phases — a rejected draw's phase is removed, D2), per round/task-round/
// group, any protected pair co-grouped in that group is one diagnostic row.
// Generated and prescribed draws are read identically — Group.CompetitorRefs
// is all this query sees, never draw history (that stays deferred,
// kanban/deferred-decisions.md §Draw). The pair set mirrors
// Competition.DeriveProtectedPairs — the private helper DrawPhase feeds
// PhaseDraw — so the diagnostic is measured against exactly the protection the
// draw saw: protection membership is frozen once a phase exists (the
// addProtectionMember.drawExists gate), and withdrawn members can never appear
// in a drawn group anyway, so the live-member filter only keeps the set
// identical to the one the draw was built from.

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>One protected pair found co-grouped — where it happened, and who is affected.</summary>
public sealed record DrawProtectionViolationView(
    int PhaseOrdinal,
    int RoundOrdinal,
    int TaskRoundOrdinal,
    int GroupOrdinal,
    /// <summary>The pair, in ProtectedPair's canonical order (smaller id first).</summary>
    CompetitorId CompetitorA,
    CompetitorId CompetitorB);

/// <summary>The GET /draw-diagnostics response shape — empty when the live draw co-groups no protected pair.</summary>
public sealed record DrawProtectionDiagnosticsView(ImmutableArray<DrawProtectionViolationView> Violations);

public readonly record struct GetDrawProtectionDiagnostics(CompetitionId CompetitionRef)
    : IQuery<DrawProtectionDiagnosticsView>;

public sealed class GetDrawProtectionDiagnosticsHandler(IEventStore eventStore)
    : IQueryHandler<GetDrawProtectionDiagnostics, DrawProtectionDiagnosticsView>
{
    public async Task<Result<DrawProtectionDiagnosticsView>> HandleAsync(
        GetDrawProtectionDiagnostics query, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<DrawProtectionDiagnosticsView>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var competition = loaded.Value.Competition;
        var protectedPairs = DeriveProtectedPairs(competition);

        var violations = ImmutableArray.CreateBuilder<DrawProtectionViolationView>();

        foreach (var phase in competition.Phases)
        {
            foreach (var round in phase.Rounds)
            {
                foreach (var taskRound in round.TaskRounds)
                {
                    foreach (var group in taskRound.Groups)
                    {
                        for (var i = 0; i < group.CompetitorRefs.Length; i++)
                        {
                            for (var j = i + 1; j < group.CompetitorRefs.Length; j++)
                            {
                                var pair = new ProtectedPair(group.CompetitorRefs[i], group.CompetitorRefs[j]);
                                if (protectedPairs.Contains(pair))
                                {
                                    violations.Add(new DrawProtectionViolationView(
                                        phase.Ordinal, round.Ordinal, taskRound.Ordinal, group.Ordinal,
                                        pair.A, pair.B));
                                }
                            }
                        }
                    }
                }
            }
        }

        return Result<DrawProtectionDiagnosticsView>.Success(
            new DrawProtectionDiagnosticsView(violations.ToImmutable()));
    }

    /// <summary>
    /// The same derivation Competition.DeriveProtectedPairs performs for the
    /// draw itself — union over protection groups of the unordered pairs of
    /// live (registered, not withdrawn) members, canonicalised by
    /// <see cref="ProtectedPair"/>'s constructor so plain set equality dedups
    /// whatever the group shapes overlap on. Deliberately duplicated in
    /// Application rather than widened on the aggregate: the aggregate exposes
    /// no read-surface helper today and this query is a pure read over its
    /// state.
    /// </summary>
    private static ImmutableArray<ProtectedPair> DeriveProtectedPairs(Competition competition)
    {
        var live = competition.Competitors
            .Where(c => c.WithdrawnAt is null)
            .Select(c => c.Id)
            .ToHashSet();

        var pairs = new HashSet<ProtectedPair>();
        foreach (var members in competition.ProtectionGroupMemberships
            .GroupBy(m => m.GroupRef)
            .Select(g => g.Select(m => m.CompetitorRef).Where(live.Contains).ToArray()))
        {
            for (var i = 0; i < members.Length; i++)
            {
                for (var j = i + 1; j < members.Length; j++)
                {
                    pairs.Add(new ProtectedPair(members[i], members[j]));
                }
            }
        }

        return [.. pairs];
    }
}
