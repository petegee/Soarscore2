// authentication-and-authorisation.md WI-3, fold invariants, stated here
// verbatim as:
//
//   **Fold tolerance and set semantics for Person roles and identity links.**
//   For any generated sequence of RoleGranted / RoleRevoked / IdentityLinked
//   events folded onto a registered Person through the generic Apply switch:
//   (a) Roles is exactly the abstract set model — each RoleGranted adds, each
//   RoleRevoked removes, and grant-of-a-held / revoke-of-an-absent role folds
//   to no change (a set, not a bag); (b) duplicating every event in the
//   sequence yields exactly the state folding the sequence once yields — a
//   replayed IdentityLinked appends no second identical link and duplicated
//   role events are idempotent. The duplication half is the fold tolerance
//   WI-3 asks for; the decide refusals (person.roleAlreadyHeld,
//   person.identityAlreadyLinked) are the decide layer's stricter guarantee —
//   the fold tolerates what the decide forbids.

using System.Collections.Immutable;
using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.People;
using Xunit;

namespace Soarscore.Domain.Tests;

public class PersonRoleAndIdentityPropertyTests
{
    private static readonly ContactDetails SampleContact = new()
    {
        Email = "pilot@example.com",
        Phone = "021 555 0100",
        HomeCity = "Auckland",
    };

    private static readonly ClubAffiliation SampleClub = new()
    {
        ClubName = "Auckland Soaring Club",
        MembershipNumber = "ASC-042",
    };

    private static readonly DateTimeOffset Now = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    // Small pools so generated sequences collide on (Provider, Subject) and
    // roles often — the duplication half of the invariant must actually bite.
    private static readonly string[] Providers = ["auth0", "google-oauth2", "mock"];

    private static readonly string[] Subjects = ["sub-1", "sub-2", "sub-3"];

    private static readonly Gen<PersonEvent> AuthEvent =
        from kind in Gen.Int[0, 2]
        from competitor in Gen.Bool
        from provider in Gen.Int[0, Providers.Length - 1]
        from subject in Gen.Int[0, Subjects.Length - 1]
        select (PersonEvent)(kind switch
        {
            0 => new RoleGranted(competitor ? PersonRole.Competitor : PersonRole.Organiser, Now),
            1 => new RoleRevoked(competitor ? PersonRole.Competitor : PersonRole.Organiser, Now),
            _ => new IdentityLinked(Providers[provider], Subjects[subject], Now),
        });

    private static Person Registered() =>
        Person.Create(new PersonRegistered(PersonId.New(), "Alex Pilot", SampleContact, SampleClub, Now));

    [Fact]
    public void Role_folds_agree_with_the_set_model_for_any_event_sequence()
    {
        AuthEvent.Array[0, 24].Sample(events =>
        {
            var model = ImmutableHashSet<PersonRole>.Empty;
            foreach (var @event in events)
            {
                switch (@event)
                {
                    case RoleGranted granted:
                        model = model.Add(granted.Role);
                        break;
                    case RoleRevoked revoked:
                        model = model.Remove(revoked.Role);
                        break;
                }
            }

            var final = events.Aggregate((Person?)Registered(), (person, @event) => Person.Apply(person, @event))!;

            final.Roles.Should().HaveCount(model.Count);
            final.Roles.SetEquals(model).Should().BeTrue();
        });
    }

    [Fact]
    public void Duplicating_every_event_leaves_the_folded_state_unchanged()
    {
        AuthEvent.Array[1, 24].Sample(events =>
        {
            var doubled = events.SelectMany(e => new[] { e, e }).ToArray();

            var once = events.Aggregate((Person?)Registered(), (person, @event) => Person.Apply(person, @event))!;
            var twice = doubled.Aggregate((Person?)Registered(), (person, @event) => Person.Apply(person, @event))!;

            once.Roles.SetEquals(twice.Roles).Should().BeTrue();
            once.Identities.SequenceEqual(twice.Identities).Should().BeTrue();
        });
    }
}
