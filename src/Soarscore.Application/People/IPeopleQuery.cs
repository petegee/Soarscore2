// The `people` read model's query port — docs/plans/command-side-steel-thread-plan.md
// WI-5, LADR-0001 §4.2. Defined here, implemented in Soarscore.Infrastructure
// against Marten; IDocumentSession never appears above that project.
//
// Deliberately no get-by-id method: high-level-architecture.md is explicit
// that querying by ID folds the stream. `GetPerson` (WI-6) goes through
// IEventStore, not this interface — this exists solely for the cross-stream
// lookups a single stream cannot answer.

namespace Soarscore.Application.People;

public interface IPeopleQuery
{
    Task<PersonSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonSummary>> SearchByNameAsync(string name, CancellationToken cancellationToken = default);
}
