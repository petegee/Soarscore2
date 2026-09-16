// The identity rows of the `people` read model — authentication-and-authorisation.md
// WI-6, LADR-0004 §"Identity resolution is a per-request read-model lookup (D2)".
// Not a fifth read model: LADR-0004 records the clarification to LADR-0001 §3
// that identity rows belong to the **people** read model, so the four-model
// inventory stands. One row per (Provider, Subject) link — what the WI-9
// current-user middleware resolves per request (D2) and what the store's
// unique compound index on (Provider, Subject) (WI-8) arbitrates, exactly the
// PersonSummary.Email precedent (LADR-0001 §2): uniqueness across the
// population is the index's job, never this fold's.
//
// PersonIdentityProjection.Apply is a plain function, portable if the store is
// ever swapped, in the PeopleProjection.Apply mould. The Infrastructure shim
// wrapping it is WI-8's concern, not this project's.
//
// Fed only by Person streams. Every row needs the stream's PersonId, and
// IdentityLinked — like every change event — is stream-scoped and carries no
// Id, so the PersonId arrives as a parameter: the shim passes the stream id as
// a PersonId, the same derivation PersonSummaryProjection's LoadCurrentAsync
// already uses to load a PersonSummary by its stream
// (Infrastructure/People/PersonSummaryProjection.cs).

using Soarscore.Domain.People;

namespace Soarscore.Application.Queries.People;

/// <summary>
/// The projected row for one identity link: the (Provider, Subject) pair the
/// IdP guarantees and the one Person it belongs to (glossary "Identity link").
/// A person may hold several rows; a row belongs to exactly one person. The
/// person's roles live on <see cref="PersonSummary"/>, not here — the join of
/// a row and its person's summary is <see cref="PersonIdentityMatch"/>, which
/// WI-8's FindIdentityAsync adapter returns.
/// </summary>
public sealed record PersonIdentityRow(string Provider, string Subject, PersonId PersonId);

public static class PersonIdentityProjection
{
    /// <summary>
    /// Folds one <see cref="PersonEvent"/> onto the current identity row,
    /// mirroring <see cref="PeopleProjection.Apply"/>'s discipline exactly.
    ///
    /// <see cref="IdentityLinked"/> is this row's minting event and creates
    /// unconditionally, ignoring <c>current</c> — the same decision
    /// PeopleProjection's PersonRegistered arm makes for its summary. A
    /// duplicated IdentityLinked therefore folds to an identical row (upsert,
    /// no error): tolerance for a replayed event, with the unique index still
    /// the arbiter of genuinely conflicting links.
    ///
    /// <see cref="PersonRegistered"/> mints nothing here — it carries no
    /// identity link — and as a creation event it never trips the change-event
    /// Require. Every other (change) event carries nothing this row folds, but
    /// the change-event rule holds unchanged: it requires a current row, and
    /// none is the same ArgumentException PeopleProjection's Require throws.
    /// </summary>
    public static PersonIdentityRow? Apply(PersonIdentityRow? current, PersonEvent @event, PersonId personId) =>
        @event switch
        {
            IdentityLinked e => new PersonIdentityRow(e.Provider, e.Subject, personId),
            PersonRegistered => current,
            RoleGranted or RoleRevoked or PersonRenamed or ContactDetailsChanged or ClubAffiliationChanged
                => Require(current, @event),
            _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
        };

    private static PersonIdentityRow Require(PersonIdentityRow? current, PersonEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} projected with no current identity row — a change event can never be first in the stream.");
}
