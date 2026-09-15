// authentication-and-authorisation.md WI-5 — the capture path's policy (the
// §Per-command policy table's C kind): OpenEntry and the three entry-scoped
// capture commands. The evaluation is D10's six steps, IN ORDER (first match
// wins) — the order is load-bearing: the organiser pass precedes every
// read-model read, so an organiser never pays for one.
//
// NFR-4 is why this policy is shaped the way it is: it reads ONLY competition
// configuration (the policy itself) and the principal — never task-round
// state, never what has been captured — so a capture's authorisation is
// invariant under the order captures arrive. WI-10 pins that as a property.
//
// Two deliberate non-denials:
//   • entry not found ⇒ ALLOW THROUGH. The handler's stream load produces the
//     authoritative *.notFound; the policy must never invent a 404 for an
//     EntryRef it resolved through the index (IEntryQuery.cs's single-key
//     note: when entryRef is supplied, competitionRef is not applied — the
//     pipeline, which does not yet know the entry's competition, passes
//     default and reads CompetitionRef off the returned row).
//   • competition with no configured policy ⇒ evaluates as OrganisersOnly
//     (D10: the safe default, today's single-operator behaviour). A missing
//     competition row reads the same way and denies; the handler's
//     competition.notFound is still reachable for the Organiser half.
//
// Fail-closed (same stance as the pipeline's table miss): a capture command
// implementing neither scope marker, or a missing read-model port, is a
// wiring bug and denies with auth.policyMissing rather than guessing.

using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;

namespace Soarscore.Application.Auth.Policies;

public sealed class CapturePolicyPolicy : ICommandPolicy
{
    public async Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct)
    {
        // D10 step 1 — not authenticated (401).
        if (!user.IsAuthenticated)
        {
            return AuthzOutcome.Deny("auth.notAuthenticated", "An authenticated principal is required to enter scores.");
        }

        // D10 step 2 — the organiser can always enter scores ("organisers can
        // do everything", trust model), whatever the competition's policy says.
        if (user.HasRole(PersonRole.Organiser))
        {
            return AuthzOutcome.Allow();
        }

        // Resolve the competition the capture addresses: competition-scoped
        // commands carry it; entry-scoped commands resolve it through the
        // entry index.
        CompetitionId? competitionRef;
        if (command is ICompetitionScopedCommand competitionScoped)
        {
            competitionRef = competitionScoped.CompetitionRef;
        }
        else if (command is IEntryScopedCommand entryScoped)
        {
            if (services.GetService(typeof(IEntryQuery)) is not IEntryQuery entryQuery)
            {
                return AuthzOutcome.Deny(
                    "auth.policyMissing",
                    "The capture policy could not read the entry index — no IEntryQuery is registered; the pipeline fails closed.");
            }

            // The middle filters are mandatory parameters (no defaults —
            // "every filter, all optional" is about meaning, not C# defaults),
            // so the single-key lookup passes them null positionally and
            // names only entryRef.
            var matches = await entryQuery.FindAsync(
                competitionRef: default, phaseOrdinal: null, roundOrdinal: null, taskRoundOrdinal: null,
                groupRef: null, competitorRef: null, cancellationToken: ct, entryRef: entryScoped.EntryRef);

            // Entry not found ⇒ allow through: the handler's stream load is the
            // authoritative *.notFound, never a policy-invented 404.
            if (matches.Count == 0)
            {
                return AuthzOutcome.Allow();
            }

            competitionRef = matches[0].CompetitionRef;
        }
        else
        {
            return AuthzOutcome.Deny(
                "auth.policyMissing",
                $"{command.GetType().Name} is mapped to the capture policy but carries neither a competition scope nor an entry scope; the pipeline fails closed.");
        }

        if (services.GetService(typeof(ICompetitionsQuery)) is not ICompetitionsQuery competitionsQuery)
        {
            return AuthzOutcome.Deny(
                "auth.policyMissing",
                "The capture policy could not read the competition's policy — no ICompetitionsQuery is registered; the pipeline fails closed.");
        }

        var policy = await competitionsQuery.FindCapturePolicyAsync(competitionRef.Value, ct);

        // D10 step 3 — OrganisersOnly denies (and so does "no configured
        // policy": the safe default, today's single-operator behaviour).
        if (policy is null || policy.Mode == CapturePolicyMode.OrganisersOnly)
        {
            return AuthzOutcome.Deny("auth.capturePolicy.denied", "This competition's capture policy reserves score entry for organisers.");
        }

        // D10 step 4 — an unlinked identity is nobody: authenticated at the
        // IdP but linked to no person, so no mode beyond OrganisersOnly can
        // speak for it.
        if (user.PersonId is not { } personId)
        {
            return AuthzOutcome.Deny(
                "auth.capturePolicy.denied",
                "Score entry for this competition is limited to known people — this signed-in identity is not linked to a person yet.");
        }

        // D10 step 5 — any registered person.
        if (policy.Mode == CapturePolicyMode.AnyRegisteredPerson)
        {
            return AuthzOutcome.Allow();
        }

        // D10 step 6 — the allow-list: allow iff the acting PersonId is on it.
        return policy.Capturers.Contains(personId)
            ? AuthzOutcome.Allow()
            : AuthzOutcome.Deny("auth.capturePolicy.denied", "This competition's capture policy does not allow-list this person to enter scores.");
    }
}
