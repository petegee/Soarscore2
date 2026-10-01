// ui_competition-roster-read.md. One competition-scoped read, open to any
// authenticated caller, returning every competitor's display name alongside
// competitor number and withdrawn flag — with no contact details or identity
// links.
//
// Why a new read rather than names on the existing views: /competition,
// /competition-result and /draw-matrix carry IDs only, and widening them
// would push names to callers that never asked. A cacheable, ordered-by-number
// roster keeps the PII surface to exactly one endpoint.
//
// Read-model discipline (LADR-0001 §3): fold the competition stream
// (CompetitionLoader — existence and the competitor map come from the fold,
// never a read model) → batch IPeopleQuery.FindByIdsAsync for the names →
// project PersonSummary.Name only. Never Email/Phone/HomeCity/Roles/ClubName.
// ClubName is deliberately excluded: the UI did not require it and it is
// PII-adjacent. A person missing from the read model degrades to
// "competitor #n", the GetCompetitionEventLog precedent.
//
// GetDrawMatrix.cs's "client joins via existing people reads" note predates
// the D4 narrowing (/people is organiser-only now) — this read is the join
// non-organisers use.

using System.Collections.Immutable;
using Soarscore.Application.Queries.People;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>
/// One fielded pilot: the display name for a competitor number, plus whether
/// the competitor has withdrawn. Carries no contact details, no club, no
/// identity links — Name is the only person-sourced field.
/// </summary>
public sealed record CompetitionRosterEntry(
    CompetitorId CompetitorRef,
    int CompetitorNumber,
    string Name,
    bool Withdrawn);

/// <summary>The GET /competition-roster response shape, ordered by CompetitorNumber asc.</summary>
public sealed record CompetitionRosterView(
    CompetitionId CompetitionRef,
    ImmutableArray<CompetitionRosterEntry> Entries);

public readonly record struct GetCompetitionRoster(CompetitionId CompetitionRef) : IQuery<CompetitionRosterView>;

public sealed class GetCompetitionRosterHandler(IEventStore eventStore, IPeopleQuery peopleQuery)
    : IQueryHandler<GetCompetitionRoster, CompetitionRosterView>
{
    public async Task<Result<CompetitionRosterView>> HandleAsync(
        GetCompetitionRoster query, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, query.CompetitionRef, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionRosterView>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var competition = loaded.Value.Competition;

        var namesByPerson = (await peopleQuery.FindByIdsAsync(
                competition.Competitors.Select(c => c.PersonRef).ToImmutableArray(), cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

        var entries = competition.Competitors
            .OrderBy(c => c.CompetitorNumber)
            .Select(c => new CompetitionRosterEntry(
                c.Id,
                c.CompetitorNumber,
                namesByPerson.TryGetValue(c.PersonRef, out var name)
                    ? name
                    : $"competitor #{c.CompetitorNumber}",
                c.WithdrawnAt is not null))
            .ToImmutableArray();

        return Result<CompetitionRosterView>.Success(new CompetitionRosterView(query.CompetitionRef, entries));
    }
}
