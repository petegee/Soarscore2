// The `people` read model's query port — kanban/completed/command-side-steel-thread-plan.md
// WI-5, LADR-0001 §4.2. Defined here, implemented in Soarscore.Infrastructure
// against Marten; IDocumentSession never appears above that project.
//
// Deliberately no get-by-id method: high-level-architecture.md is explicit
// that querying by ID folds the stream. `GetPerson` (WI-6) goes through
// IEventStore, not this interface — this exists solely for the cross-stream
// lookups a single stream cannot answer.
//
// `FindByIdsAsync` (kanban/in-progress/competition-event-log-endpoint.md WI-2)
// is a cross-stream lookup in exactly that sense, not a get-by-id: the caller
// has already loaded a competition's streams and needs the names that live on
// person streams to render readable summaries. It returns read-model summaries
// for *labelling* — never a substitute for GetPerson's authoritative fold.

using Soarscore.Domain.People;

namespace Soarscore.Application.Queries.People;

public interface IPeopleQuery
{
    Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The names for a batch of people in one round trip — the
    /// competition-event-log merge resolves every CompetitorId's person in a
    /// single call. People absent from the read model are simply missing from
    /// the result; the caller renders what it has.
    /// </summary>
    Task<IReadOnlyList<PersonSummary>> FindByIdsAsync(IReadOnlyList<PersonId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// One validated token's identity resolved against the read model (D2,
    /// authentication-and-authorisation.md WI-5): roles never live in tokens,
    /// so the WI-9 current-user middleware calls this per request and a role
    /// grant takes effect on the next request with no token-refresh dance.
    /// Null when no identity link matches the (provider, subject) pair.
    /// </summary>
    Task<IdentityMatch?> FindIdentityAsync(string provider, string subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many people hold a role — WI-7's RevokeRole last-organiser guard
    /// (revoking the only organiser would strand the system's authority; the
    /// count is a cross-stream read guarding a UX deadlock, not an aggregate
    /// invariant, and is race-tolerant — WI-7/tech-debt.md).
    /// </summary>
    Task<int> CountByRoleAsync(PersonRole role, CancellationToken cancellationToken = default);
}

/// <summary>
/// The identity→person join behind <see cref="IPeopleQuery.FindIdentityAsync"/>:
/// one (provider, subject) link with the person it belongs to and that
/// person's roles in the same read. WI-6 names the fuller join row
/// (PersonIdentityMatch); this is the WI-5 port shape the pipeline's world
/// consumes.
/// </summary>
public sealed record IdentityMatch(PersonId PersonId, IReadOnlyList<PersonRole> Roles);
