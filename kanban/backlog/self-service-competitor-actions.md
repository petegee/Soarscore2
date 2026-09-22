# Story — Self-service competitor actions (self-entry, self-withdrawal)

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D7)

## What

Let a signed-in Competitor enter themselves into a competition and withdraw
themselves, without an organiser typing on their behalf. In v1 both stay
organiser-side: `RegisterCompetitor` and `WithdrawCompetitor` carry the
`Organiser` policy. This story re-scopes those two commands to
"self (own person) or organiser" — the `SelfOrOrganiserPolicy` pattern
already in the pipeline — or a competitor-appropriate policy of their own,
subject to the competition's entry state (entries open/closed, drawn or not).

## Why it matters

Every registration today is an organiser data-entry task, and at a club event
the organiser is also running the draw and the field. Self-service moves the
typing to the pilot and keeps the organiser's authority where it matters
(rulings, annulments). D7 deliberately left these organiser-side in v1 so the
auth story landed one policy model at a time; the vocabulary (roles, capture
policy, pipeline) to widen it now exists.

## Before starting

- Read D7 and the §Per-command policy table of
  `kanban/completed/authentication-and-authorisation.md`.
- Decide the interaction with entry state and NFR-4 first: self-service must
  add no new gate on capture and must not let a pilot join a competition
  whose draw has already run (or must it? — that is a rulebook question to
  settle with the `fai-rules` skill before any code).
- `WithdrawCompetitor` semantics need a check against withdrawal's existing
  meaning ("won't fly", `kanban/deferred-decisions.md` §Task-round lifecycle)
  — self-withdrawal must not create a second way to record a third state.
- House rule 2 cross-check: `docs/users.md` (who does what) and the trust
  model in CLAUDE.md — competitor powers are per-competition configuration;
  confirm entry/withdrawal should stay role-scoped rather than becoming
  competition configuration like the capture policy.
