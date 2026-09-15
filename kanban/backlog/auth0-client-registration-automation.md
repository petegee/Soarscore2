# Story — Auth0 client registration automation (Management API)

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D12)

## What

Register M2M clients on the Auth0 tenant via the Auth0 Management API,
instead of dashboard clicks per integrator. Today's recipe (integrators
guide §"Integrator recipe (M2M)") is: register the client on the tenant →
client-credentials token → organiser binds via `/bind-identity` →
allow-list via `/configure-capture-policy`. The first step is manual; D12
explicitly stubbed automating it.

## Why it matters

Every new integration currently costs a dashboard session by someone with
tenant-admin rights — a manual step that does not scale with the number of
integrators and cannot be delegated safely. Automation also makes the
registration repeatable for CI-managed client lifecycles (rotate secrets,
retire stale clients) rather than tribal knowledge.

## Before starting

- Read D12 of `kanban/completed/authentication-and-authorisation.md`.
- Decide who is allowed to trigger registrations and through what surface —
  organiser-initiated from Soarscore (a new organiser command + a
  Management-API adapter in Infrastructure) vs a standalone tool/CLI. Note
  NFR-3: a Management-API client is an Infrastructure adapter concern, never
  Api-layer logic; and LADR-0003 discipline for the new package.
- Decide the secret story: where the created client's secret goes (an email?
  printed once? stored nowhere?) — never into the event log.
- Check the tenant's Management API entitlements (machine-to-machine
  application creation may be tier-gated) before promising the feature.
- House rule 2 cross-check: LADR-0004 (Auth0 stays outside the core; this
  widens tenant administration, not token validation); no rulebook
  engagement.
