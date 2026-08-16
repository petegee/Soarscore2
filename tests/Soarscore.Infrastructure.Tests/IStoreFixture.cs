// kanban/completed/multi-backend-deployment.md WI-6.
//
// Every store-backed test in this project is written against this interface
// rather than against a particular fixture, so the whole suite runs unchanged on
// every backend Soarscore claims to support. That is the support claim: not a
// separate Fisher suite proving Fisher-shaped things, but the existing tests —
// the ones that already encode which behaviours matter — run twice and passing
// both times.
//
// The first five members are the Application-layer ports, and are the entire
// surface almost every test needs. The last two are the exception, and are the
// reason this is an interface rather than a base class: dropping a read model
// and replaying it (MartenEventStoreTests' fourth test, LADR-0001 §4.10) has no
// port, because it is an operations concern rather than an application one. Each
// fixture answers it with its own store's admin API, and the test that uses it
// stays store-agnostic.

using Soarscore.Application;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Queries.People;

namespace Soarscore.Infrastructure.Tests;

public interface IStoreFixture
{
    IEventStore EventStore { get; }

    IPeopleQuery PeopleQuery { get; }

    IClassLibraryQuery ClassLibraryQuery { get; }

    ICompetitionsQuery CompetitionsQuery { get; }

    /// <summary>capture-a-score-steel-thread-plan.md WI-12 — the entry_index query port.</summary>
    IEntryQuery EntryQuery { get; }

    /// <summary>
    /// Drops one read model's documents, leaving the event log untouched
    /// (LADR-0001 §4.10: read models are dropped and replayed, never migrated).
    /// No <c>IPeopleQuery</c>/<c>IEventStore</c> port exposes this and none
    /// should — it is an operator's action, not the application's.
    /// </summary>
    Task DropDocumentsAsync<TDocument>(CancellationToken cancellationToken)
        where TDocument : notnull;

    /// <summary>
    /// Replays the whole event log through one named Inline projection, on
    /// demand — never via a continuously-running async daemon (LADR-0001 §2).
    /// The name is the one pinned at registration in each store's composition
    /// root, and means the same thing on every backend.
    /// </summary>
    Task RebuildProjectionAsync(string projectionName, CancellationToken cancellationToken);
}
