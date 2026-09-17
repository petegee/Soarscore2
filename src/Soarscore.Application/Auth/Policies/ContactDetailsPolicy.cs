// The command-specific policy for ChangePersonContactDetails —
// secure-automatic-identity-linking.md (security review 2026-09-17). The S
// kind's self-or-organiser shape plus one guard: the contact email is D5's
// email-match key and D3's bootstrap key, so a non-organiser may keep the
// stored address unchanged or set it to the address the IdP verified on
// their token — never an attacker-chosen third address. Without the guard a
// linked person could point their contact email at the bootstrap-listed
// organiser's address and capture that organiser's first verified sign-in
// (its IdentityLinked and RoleGranted both) onto their own person. The
// organiser half stays unrestricted — "organisers can do everything" (trust
// model), and pre-registration/repair is exactly their job.
//
// The two allowed shapes, evaluated ordinal-ignore-case (emails differ in
// case across providers, the D3 bootstrap precedent):
//   • unchanged — the new email equals the read model's stored address: an
//     edit of the other contact fields, with the match key untouched;
//   • verified — the new email equals the token's email AND the IdP has
//     vouched for it (EmailVerified): the real "I changed my address at the
//     IdP" case.
// An unknown stored address (read-model row missing) falls to the strict
// verified rule alone — an "unchanged" half that cannot be checked proves
// nothing. Fail-closed elsewhere: a missing read-model port, or a command of
// another type reaching this policy, is a wiring bug and denies with
// auth.policyMissing, the pipeline's table-miss stance.

using Soarscore.Application.Commands.People;
using Soarscore.Application.Queries.People;
using Soarscore.Domain.People;

namespace Soarscore.Application.Auth.Policies;

public sealed class ContactDetailsPolicy : ICommandPolicy
{
    public async Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct)
    {
        if (!user.IsAuthenticated)
        {
            return AuthzOutcome.Deny("auth.notAuthenticated", "An authenticated principal is required.");
        }

        if (user.HasRole(PersonRole.Organiser))
        {
            return AuthzOutcome.Allow();
        }

        if (command is not ChangePersonContactDetails change)
        {
            return AuthzOutcome.Deny(
                "auth.policyMissing",
                $"{command.GetType().Name} is mapped to the contact-details policy but is not a ChangePersonContactDetails; the pipeline fails closed.");
        }

        // The self-half: the principal's D2-resolved PersonId equals the
        // command's target (SelfOrOrganiserPolicy's exact semantics).
        if (user.PersonId is not { } personId || personId != change.Id)
        {
            return AuthzOutcome.Deny("auth.forbidden", "Only the person themself or an organiser may do this.");
        }

        if (services.GetService(typeof(IPeopleQuery)) is not IPeopleQuery peopleQuery)
        {
            return AuthzOutcome.Deny(
                "auth.policyMissing",
                "The contact-details policy could not read the person's stored address — no IPeopleQuery is registered; the pipeline fails closed.");
        }

        var summaries = await peopleQuery.FindByIdsAsync([personId], ct);
        var storedEmail = summaries.Count > 0 ? summaries[0].Email : null;

        // A client that omits "contact" binds null straight through (the
        // ValidateContact reasoning in reverse) — a null email is neither an
        // unchanged address nor a verified one, so it falls to the denial.
        var newEmail = change.Contact?.Email;
        var unchanged = !string.IsNullOrWhiteSpace(newEmail)
            && string.Equals(newEmail, storedEmail, StringComparison.OrdinalIgnoreCase);
        var verified = user.EmailVerified
            && !string.IsNullOrWhiteSpace(newEmail)
            && string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase);

        return unchanged || verified
            ? AuthzOutcome.Allow()
            : AuthzOutcome.Deny(
                "auth.contact.emailOwnership",
                "A person may keep their contact email unchanged or set it to the address their identity provider has verified — ask an organiser for any other change.");
    }
}
