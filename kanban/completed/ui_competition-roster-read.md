# Story — UI prerequisite: competition roster read for non-organisers

**Status:** Backlog · **Raised:** 2026-10-02 · **Source:** `SoarscoreUI/kanban/blocked/ss_engine-prerequisites.md` item 1 + `SoarscoreUI/SOARSCORE2-HANDOVER.md` §1 · **Engine observed at:** `843ef25`

## What

One competition-scoped read, open to any authenticated caller, returning every competitor's display name alongside competitor number and withdrawn flag — with no contact details or identity links.

Verified facts (re-verify before acting):

- `/competition`, `/competition-result`, `/draw-matrix` carry IDs only: `Competitor{Id, PersonRef, CompetitorNumber, …}` (`src/Soarscore.Domain/Competitions/Competition.cs:332-344`), `CompetitorFinalScoreView(CompetitorRef, …)` (`src/Soarscore.Application/Queries/Scoring/ScoreCompetition.cs:18-23`), `DrawMatrixPilotView(CompetitorRef, CompetitorNumber, Withdrawn)` (`src/Soarscore.Application/Queries/Competitions/GetDrawMatrix.cs:30-49`).
- `/people` = `OrganiserPolicy`, `/person` = `SelfOrOrganiserPolicy` (`src/Soarscore.Application/Auth/CommandPolicyTable.cs:99-105`, narrowed 2026-09-16 per `kanban/deferred-decisions.md:552-566`). `/competition`, `/competition-result`, `/draw-matrix`, `/competition-event-log`, `/competition-teams` stay `Authenticated` (`:90-98,106-111`).
- Names already leak incidentally: `GetCompetitionEventLogHandler` folds competitors then `peopleQuery.FindByIdsAsync` (`src/Soarscore.Application/Queries/Competitions/GetCompetitionEventLog.cs:97-104`) and labels `"Name (#n)"` (`:168-170`, `EventLogSummariser.cs:82-117`).
- Read-model pieces exist, no new projection needed (LADR-0001 inventory `docs/ladr/ladr-0001-event-store.md:54-73`): fold competition stream (existing `CompetitionLoader` pattern) → batch `IPeopleQuery.FindByIdsAsync` (`src/Soarscore.Application/Queries/People/IPeopleQuery.cs:20-52`) → project `PersonSummary.Name` (+ optional `ClubName`). Never `Email/Phone/HomeCity/Roles/Identities/MembershipNumber`.
- `GetDrawMatrix.cs:9-11` client-join note ("client joins via existing people reads") is stale post-narrowing.

## Why it matters

UI v1 scorers are competitors on their own phones (admitted by capture policy, not organisers) and every signed-in person reads results. Today both see numbers only. Blocks UI `score-capture.md` (non-organiser scorers) and `results-and-standings.md`. Organiser-only screens are not blocked.

## Before starting

- Decide: new roster read (recommended, cacheable, ordered by `CompetitorNumber`) vs name on existing views. Recommended view: `CompetitionRosterEntry(CompetitorRef, CompetitorNumber, Name[, ClubName], Withdrawn)`, `GET` via `MapQuery` (route-shape test `tests/Soarscore.Architecture.Tests/RouteShapeTests.cs:20-40` allows GET/POST only).
- Decide club in/out (handover leaves it to us; recommend out — PII, UI did not require it) and confirm event-log labels are intended exposure matching this read.
- Read D4 narrowing (`kanban/deferred-decisions.md:552-566`), `kanban/completed/authentication-and-authorisation.md` §Per-command policy + WI-10 totality property (new query needs a table row), and `kanban/backlog/public-read-surface.md` PII warning.
- House rule 2 cross-check: `docs/users.md`, trust model (CLAUDE.md); no new domain concept expected, but surface one if the design invents it. No `/docs` change without owner approval.

## Acceptance

- [ ] A Competitor-role caller who is not an organiser gets every competitor's display name for a competition in one read, with no contact details exposed.
