# Story — NZ F3K NDC seed class

**Status:** Backlog · **Raised:** 2026-08-29 (three-way cross-reference of
seed `10-f3k.json` vs FAI F3K.11 vs NZMAA S5 during the seed-vs-corpus
conformance analysis)

## What

Author the seed-data Competition Class for the NZMAA **F3K National
Decentralised Contest (NDC)** format — `NZ.0.2` / `NZ.0.2.1` in
`docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md`:

- **Task catalogue: B, D, G and H only** (`NZ.0.2.1 a`), with the task
  arithmetic quoted verbatim from FAI 5.7.11.2 / 5.7.11.4 / 5.7.11.7 /
  5.7.11.8 — identical numbers to `SeedF3K.cs`'s tasks.
- **Scoring frame: sum of raw scores** (`NZ.0.2.1 a` "Total is sum of raw
  scores") — no per-group normalisation, therefore no drop-worst either
  (rulebook silence; see Before starting).
- **Timing to 0.1 s, truncated** (`NZ.0.2.1 a`: "59.99 seconds is recorded
  at 59.9 seconds") — already the seed F3K metric precision.

It becomes a new `SeedNzF3kNdc.cs` (canonical JSON `85b-…` slot by existing
numbering conventions) and a seed-corpus test presence like its siblings.

## Why it matters

S5 §3.8.1 retired NZ's own HLG class and nominated FAI §5.7 as *the* F3K
rulebook; §0.2 is its only NZ variation, and it changes the scoring frame,
not the tasks. The seed corpus is the model's test — a real NZ format it
cannot express would be a gap in the model. This one is expected to express
as pure data: the no-normalisation raw-sum frame already exists
(`SeedNzMNdc.cs` for Class M, citing `NZ.3.12.7 c`; Classes N/P per
`NZ.3.13.1 i` / `NZ.3.15.1 i`), and the four task bodies already exist in
`SeedF3K.cs`.

## Before starting

- **Precedent to copy:** `SeedNzMNdc.cs` is the same NDC frame (raw sum, no
  normalise, landing bonus moved into the raw score) for a different task
  family. Its structure, comments and citation style are the template.
- **Task bodies:** transplant B, D, G, H from `SeedF3K.cs`; verify the
  parameter bindings (working-time 420/600 variants, `maxFlight.B`) carry
  over — `NZ.0.2.1` restates both the 10- and 7-minute variants for B.
- **Rulebook silence is a question:** `NZ.0.2` states no round count and no
  drop rule. Do not borrow FAI F3K's drop-worst or an assumed
  four-rounds-one-per-task shape — surface for a ruling (NDC Class M is
  four rounds by explicit text, not analogy).
- **No-new-concepts gate:** NDC already exists in the seed, so the glossary
  should be untouched; stop and surface if anything new seems needed.
- **Cross-reference (house rule 2):** `docs/users.md` and
  `docs/non-functional-requirements.md` for anything this contradicts;
  also `kanban/backlog/tie-break-policy-in-class-definition.md` — NDC's
  raw-sum frame has no normalisation scale, which affects any tie-break
  rung that assumes one.
- **Consumer:** `kanban/backlog/fai-conformant-f3k-fixture-hunt.md` — an
  NDC-run fixture would replay against *this* class, not `10-f3k.json`.
  Either story can land first; neither blocks the other.
