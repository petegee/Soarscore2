// The Marten adapter for IEntryQuery — docs/plans/capture-a-score-steel-thread-plan.md
// WI-9. Reads the `entry_index` read model only; never the event log. Mirrors
// Competitions/MartenCompetitionsQuery.cs.
//
// Filtering happens in memory, over every EntrySummary row, rather than as a
// server-side Marten `Where` on CompetitionRef/GroupRef/CompetitorRef — found
// to be necessary by WI-12's store-backed tests against a real Postgres.
// Those three properties are strong-typed ids (readonly record struct
// wrapping Guid) serialised as a JSON object ({"value": "..."}) like any
// other record — no custom JsonConverter narrows them to a bare scalar
// (SoarscoreEventJson.cs only special-cases decimal/enum/NumberOrParam/
// FlagOrParam). Marten's strong-typed-identifier LINQ support duck-types any
// `...Id`-suffixed type it sees in a `Where` and maps the whole property
// straight onto a `uuid` SQL parameter, assuming bare-scalar storage; against
// this project's actual (nested-object) storage shape that both throws
// outright (InvalidCastException writing the wrapper struct as a raw Npgsql
// parameter) and, once Marten does resolve a value, fails server-side
// ("invalid input syntax for type uuid" — Marten casts the JSON *object*
// text, having never drilled into `.value`). Explicitly comparing `.Value`
// does not help: Marten's duck-typing recognises the parent property's type
// before it looks at the member chain and takes the same shortcut regardless
// of how deep the access goes. Loading every row for the whole store and
// filtering here in plain C# sidesteps the mismatch entirely, and is a
// legitimate trade at this project's scale — a competition's entire
// entry_index is at most a few hundred rows (≤20 pilots, ≤8 rounds/day,
// docs/non-functional-requirements.md's Key constraints).
//
// PhaseOrdinal/RoundOrdinal/TaskRoundOrdinal are plain `int`s and remain
// ordinary LINQ predicates — only the three id-typed filters are affected.

using Marten;
using Soarscore.Application.Queries.Entries;
using Soarscore.Domain.Competitions;

namespace Soarscore.Infrastructure.Entries;

public sealed class MartenEntryQuery(IDocumentStore store) : IEntryQuery
{
    public async Task<IReadOnlyList<EntrySummary>> FindAsync(
        CompetitionId competitionRef,
        int? phaseOrdinal,
        int? roundOrdinal,
        int? taskRoundOrdinal,
        GroupId? groupRef,
        CompetitorId? competitorRef,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        var all = await session.Query<EntrySummary>().ToListAsync(cancellationToken);

        IEnumerable<EntrySummary> results = all.Where(s => s.CompetitionRef == competitionRef);

        if (phaseOrdinal is not null)
        {
            results = results.Where(s => s.PhaseOrdinal == phaseOrdinal.Value);
        }

        if (roundOrdinal is not null)
        {
            results = results.Where(s => s.RoundOrdinal == roundOrdinal.Value);
        }

        if (taskRoundOrdinal is not null)
        {
            results = results.Where(s => s.TaskRoundOrdinal == taskRoundOrdinal.Value);
        }

        if (groupRef is not null)
        {
            results = results.Where(s => s.GroupRef == groupRef.Value);
        }

        if (competitorRef is not null)
        {
            results = results.Where(s => s.CompetitorRef == competitorRef.Value);
        }

        return results.ToList();
    }
}
