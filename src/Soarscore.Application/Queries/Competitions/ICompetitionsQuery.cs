// The `competitions` read model's query port — kanban/completed/create-competition-steel-thread-plan.md
// WI-1, LADR-0001 §4.2. Defined here, implemented in Soarscore.Infrastructure
// against Marten; IDocumentSession never appears above that project. Mirrors
// People/IPeopleQuery.cs and CompetitionClasses/IClassLibraryQuery.cs.
//
// Deliberately no get-by-id method: high-level-architecture.md is explicit
// that querying by ID folds the stream. GetCompetition (WI-3) goes through
// IEventStore, not this interface — this exists solely for the cross-stream
// listing/filtering a single stream cannot answer.

using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

public interface ICompetitionsQuery
{
    Task<IReadOnlyList<CompetitionSummary>> SearchAsync(DateOnly? onOrAfter, string? classContentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// The competition's configured capture policy (D10,
    /// authentication-and-authorisation.md WI-5) — the capture-policy policy's
    /// one read. Null when none has been configured (or the competition itself
    /// is missing): the policy evaluates null as OrganisersOnly, the safe
    /// default, and the handler's authoritative *.notFound is still reachable
    /// through the organiser half.
    /// </summary>
    Task<CapturePolicy?> FindCapturePolicyAsync(CompetitionId competitionRef, CancellationToken cancellationToken = default);
}
