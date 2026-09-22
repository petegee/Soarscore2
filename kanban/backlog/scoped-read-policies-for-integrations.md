# Story — Scoped read policies for integrations (per-client read scoping)

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D12)

## What

Per-client *read* scoping for machine actors. A registered M2M client today
(the results-display case) holds nothing beyond `Authenticated` reads — which
means it sees everything any authenticated principal sees: every person, every
identity-bearing summary, every competition. This story introduces a way to
narrow what a given client can read (per-client or per-person allow-lists over
the query surface) without reintroducing scopes-in-tokens, which D12
explicitly rejected.

## Why it matters

A scoring-rig client is properly narrowed by the capture policy (AllowList on
its competition), but a *read* integrator is all-or-nothing today. The
authority vocabulary has no read-side analogue of the capture policy; as
third-party integrations grow (the integrators guide advertises M2M clients),
"any bound machine can read the whole people read model" is a standing
over-grant that only gets harder to retract later.

## Before starting

- Read D12 of `kanban/completed/authentication-and-authorisation.md` —
  especially the *rejected* scope-in-token stance. Any design here must keep
  authority in Soarscore's own data (roles, competition configuration, event
  log), never in token claims; a read-scoping mechanism is likely
  Soarscore-side configuration about the bound Person, not Auth0
  permissions.
- Decide the unit of scoping: per-client (the identity row), per-Person (the
  machine is a person — D12), or per-query-class. Do not design until the
  first real consumer names what it must NOT see.
- Check the enforcement point: the pipeline's `AuthenticatedPolicy` is the
  query gate today; a scoped-read policy is a new policy kind in the same
  table, and the WI-10 totality property must be restated over it.
- House rule 2 cross-check: NFR-3 (no new read models for policy state —
  where does the scope live? the people read model is the likely home),
  LADR-0001 (ports gain methods, not new abstractions).
