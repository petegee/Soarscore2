# Story — F5K 5.5.10.13 ii: launch altitude not recorded ⇒ re-flight entitlement

**Status:** Backlog (raised 2026-09-22, corpus sweep of `kanban/in-progress/add-recorded-predicate.md` WI-4 item 4)

## What

FAI F5K 5.5.10.13 ii (`docs/rules/source-docs/f5-electric-2026.md:1390-1391`):
one of the re-flight grounds is that "the launch altitude was not recorded in
the AMRT and / or the associated external AMRT software could not determine the
launch altitude".

Same recordedness subject as F5J's 5.5.11.7 e, but a **different consequence**:
a re-flight entitlement, not a zeroed flight. The `IsRecorded` predicate as
landed expresses only the gate-zero consequence, and re-flights are
enteritlement-driven (reflight groups, `kanban/completed/reflight-groups.md`),
not flight-validity-driven.

Related observation from the same sweep: F5K 5.5.10.5 b
(`f5-electric-2026.md:1252`) resets the display to "---" on motor restart and
states **no consequence** — whether the '---' state feeds 5.5.10.13 ii's "could
not determine" is a rule question for the fai-rules skill to resolve first.

## Why it matters

SeedF5K is in the corpus, so its rulebook's recordedness semantics matter to
the model's test. If a no-record altitude can happen, today's encoding would
pend the flight (ordinary awaited metric), which matches neither a re-flight
nor a zero.

## Before starting

- Resolve the 5.5.10.5 b ↔ 5.5.10.13 ii link with the rule text (skill
  discipline: a rule you cannot find is a question, not a guess).
- Decide the shape: does this need a new vocabulary member (recordedness →
  re-flight) or is it an operational exception captured outside the score
  pipeline? NFR-2 additive-only applies.
- NZ F5K (NDC Class Q §3.16, NZ.0.4) does NOT carry this ground —
  `docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md:2211-2212` lists only
  organisers'-fault — so the two F5K rulebooks would diverge if adopted.
