// docs/plans/create-competition-steel-thread-plan.md WI-3. Served by folding
// the stream, never from the `competitions` read model —
// high-level-architecture.md: "If querying by ID, then you must use load the
// stream." This is the one query ICompetitionsQuery deliberately has no
// method for (ICompetitionsQuery.cs). Mirrors People/GetPerson.cs.
//
// docs/plans/phase-drawn-steel-thread-plan.md WI-6a extended the result with
// PairwiseCoOccurrence — computed here, on read, from the folded Competition;
// not stored or denormalised, and not a new query (the plan's explicit
// stance: GET /competition gains a field, rather than growing a sibling
// endpoint).

using System.Collections.Immutable;
using Soarscore.Application.Shared.Competitions;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;

namespace Soarscore.Application.Queries.Competitions;

/// <summary>The GET /competition response shape: the folded aggregate plus a read-only derivation over it.</summary>
public sealed record CompetitionView(Competition Competition, ImmutableArray<PairwiseCoOccurrenceEntry> PairwiseCoOccurrence);

public readonly record struct GetCompetition(CompetitionId Id) : IQuery<CompetitionView>;

public sealed class GetCompetitionHandler(IEventStore eventStore) : IQueryHandler<GetCompetition, CompetitionView>
{
    public async Task<Result<CompetitionView>> HandleAsync(GetCompetition query, CancellationToken cancellationToken)
    {
        var loaded = await CompetitionLoader.LoadAsync(eventStore, query.Id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<CompetitionView>.Failure(loaded.Code!, loaded.Message!, loaded.Defects);
        }

        var competition = loaded.Value.Competition;

        // Flattened across every phase this competition has, rather than
        // hardcoded to Phases[0]: correct today (this thread only ever
        // produces one phase) and stays correct once a flyoff-phase draw
        // exists, with nothing here to revisit.
        var rounds = competition.Phases.SelectMany(p => p.Rounds).ToImmutableArray();
        var pairwiseCoOccurrence = PairwiseCoOccurrence.ComputeEntries(rounds);

        return Result<CompetitionView>.Success(new CompetitionView(competition, pairwiseCoOccurrence));
    }
}
