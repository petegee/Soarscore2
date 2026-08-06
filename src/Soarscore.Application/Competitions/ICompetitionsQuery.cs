// The `competitions` read model's query port — docs/plans/create-competition-steel-thread-plan.md
// WI-1, LADR-0001 §4.2. Defined here, implemented in Soarscore.Infrastructure
// against Marten; IDocumentSession never appears above that project. Mirrors
// People/IPeopleQuery.cs and CompetitionClasses/IClassLibraryQuery.cs.
//
// Deliberately no get-by-id method: high-level-architecture.md is explicit
// that querying by ID folds the stream. GetCompetition (WI-3) goes through
// IEventStore, not this interface — this exists solely for the cross-stream
// listing/filtering a single stream cannot answer.

namespace Soarscore.Application.Competitions;

public interface ICompetitionsQuery
{
    Task<IReadOnlyList<CompetitionSummary>> SearchAsync(DateOnly? onOrAfter, string? classContentHash, CancellationToken cancellationToken = default);
}
