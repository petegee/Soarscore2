// The Inline projection for the identity rows of the `people` read model —
// authentication-and-authorisation.md WI-8, wrapping Application's
// PersonIdentityProjection.Apply fold (WI-6). The rows belong to the `people`
// read model, not a fifth read model — LADR-0004 §D2 records that
// clarification to LADR-0001 §3 — and they exist so two things no single
// stream can answer for itself become possible: the per-request identity
// resolution D2 needs (IPeopleQuery.FindIdentityAsync's first lookup), and
// the D5 link arbiter — the (Provider, Subject) compound unique index each
// composition root declares on the row document (MartenConfig.cs /
// FisherConfig.cs, the PersonSummary.Email precedent, LADR-0001 §2). A second
// person claiming an identity already linked fails INSIDE the append
// transaction, and the per-store event-store adapters translate that into
// eventStore.uniqueConstraintViolation, which LinkSignIn's bounded retry
// resolves on.
//
// kanban/completed/jasperfx-shared-store-contracts.md WI-3's split applies as
// for the other four projections: the fold below is store-agnostic ballast
// against IJasperFxProjection<TOperations> / IDocumentWriteOperations, and
// the two per-store shims at the foot of this file (Marten* /
// Fisher*PersonIdentityProjection) are the only things here that name a
// store — registration markers, and nothing else.
//
// === Why this fold needs no LoadCurrentAsync (no strong-typed-id seam) =====
//
// The other four projections load the current document by stream id and
// replay onto it; that load is where the shared contract's Guid-only
// identity overloads bite (the full finding is on
// People/PersonSummaryProjection.cs's LoadCurrentAsync). This one loads
// nothing: Application's fold mints the row unconditionally from the
// IdentityLinked event — `IdentityLinked e => new PersonIdentityRow(...)`,
// ignoring `current` — so the row's content is a pure function of the event
// and the stream id, and the document is stored (upserted) under a key
// derived from the row itself. No LoadAsync call, no seam to override.

using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;

namespace Soarscore.Infrastructure.People;

/// <summary>
/// The stored form of one <see cref="PersonIdentityRow"/>. Application's row
/// is the fold's output shape and deliberately carries no document key — the
/// key is this projection's storage decision, derived here so the two stores
/// see one consistent identity for the row.
/// </summary>
/// <remarks>
/// The key is (PersonId, Provider, Subject) — the person FIRST — and that
/// ordering is the whole design. Keying by (Provider, Subject) alone would
/// turn the D5 arbiter into a silent link theft: a second person claiming the
/// same identity would resolve to the first person's document id and UPSERT
/// it — the append would succeed and the link would quietly change owners.
/// Keyed by person first, a cross-person claim is a NEW document whose insert
/// then violates the (Provider, Subject) unique index, which is exactly the
/// rejection the arbiter exists to produce. The same key derivation is also
/// what makes a duplicated IdentityLinked on the SAME stream fold tolerantly
/// (WI-6's fold tolerance): the identical event derives the identical
/// document id, so the second Store is an update of the same row, not a
/// second row.
///
/// The provider and subject are percent-escaped before joining, so the '|'
/// separators are structurally unambiguous: providers and subjects are
/// caller/IdP-supplied strings, subjects legitimately contain '|' (the raw
/// Auth0 sub form), and BindIdentity (D12) takes both free-hand. ("a|b", "c")
/// and ("a", "b|c") must never derive one key. Uri.EscapeDataString leaves
/// only RFC 3986 unreserved characters, so the escaped segments cannot
/// themselves contain a separator.
/// </remarks>
internal sealed record PersonIdentityRowDocument(string Id, string Provider, string Subject, PersonId PersonId)
{
    public static PersonIdentityRowDocument From(PersonIdentityRow row) =>
        new(Key(row.PersonId, row.Provider, row.Subject), row.Provider, row.Subject, row.PersonId);

    private static string Key(PersonId personId, string provider, string subject) =>
        $"{personId.Value:N}|{Uri.EscapeDataString(provider)}|{Uri.EscapeDataString(subject)}";
}

internal class PersonIdentityProjection<TOperations> : IJasperFxProjection<TOperations>
    where TOperations : IDocumentWriteOperations
{
    public Task ApplyAsync(TOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
    {
        // Change events (PersonRenamed, RoleGranted, ContactDetailsChanged, …)
        // are deliberately NOT fed to the fold, unlike every summary shim,
        // where they must be because the summary's content moves with them.
        // Here they carry nothing the row folds — the fold returns its current
        // unchanged for every one of them — so skipping them stores exactly
        // what folding them would store. And they MUST be skipped when no row
        // exists: a person may legitimately hold change events while having
        // minted no identity row (the pre-provisioned person of D12 —
        // RegisterPerson + GrantRole/BindIdentity-later), and the fold's
        // Require would fire there as a false alarm, breaking GrantRole and
        // RenamePerson for every not-yet-linked person. The fold keeps its
        // Require for its own totality (and WI-6's tests); the shim just never
        // hands it a case the store cannot actually be in.
        //
        // Rows are independent documents keyed by their own (person, provider,
        // subject) — not one document per stream like the four summaries — so
        // there is nothing to group by stream and no per-stream replay order
        // to preserve: each minting event stores exactly one row, wherever it
        // appears in the batch.
        foreach (var e in events)
        {
            if (e.Data is not IdentityLinked linked)
            {
                continue;
            }

            // The WI-6 fold, with the PersonId the stream supplies — the same
            // stream-id-as-PersonId derivation PersonSummaryProjection's
            // LoadCurrentAsync uses, recorded on the fold itself. IdentityLinked
            // is the row's minting event and creates unconditionally, so
            // `current` is null here by construction and the fold ignores it.
            var row = PersonIdentityProjection.Apply(null, linked, new PersonId(e.StreamId))!;

            operations.Store(PersonIdentityRowDocument.From(row));
        }

        return Task.CompletedTask;
    }
}

// Registration marker only — no load override to carry across the seam, per
// the header note. Marten.Events.Projections.IProjection is
// IJasperFxProjection<Marten.IDocumentOperations> plus IMartenRegistrable,
// which declares no instance members — the base class above satisfies every
// member of both.
internal sealed class MartenPersonIdentityProjection
    : PersonIdentityProjection<Marten.IDocumentOperations>, Marten.Events.Projections.IProjection;

// The Fisher/SQLite shim — kanban/completed/multi-backend-deployment.md WI-3.
// Fisher.Projections.IProjection is IJasperFxProjection<Fisher.IDocumentSession>
// and declares no members of its own, so the fold above satisfies it whole.
internal sealed class FisherPersonIdentityProjection
    : PersonIdentityProjection<Fisher.IDocumentSession>, Fisher.Projections.IProjection;
