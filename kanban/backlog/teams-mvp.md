# Story — Teams: scoring teams, protection groups and team classification (Option 2 MVP)

**Status:** Backlog · **Raised:** 2026-08-31 (direction decision: owner has
assumed **Option 2 — "rule-spirit MVP"** from the teams research paper. The
paper `teams-feature-options.md` at the repo root is the primary context for
this story: it is research and options, not an approval — the explicit
approvals listed under *Before starting* are still required before code.)

## What

Implement team support for a competition **without** copying GliderScore's
central conflation (one team number driving both score aggregation and draw
protection — paper §"What GliderScore implements"):

- competition-scoped **scoring teams** with stable identities and names, plus
  per-member **contribution eligibility** — so a defending-champion-style
  member can be drawn alongside countrymen without contributing to their
  team score;
- independent **draw-protection groups** (or explicit protected pairs — see
  Before-starting decision 1) driving same-group separation in generated
  draws, with protection diagnostics and explicit handling of infeasibility;
- one classification policy: sum each team's best three eligible individual
  scores, tie-break by the placing sum of the contributors then best
  individual placing;
- rosters, partial and final team standings through the API — report-ready
  data only, never rendered reports/badges/email (NFR-3);
- declared team classification captured at competition finalisation
  (subject to Before-starting decision 2).

A competitor may be attached to one scoring team and independently to one
protection grouping. Avoid the terms "national team" and "FAI team
classification" unless the competition actually supplies that context — a
generic scoring team must also serve local club/invitational formats (paper
§Option 2, closing note).

All scope detail — changes required, risks, sequence, salient exclusions —
is already worked out in `teams-feature-options.md` §"Option 2: Minimal
rule-spirit MVP" (including the current-code seam inventory in §"Current
Soarscore baseline": `Competitor`/`Competition`/`Finalisation` in
`src/Soarscore.Domain/Competitions/Competition.cs`, `PhaseDraw.cs`,
`ClassDefinition.cs`, the scoring engine, and `CompetitionScoreView`). This
story's job is to turn that section into a refined plan (WI-n items); do not
re-derive it from scratch.

## Why it matters

- GliderScore's single integer `Team` field is the one model known not to
  represent the rules cleanly; Option 2 is the smallest model that avoids
  embedding that mistake while giving credible team-aware support for club,
  national and some international use (paper §Executive summary and
  §Comparison).
- The upgrade path to a complete rule-aware implementation (Option 3) is
  preserved by construction — scoring teams can later gain delegations,
  protection groups can gain reason/applicability, the fixed method can
  become one variant in a policy vocabulary (paper §Option 2, "Upgrade
  path").
- The GliderScore fixture corpus already carries team-related fields; the
  replay DTOs deliberately ignore them today. Team-bearing fixtures can
  provide parity evidence without sourcing a new corpus (paper §"Current
  Soarscore baseline").

## Before starting

- **Scope-note conflict to resolve first (house rule 2 — surface, don't
  reconcile):** `docs/rules/00-general-rules.md` MVP scope note says team
  separation and team classification are **out of MVP software scope**.
  Selecting Option 2 requires an owner-approved scope change; the sport
  corpus itself must not be edited to fit product scope (house rule 1).
  Agree with the owner where the scope change is recorded before refining.
- **Glossary and class-diagram approval.** New concepts (scoring team,
  protection grouping vs protected pairs, contribution eligibility) need
  explicit owner approval before `docs/soaring-domain-glossary.md`, the
  class diagram, or any events land (paper §Status and purpose; house
  rules 3–4).
- **Decisions to settle before refinement** (paper §"Decisions needed
  before story refinement", the ones Option 2 does not answer by itself):
  1. Singular protection group vs explicit protected pairs /
     many-to-many — settle *before* membership events land; prefer
     explicit pairs if real examples need overlapping relationships
     (decision 4; F5J junior/helper is the test case).
  2. Declared team results at finalisation, or re-derivation only — the
     existing meaning of finalisation favours capture (decision 2).
  3. Infeasible protection: reject the draw, return least-bad for explicit
     acceptance, or support both by policy (decision 3).
  4. Team edits between draw production and acceptance — allowed, and does
     the edit discard the candidate draw (decision 6)?
  5. What finalisation declares: total + place only, or also contributors
     and tie-break evidence (decision 7; paper design principle 8 wants
     counting members and tie-break evidence in output).
  6. Where the MVP classification method is declared: adopted class data
     vs competition-level local configuration clearly labelled not-an-FAI
     policy (decision 12, Option-2 fork stated in paper §Option 2).
  7. GliderScore adapter default: one team number maps to *both* scoring
     and protection membership at the compatibility boundary — how are
     exceptions surfaced (decision 11)?
- **Name the property invariants during planning, not implementation**
  (house testing rule). Option 2 already has seven written down — adopt or
  amend them explicitly: individual-score independence, protected
  separation, structural preservation, fairness priority (existing
  repeat-co-occurrence objective remains the deciding measure after
  protection count), contributor selection, classification determinism,
  partial-result monotonic availability (paper §Option 2, "Explicit
  property invariants"). Draw and classification properties are the
  CsCheck targets; the NFR-4 partial-standings workflow is the BDD
  acceptance case (out-of-order score capture must not gate anything).
- **Lifecycle boundaries to carry into the plan:** protection membership
  frozen with draw acceptance; scoring-team corrections via explicit
  auditable events; reopen/refinalise after declaration; assignment
  changes after acceptance rejected unless the draw is invalidated first
  (paper design principles 3, 7 and §Option 2 changes).
- **Draw-engine discipline:** `PhaseDraw` input generalises to protected
  pairs / a protection lookup — never taught about scoring teams; rank
  candidates by protection violations first, then the existing fairness
  measures; prescribed-draw import stays diagnostic-only, never a
  rejection path; rerun all existing draw properties against
  protection-free competitions to prove no regression (paper §Option 2
  changes and risk table).
- **Store and validation discipline:** every store-backed test and the BDD
  suite run unchanged on PostgreSQL and SQLite; GliderScore team-bearing
  fixtures replayed through an adapter with semantic deviations ledgered,
  matching the harness's existing divergence-ledger practice (paper
  §Option 2 sequence, step 8).
- **Standing exclusions (do not silently absorb):** no officials/team
  managers, no delegation/eligibility validation, no roster sizes or
  substitutions, no event-type mandatory/prohibited protection policy, no
  adjacent-group preference, no physical lanes/spots (separate story —
  `kanban/backlog/lane-assignment.md`), no placing-sum method, no fly-off
  protection (fly-off draws remain deferred), F3B multi-task draws remain
  deferred and cap any class-support claim (paper §Option 2, "Salient
  exclusions"; `kanban/deferred-decisions.md` §Draw).
- **Documentation issue to resolve during refinement, with the owner:** the
  condensed general-rules summary describes only score-sum team
  classification while FAI `C.15.6.2` permits placing-sum too; the official
  source is authoritative and the read-only corpus is not edited to fit
  (paper §Cross-reference, closing note). Surface via the `fai-rules`
  skill when refining the classification work item.
- **Cross-reference (house rule 2):** re-verify `docs/users.md`,
  `docs/non-functional-requirements.md` (NFR-1/2 data-driven variation,
  NFR-3 headless, NFR-4 partial standings) and the rule docs against the
  refined plan; the paper's §Cross-reference and conflict check is the
  starting point, not a substitute.
