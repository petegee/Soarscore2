// The `people` read model — kanban/completed/command-side-steel-thread-plan.md WI-5,
// LADR-0001 §3/§4.3. One of the four read models the ADR permits; it exists
// solely so email/name can be queried across the whole population, which no
// single stream can answer for itself (Domain/People/Person.cs).
// authentication-and-authorisation.md WI-6 added the system-level roles the
// summary now carries, and the identity rows folded beside it — same read
// model, not a fifth: LADR-0004 §D2 records that clarification
// (People/PersonIdentityProjection.cs).
//
// PeopleProjection.Apply is a plain function, portable if the store is ever
// swapped (LADR-0001 §4.3/§5). The Marten IProjection shim wrapping it is
// Infrastructure's concern (WI-7), not this project's.

using Soarscore.Domain.People;

namespace Soarscore.Application.Queries.People;

/// <summary>
/// The projected row for one person. A read-side denormalisation of
/// <see cref="Person"/> — nothing here is authoritative; <c>GetPerson</c>
/// (WI-6) resolves by folding the stream, never from this document.
/// </summary>
public sealed record PersonSummary(
    PersonId Id,
    string Name,
    string Email,
    string? Phone,
    string? HomeCity,
    string? ClubName,
    /// <summary>
    /// The system-level roles the person holds (WI-3's RoleGranted/RoleRevoked
    /// events, authentication-and-authorisation.md WI-6) — positional-parameter
    /// append, deliberately without a default: a read-model row names every
    /// column it carries (CompetitionSummary.CapturePolicy precedent). Empty
    /// until a grant arrives; folded with the aggregate's set semantics, so a
    /// duplicated grant never duplicates the entry.
    /// </summary>
    IReadOnlyList<PersonRole> Roles);

public static class PeopleProjection
{
    /// <summary>
    /// Folds one <see cref="PersonEvent"/> onto the current summary, or
    /// creates it from <see cref="PersonRegistered"/>. Mirrors
    /// <see cref="Person.Apply(Person?, PersonEvent)"/>'s shape exactly —
    /// same non-null-current-for-a-change-event rule, same reasoning. Roles
    /// fold from RoleGranted/RoleRevoked with the aggregate's set semantics
    /// (Add/Remove on a set: a duplicate grant or a revoke of an unheld role
    /// folds to no change — those are the decide's refusals, not the fold's);
    /// IdentityLinked changes nothing here because the summary carries no
    /// links — they are <see cref="PersonIdentityRow"/> rows of the same read
    /// model — but the event still obeys the change-event rule.
    /// </summary>
    public static PersonSummary? Apply(PersonSummary? current, PersonEvent @event) =>
        @event switch
        {
            PersonRegistered e => new PersonSummary(e.Id, e.Name, e.Contact.Email, e.Contact.Phone, e.Contact.HomeCity, e.Club?.ClubName, []),
            PersonRenamed e => Require(current, e) with { Name = e.Name },
            ContactDetailsChanged e => Require(current, e) with { Email = e.Contact.Email, Phone = e.Contact.Phone, HomeCity = e.Contact.HomeCity },
            ClubAffiliationChanged e => Require(current, e) with { ClubName = e.Club?.ClubName },
            RoleGranted e => Grant(current, e),
            RoleRevoked e => Revoke(current, e),
            IdentityLinked e => Require(current, e),
            _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
        };

    private static PersonSummary Require(PersonSummary? current, PersonEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} projected with no current summary — a change event can never be first in the stream.");

    // Set semantics mirroring Person.Apply's ImmutableHashSet.Add/Remove:
    // idempotent under replay in both directions. The decides refuse a grant
    // already held (person.roleAlreadyHeld) and a revoke of an unheld role
    // (person.roleNotHeld); the fold tolerates both, exactly as a replayed
    // event must.
    private static PersonSummary Grant(PersonSummary? current, RoleGranted @event)
    {
        var summary = Require(current, @event);
        return summary.Roles.Contains(@event.Role)
            ? summary
            : summary with { Roles = [.. summary.Roles, @event.Role] };
    }

    private static PersonSummary Revoke(PersonSummary? current, RoleRevoked @event)
    {
        var summary = Require(current, @event);
        return summary.Roles.Contains(@event.Role)
            ? summary with { Roles = [.. summary.Roles.Where(role => role != @event.Role)] }
            : summary;
    }
}
