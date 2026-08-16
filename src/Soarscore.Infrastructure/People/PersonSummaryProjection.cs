// The Inline projection for the `people` read model —
// kanban/completed/command-side-steel-thread-plan.md WI-7, LADR-0001 §2/§4.3.
//
// Portable ballast: all it does is group the raw events the store hands it back
// into per-stream order and replay them through Application's
// PeopleProjection.Apply, which is the only part of this that would survive a
// store swap. Registered Inline (never Async — LADR-0001 §2, the Person email
// uniqueness invariant is provable only inside the append transaction).
//
// kanban/completed/jasperfx-shared-store-contracts.md WI-3 split this in two.
// The fold below is now written against JasperFx's store-agnostic
// IJasperFxProjection<TOperations> / IDocumentWriteOperations, so "portable
// ballast" is no longer aspirational — nothing in the fold names Marten. Each
// store's *registration* API still wants its own marker interface, so a small
// per-store shim carries it; MartenPersonSummaryProjection at the foot of this
// file is the only thing here that mentions Marten. WI-6 found that shim also has
// to override the document load — see LoadCurrentAsync below, which is where the
// whole strong-typed-id finding is written down for all four projections.

using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;

namespace Soarscore.Infrastructure.People;

internal class PersonSummaryProjection<TOperations> : IJasperFxProjection<TOperations>
    where TOperations : IDocumentWriteOperations
{
    public async Task ApplyAsync(TOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        foreach (var stream in events.GroupBy(e => e.StreamId))
        {
            var current = await LoadCurrentAsync(operations, stream.Key, cancellation);
            foreach (var e in stream.OrderBy(e => e.Version))
            {
                if (e.Data is PersonEvent personEvent)
                {
                    current = PeopleProjection.Apply(current, personEvent);
                }
            }

            if (current is not null)
            {
                operations.Store(current);
            }
        }
    }

    // === The strong-typed-id seam. Explained once, here; the other projections
    // === cross-reference this comment rather than repeat it.
    //
    // kanban/completed/jasperfx-shared-store-contracts.md WI-6.
    //
    // JasperFx's IDocumentReadOperations deliberately exposes only Guid and String
    // identity overloads — its own remarks say so: the two the measured consumer
    // surface uses, with numeric and tenant-scoped reads left to be added additively
    // later. There is no overload that takes an arbitrary id value.
    //
    // Marten satisfies that contract with its *existing* members, so the contract's
    // LoadAsync<T>(Guid) is Marten's own LoadAsync<T>(Guid) — which statically binds
    // TId to Guid and resolves storage via QuerySession.StorageFor<T, Guid>().
    // PersonSummary.Id is a PersonId, so that lookup throws
    // DocumentIdTypeMismatchException ("the id type for the included document type
    // PersonSummary is PersonId, but Guid was used"). Confirmed empirically on
    // 2026-08-16: WI-3 shipped the fold calling the shared Guid overload directly and
    // all 8 acceptance tests failed against the Testcontainers PostgreSQL on exactly
    // that exception. Inferred first from the assembly metadata, then proven.
    //
    // So this is a real limit of the shared contract, not a Marten quirk. Any backend
    // that stores these documents under a strong-typed id has the same problem and
    // needs the same override — the seam is the honest place to put it, and it keeps
    // the fold above store-agnostic. Same shape WI-4 uses for the .Events accessor:
    // narrow protected member, defaulted to the shared contract, overridden per store.
    protected virtual async Task<PersonSummary?> LoadCurrentAsync(
        TOperations operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<PersonSummary>(streamId, cancellation);
}

// Registration marker plus the one per-store override. Marten.Events.Projections.IProjection
// is IJasperFxProjection<Marten.IDocumentOperations> plus IMartenRegistrable, which declares
// no instance members — the base class above satisfies every member of both.
internal sealed class MartenPersonSummaryProjection
    : PersonSummaryProjection<Marten.IDocumentOperations>, Marten.Events.Projections.IProjection
{
    // Marten's runtime-dispatching LoadAsync<T>(object id) overload — "load a single
    // document of type T by a user supplied id". A PersonId declares no conversion to
    // Guid, so the strong-typed value binds here and nowhere else, and the id type is
    // resolved against the configured storage at run time rather than statically. This
    // is what the projection called before WI-3; see LoadCurrentAsync above for why the
    // shared contract cannot express it.
    protected override async Task<PersonSummary?> LoadCurrentAsync(
        Marten.IDocumentOperations operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<PersonSummary>(new PersonId(streamId), cancellation);
}

// The Fisher/SQLite shim — kanban/completed/multi-backend-deployment.md WI-3.
// Fisher.Projections.IProjection is IJasperFxProjection<Fisher.IDocumentSession>
// and declares no members of its own, so the fold above satisfies it whole; the
// type parameter differs from Marten's only because each store names the write
// session type it hands its projections.
//
// The strong-typed-id override is here for the same reason it is on the Marten
// shim, and its presence on BOTH is the evidence for what LoadCurrentAsync's
// comment claims: this is a limit of the shared JasperFx contract, not a Marten
// quirk. Fisher's LoadAsync<T, TId>(TId) is the counterpart of Marten's
// runtime-dispatching LoadAsync<T>(object) — a different way to escape the
// Guid-only identity overloads, reaching the same place.
internal sealed class FisherPersonSummaryProjection
    : PersonSummaryProjection<Fisher.IDocumentSession>, Fisher.Projections.IProjection
{
    protected override async Task<PersonSummary?> LoadCurrentAsync(
        Fisher.IDocumentSession operations, Guid streamId, CancellationToken cancellation)
        => await operations.LoadAsync<PersonSummary, PersonId>(new PersonId(streamId), cancellation);
}
