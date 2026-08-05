// The `people` read model — docs/plans/command-side-steel-thread-plan.md WI-5,
// LADR-0001 §3/§4.3. One of the four read models the ADR permits; it exists
// solely so email/name can be queried across the whole population, which no
// single stream can answer for itself (Domain/People/Person.cs).
//
// PeopleProjection.Apply is a plain function, portable if the store is ever
// swapped (LADR-0001 §4.3/§5). The Marten IProjection shim wrapping it is
// Infrastructure's concern (WI-7), not this project's.

using Soarscore.Domain.People;

namespace Soarscore.Application.People;

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
    string? ClubName);

public static class PeopleProjection
{
    /// <summary>
    /// Folds one <see cref="PersonEvent"/> onto the current summary, or
    /// creates it from <see cref="PersonRegistered"/>. Mirrors
    /// <see cref="Person.Apply(Person?, PersonEvent)"/>'s shape exactly —
    /// same non-null-current-for-a-change-event rule, same reasoning.
    /// </summary>
    public static PersonSummary? Apply(PersonSummary? current, PersonEvent @event) =>
        @event switch
        {
            PersonRegistered e => new PersonSummary(e.Id, e.Name, e.Contact.Email, e.Contact.Phone, e.Contact.HomeCity, e.Club?.ClubName),
            PersonRenamed e => Require(current, e) with { Name = e.Name },
            ContactDetailsChanged e => Require(current, e) with { Email = e.Contact.Email, Phone = e.Contact.Phone, HomeCity = e.Contact.HomeCity },
            ClubAffiliationChanged e => Require(current, e) with { ClubName = e.Club?.ClubName },
            _ => throw new ArgumentException($"Unknown PersonEvent subtype: {@event.GetType().Name}"),
        };

    private static PersonSummary Require(PersonSummary? current, PersonEvent @event) =>
        current ?? throw new ArgumentException($"{@event.GetType().Name} projected with no current summary — a change event can never be first in the stream.");
}
