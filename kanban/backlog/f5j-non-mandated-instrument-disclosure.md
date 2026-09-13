# Story stub - F5J non-mandated instrument disclosure

**Status:** Backlog
**Raised:** 2026-09-09 — WI-0 owner question Q1 of
`kanban/completed/tape-points-landing-seeds.md` (the landing tape as a
declared reading scale), decided log-only; reporting surface deferred here.

## What

FAI rule `5.5.11.12 i` (`docs/rules/source-docs/f5-electric-2026.md:991-993`)
mandates a points-marked tape for F5J landings, so a tape-measure distance
captured for F5J is score-equivalent (composition fidelity, proven in WI-5)
but a deviation from the rulebook instrument. Each measurement already carries
its instrument in the event log, so the audit trail is complete with no new
code. This story exists to decide and build any *visible* disclosure — e.g. a
deviation flag on score/completeness reporting — if and when the club wants it.

## Why it matters

A CD reviewing results currently sees clean scores with no hint that F5J ran
on a non-mandated instrument; reconstructing that requires digging the event
log after the fact. Whether that latency is acceptable for a trusted club
tool is a product call, not an architectural one — hence a stub, not scope in
the tape story.

## Decided (2026-09-09, interim)

- **Current treatment is log-only (option i).** Recorded honestly in the event
  log via the instrument declaration + per-measurement instrument; never
  refused (refusing a real landing would breach the Scorer role in
  `docs/users.md` and NFR-4).
- **Reporting flag (option ii) deferred here**, until real reporting exists to
  hang it on and the club asks for it.
- **Targeted declaration warning (option iii) is architecturally unavailable:**
  warning specifically for F5J would put class-specific instrument knowledge
  in the core, which the tape story forbids. The only clean design is
  *generic* instrument-coverage disclosure (the declaration summary shows per
  metric which instruments cover it, letting a human spot the gap) — a weaker
  form of (ii), to be designed here, not in the core.

## Before starting

- Read the Q1 analysis (tape story, "What the rulebooks say about the
  instrument" + owner decision record) and `docs/users.md` Scorer role.
- Constraint: no class-specific instrument rule in `src/` under any option
  (architectural law — Competition Class model vs. core system).
- Existing facts to build on: `InstrumentsDeclared` /
  `InstrumentDeclarationCorrected` events and `Measurement.Instrument`
  (`src/Soarscore.Domain/Competitions/Competition.cs`,
  `src/Soarscore.Domain/Entries/Entry.cs`); recording-completeness queries.
