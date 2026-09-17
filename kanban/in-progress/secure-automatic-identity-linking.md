# Story — Secure automatic identity linking (email-ownership guard)

**Status:** In progress · **Raised:** 2026-09-17 (owner-approved fix from the
security review of PR #1 against
`kanban/completed/authentication-and-authorisation.md`)

## What

Two linked changes to the identity-linking surface, both closing the one
confirmed finding of the review — **a self-editable contact email can capture
another person's sign-in and any bootstrap privilege attached to it**:

1. **LinkSignIn's email-match arm (D5 arm 2) links only a person with no
   identity links yet.** An email match against a person that already holds a
   linked sign-in refuses with `auth.signIn.explicitLinkRequired` (409) — no
   link, no bootstrap grant, no PersonId in the response. Legitimate
   cross-provider linking becomes an organiser act (`/bind-identity`).
2. **Self-service contact-email changes are ownership-bounded.** A new
   `ContactDetailsPolicy` (the S kind plus one guard) allows a non-organiser to
   keep the stored address unchanged or set it to the IdP-verified token email;
   anything else denies with `auth.contact.emailOwnership` (403). Organisers
   keep unrestricted authority.

## Why it matters

`ChangePersonContactDetails` was plain self-or-organiser: any linked person
could set their contact email to any address, and `LinkSignIn`'s email-match
arm trusted that stored address as the identity-match key. On a fresh
deployment an attacker could register, edit their contact email to the
bootstrap-listed organiser's address, and the organiser's first verified
sign-in would link onto the attacker's person — granting the attacker's
original identity Organiser on its next request. The `email_verified` gate
(shipped in the `fixes` commit) vouches for the incoming claim, not the
attacker-editable stored match, so it does not close this.

## Before starting

- Owner approval secured 2026-09-17 (behaviour change narrows D5's
  email-second arm and the S kind's contact-details authority); the amendment
  is recorded in `kanban/deferred-decisions.md` under Authentication and
  authorisation.
- The `IdentityLinked`-only vocabulary stands: no unlink event is added here —
  organiser `/bind-identity` is the explicit path, and
  `unlink-identity-and-account-recovery.md` remains the lifecycle story.
- Tests: the full attack sequence over HTTP on both stores; guard refusals
  append nothing; organiser pre-registration + first sign-in still links;
  contact-edit truth table; concurrent first-link attempts resolved by the
  store's expected-version arbitration.
