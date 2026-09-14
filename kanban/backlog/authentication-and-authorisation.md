# Authentication & authorisation

**Status:** backlog

## What

Add authentication and authorisation to the Soarscore core API so it can be
hosted in the cloud, with competitors and organisers registering via social
login. Decisions recorded with the owner (2026-09-14):

- **Identity provider:** Auth0 — social logins, with email magic-link as a
  fallback for members without social accounts.
- **API auth:** JWT bearer validation in `src/Soarscore.Api`; identity exposed
  to the Application layer via a current-user port; enforcement in the
  `Dispatcher` pipeline as per-command policies, not scattered in handlers.
- **Roles for v1:** Competitor + Organiser only — Contest Director folds into
  Organiser. Who *enters* scores is deliberately independent of the role that
  person played at the contest (organiser retro-entering, a scorer recording
  for another pilot, or a pilot self-scoring are all legitimate depending on
  the competition).
- **Capture policy is competition configuration:** who may enter which scores
  is per-competition data (policy as data); the core never branches on who is
  acting, it interprets the policy.
- **Role assignment:** invite flow — organisers invite/promote. Bootstrap of
  the *first* organiser is an open question (config-seeded one-off?).
- **Front-end:** a web front-end is planned as a separate consuming system
  (NFR-3); auth must suit an SPA redirect/BFF flow from day one.
- **Hosting:** Fly.io (Sydney) + managed PostgreSQL for Marten; SQLite stays
  dev/test.

## Why it matters

Hosting in the cloud and opening the system to competitors and organisers
requires knowing who is acting. The documented trust model assumed a closed
club tool with no auth (CLAUDE.md, amended owner-approved 2026-09-14).

## Before starting

- **Glossary approval** for the new concepts this introduces: *Identity link*
  (provider + subject → `Person`), *Role*, *Capture policy* (per-competition
  rule for who may enter which scores). Do not add without owner approval.
- **users.md drift (flagged, unresolved):** per-competition capture policy
  makes Soarscore *police* who may enter scores, where users.md currently says
  Soarscore "neither enables nor forbids" competitor entry and that pilots do
  not self-score. Owner chose to amend the trust model only; the users.md
  amendment is still owed during this story.
- Settle the first-organiser bootstrap mechanism.
- Write the LADR for the auth approach (provider + enforcement point).
- Cross-check: NFR-3 (headless core — auth is transport + a port), NFR-4 (auth
  must not impose ordering on score capture).
