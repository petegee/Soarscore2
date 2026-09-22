# Story — Public read surface (per-query opt-outs from Authenticated)

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D4)

## What

Allow named queries to opt out of the default `Authenticated` policy so
anonymous callers can read them — the motivating case is a public leaderboard
a clubhouse screen or results page can render without every viewer signing
in. D4 deliberately shipped the secure default (every query `Authenticated`
under `oidc`/`mock`) and deferred "which reads are public" as its own
decision rather than a default.

## Why it matters

Signage and public results are how club events actually display scores; as
shipped, a display device needs a principal, which is operationally awkward
and makes "public" indistinguishable from "authenticated". The opt-out also
completes the policy model: the pipeline table already routes every query;
this story adds the *anonymous-allowed* policy kind to the vocabulary — one
new policy, a table edit per opted-in query, and the totality property
(WI-10's anonymous-arm) needs re-checking with the new policy in the mix.

## Before starting

- Read D4 and the §Per-command policy table of
  `kanban/completed/authentication-and-authorisation.md`; the enforcement
  totality property in the auth story's WI-10 pins "anonymous ⇒ deny for
  every message type" — this story changes that invariant's shape and must
  restate and re-prove it (per-query, not per-table).
- Decide *which* queries may be public and whether the choice is code
  (table) or configuration — "which reads are public" deserves its own
  decision, not a default (D4's own words).
- Cross-check the trust model (CLAUDE.md): making reads public widens what
  an unauthenticated principal learns (personal names, club affiliations);
  surface any PII concern to the owner before shipping an opt-out.
- No `/docs` changes expected; no rule corpus engagement (scoring rules do
  not govern display).
