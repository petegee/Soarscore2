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
}
