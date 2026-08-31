# Story — Lane/spot assignment for drawn groups

**Status:** Backlog · **Raised:** 2026-08-31 (split out of the teams
direction discussion — see `teams-feature-options.md`. Independent of teams:
under Option 2 the teams story stops at roster data and explicitly excludes
physical lane/spot allocation, so if this is wanted it must stand alone.)

## What

Make the physical field position of a competitor within a drawn group
explicit data: per task-round group, map each sequence position to a
physical resource — lane, launch spot, landing spot, winch line — and
expose that mapping through the operational read views used at
score-capture time.

Deliberately **team-blind**. GliderScore derives lane allocation from its
single team number (`teams-feature-options.md` §"What GliderScore
implements"); Soarscore must not reproduce that coupling. Team-aware field
coordination (e.g. F3J's landing-spot rule referencing team members) is out
of scope here and would return to the teams thread if ever wanted.

## Why it matters

- The draw today ends at an ordered `Group.CompetitorRefs` array
  (`src/Soarscore.Domain/Competitions/PhaseDraw.cs`); nothing says which
  physical position each slot corresponds to, so every consuming system
  has to guess or hard-code one — exactly the overload the research paper
  warns against (design principle 10: "Sequence position is not
  automatically a physical lane, launch spot, landing spot, or winch
  line. Do not overload the existing ordered competitor array without
  deciding which fact is being represented.").
- Decision question 5 in the paper ("Does the first release need physical
  lanes/spots, or only group membership and sequence? These are different
  facts.") is answered *separately* from the teams option; this story is
  where the physical-fact half gets decided on its own merits.
- NFR-3: Soarscore exposes report-ready data — the mapping is exactly the
  kind of fact a consuming scoring/sheet application needs and cannot
  derive.

## Before starting

- **Decide which physical fact(s) are represented** — lane vs launch spot
  vs landing spot vs winch line are different facts; start from what the
  scorer/CD actually needs at capture time (`docs/users.md`) and model
  only that, not all of them speculatively.
- **Check the rules via the `fai-rules` skill** for anything each class
  requires of lanes/spots in the *absence* of teams. F3J's
  no-team-member-adjacent-spot rule is team-aware and is *not* a driver
  here; any genuine per-class variation that does emerge must be class
  data, never a named-class branch (NFR-1/NFR-2).
- **Glossary:** a lane/spot concept is new — owner approval required
  before `docs/soaring-domain-glossary.md`, the class diagram, or events
  land (house rules 3–4).
- **Lifecycle:** when are assignments made and can they change? Lane
  allocation is draw-derived configuration, so it follows the
  draw-affecting rule — frozen with, or invalidating, an accepted draw
  (paper design principle 7). Settle the event shape against the existing
  draw-acceptance/redraw lifecycle (`kanban/completed/draw-acceptance-redraw.md`).
- **Representation:** explicit assignment events/data — never implicitly
  defined as the array index (paper design principle 10). Decide whether
  assignment is per-competition default (e.g. slots map to sites 1..N)
  with per-round overrides, or always explicit per round; keep the model
  at club scale (≤ 20 pilots).
- **Priority and ordering:** no dependency on the teams story in either
  direction — teams neither needs nor provides this. Take it when a
  field-operation need is demonstrated rather than speculatively; if it
  lands first, the teams story's roster views must compose with it, not
  duplicate it.
- **Cross-reference (house rule 2):** check `docs/users.md`,
  `docs/non-functional-requirements.md` and the rule docs for anything
  this contradicts or duplicates before refining.
