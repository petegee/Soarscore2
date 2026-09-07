# Story — Tape-points landing seeds (derived-equivalent class definitions)

**Status:** Backlog · **Raised:** 2026-09-07 — owner insight during
`kanban/completed/corpus-parallel-run-mapping-table.md` review: the club's
landing tape is graduated in **points**, one side per class (F3J vs F3B differ
slightly on the first band, opposite ends of the same tape); the scorer writes
down the tape number and Gliderscore converts it. The jerilderie-2010 G5
refusal was re-reasoned on that basis and now cites this stub.

## What

Seed JSON variants that declare a landing metric in tape **points** plus a
`$kind:"lookup"` term over it — so fixtures whose landing evidence is
entered-points become runnable pairs with **no core change**. The model
already expresses this: the scoring vocabulary is discipline-blind
(`src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs` — a
landing table is a LookupTerm over *a metric*), so a points-kind metric
consumed by a lookup is pure additive data (NFR-2). Candidate first pair:
`jerilderie-2010` ↔ a derived-equivalent F3J tape-points seed (mirroring
`50-f3j.json`'s ladder in points form); the F5J rows' scheme-11 candidate
(55–100→5–50 vs the F5J rulebook ladder) is the same question one class over.

## Why it matters

The mapping table currently refuses jerilderie-2010 because no catalogue seed
declares a points-kind landing metric — a seed-declaration mismatch, not
inexpressibility. Authoring the variant turns a refused fixture into a
witnessing pair and, if the tape's banding matches F3J.10.5, potentially a
near-twin. It also settles which side of the general question the system is
on: the tape is class-agnostic hardware and the class definition owns the
band semantics (F3J side vs F3B side = two class definitions over one tape) —
the core consumes both identically, which is exactly the NFR-1 promise made
measurable.

## Before starting

- Verify the seed model accepts a points-unit metric consumed by a lookup
  term with no new vocabulary (glossary unchanged; if anything looks like a
  new concept, stop and surface).
- Obtain the club tape's actual banding (owner has the tape) and compare
  against the F3J.10.5 ladder — this decides near-twin vs witness before any
  run; the F3J/F3B first-band difference is the known drift precedent.
- Cross-check `docs/users.md`, `docs/non-functional-requirements.md`,
  `kanban/deferred-decisions.md`, and `metric-absence-semantics.md` for
  anything already settling landing-evidence kinds (house rule 2).
- Follow the mapping table's update contract (D6) when the pair runs: flip
  the jerilderie row, fold the outcome into its `why`.
