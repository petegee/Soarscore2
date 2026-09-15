# Story — Invite email delivery (organiser invite → email)

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D7)

## What

When an organiser invites someone — pre-provisioning a Person
(`RegisterPerson` or the `BindIdentity` pre-provisioning path) or granting a
role — deliver the invitation as an email to that person, so "the organiser
promoted you" stops being a hallway conversation. v1 has no email anywhere;
the invite flow is a human handshake.

## Why it matters

Identity links are made at first sign-in (D5: email is the match key), so a
person who never learns they have an account never signs in — the invite is
the activation step for the whole model. Email is also the only channel that
reaches members without social accounts (the magic-link fallback in the
owner's 2026-09-14 decision) — without delivery, that fallback exists in
principle only.

## Before starting

- Read D7 of `kanban/completed/authentication-and-authorisation.md` and the
  owner's Auth0 decision (D-section header list): check whether Auth0's own
  invitation/magic-link flows can carry this before any Soarscore-side
  sending infrastructure is designed — NFR-3 (headless core) means the
  answer is likely "a port in Application, an adapter in Infrastructure",
  not SMTP in the Api.
- Decide the trigger point: which commands (or read-model facts) fire an
  email, and how retries/failures are recorded without touching the event
  log's immutability contract.
- New external dependency likely (a mail API client) — LADR-0003 licence/CPM
  discipline applies.
- House rule 2 cross-check: NFR-3 (no UI assumptions; a port, not a page),
  and confirm no rulebook engagement (rules govern the contest, not its
  invitations).
