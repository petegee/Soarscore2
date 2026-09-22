# Story — Unlink identity and account recovery

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, D12's forward note
and WI-11's stub list)

## What

The two identity-lifecycle actions v1 does not have: **unlink** an identity
link from a Person (a social account lost or abandoned, a stale
client-credentials binding, a person who linked the wrong account) and
**account recovery** — re-establishing a person's access when their
sign-in path breaks (email no longer accessible, provider account deleted).
The event vocabulary has `IdentityLinked` only; there is no
`IdentityUnlinked`, and no recovery flow beyond an organiser's manual
repair.

## Why it matters

D5's get-or-create matches on identity first, email second — so a person who
loses their provider account and re-registers with the same email lands on
their old Person (good), but a person who *changes* email and loses the
provider account can strand their history under an unreachable identity.
Organisers can bind identities (`BindIdentity`, WI-7) but cannot unbind —
the fix-up path for a wrong link is absent, and it is organisers who will be
asked to fix it.

## Before starting

- Read D5 (identity-first/email-second resolution) and D12 (`BindIdentity`
  as the pre-provisioning path that "later serves account recovery") of
  `kanban/completed/authentication-and-authorisation.md`.
- Design the unlink decide/invariant first: an identity link's uniqueness is
  arbitrated by the unique index on `(provider, subject)` — unlinking must
  free the pair cleanly and the fold must tolerate replay (the link-append's
  duplicate-tolerance precedent).
- Decide who may unlink (organiser-only? self?) and what a recovery actually
  is in this model: most likely *organiser binds a new identity to the
  existing Person* — i.e. possibly no new command at all, just policy and
  procedure. Do not invent a "recovery" concept the model already expresses.
- House rule 2 cross-check: no glossary change expected (identity link
  already approved); confirm the audit story (event log) stays the record of
  who unlinked what and when — which leans on the actor-attribution stub.
