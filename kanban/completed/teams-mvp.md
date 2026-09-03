# Story — Teams: scoring teams, protection groups and team classification (Option 2 MVP)

**Status:** Completed 2026-09-02 · **Raised:** 2026-08-31 ·
**Direction:** owner-assumed **Option 2 — "rule-spirit MVP"** from
`teams-feature-options.md` (repo root; research and options, not an approval —
the approvals it lists are now settled, see *Decisions settled* below). The
plan section is this story's job done: §Option 2 turned into WI-n items; do not
re-derive it from scratch.

## What

Implement team support for a competition **without** copying GliderScore's
central conflation (one team number driving both score aggregation and draw
protection — paper §"What GliderScore implements"):

- competition-scoped **scoring teams** with stable identities and names, plus
  per-member **contribution eligibility** — so a defending-champion-style
  member can be drawn alongside countrymen without contributing to their
  team score;
- independent **protection groups** (many-to-many membership) driving
  same-group separation in generated draws, with protection diagnostics and
  explicit least-bad handling of infeasibility;
- one classification policy, declared as competition-level configuration:
  sum each team's best three eligible individual scores, tie-break by the
  placing sum of the contributors then best individual placing;
- rosters, partial and final team standings through the API — report-ready
  data only, never rendered reports/badges/email (NFR-3);
- declared team classification captured at competition finalisation.

A competitor may be attached to **one scoring team** and, independently, to
**any number of protection groups** (owner decision 2026-09-02; the F5J junior
who sits in a national-team protection group *and* holds a protected helper
pair is the test case). Avoid the terms "national team" and "FAI team
classification" unless the competition actually supplies that context — a
generic scoring team must also serve local club/invitational formats (paper
§Option 2, closing note).

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

## Decisions settled with the owner (2026-09-02)

1. **Scope-change record** — the MVP scope note in
   `docs/rules/00-general-rules.md` (teams out of MVP software scope) is
   superseded by a dated, owner-approved scope amendment in
   `docs/non-functional-requirements.md` (WI-1). The rules corpus itself is
   not edited to fit product scope (house rule 1).
2. **Glossary approval** — concepts are approved *in principle*; the exact
   definitions drafted below (§Glossary candidates) are shown to the owner
   and WI-2 halts before any `/docs` edit until they are signed off.
3. **Protection model** — **many-to-many protection groups**: a competitor
   may belong to any number of named groups; a two-person group is the MVP
   representation of a protected pair. Not singular membership, not pairs-only.
4. **Finalisation** — capture the **full declaration**: team total, place,
   counting contributors (with their scores and placings), placing sum and
   best individual placing. Consistent with `Finalisation`'s existing meaning
   ("answers what was declared, never what is the score") and the paper's
   design principle 8.
5. **Infeasible protection** — the draw returns the **least-bad** draw
   (minimum protection violations), with violations visible as read-side
   diagnostics; the CD accepts or rejects through the existing draw
   accept/reject path. No rejection-upfront, no policy knob.
6. **Protection edits vs a live phase** — protection-membership commands are
   **refused while any live phase exists** ("reject the draw first"). One
   gate covers drawn-but-unaccepted (the CD rejects — free pre-acceptance —
   then edits, then redraws) and accepted (frozen with acceptance). A
   rejected draw removes the phase (D2, `draw-acceptance-redraw.md`) and
   reopens editing. Scoring-team commands have **no** draw gate ever — the
   draw never sees scoring teams.
7. **Classification method location** — **competition-level configuration**
   on the Competition stream (option A): enabled flag + method token from a
   closed vocabulary with exactly one MVP member (`bestThreeScoreSum`).
   No `ClassDefinition` change, no seed/hash/notation churn for a method
   constant across all classes. Option 3 later adds vocabulary members and
   may move the declaration into class data when real variation exists.
   This also keeps generic club-team and NZ use expressible today, which
   class-declared FAI policy could not (NZMAA strips national-team
   provisions from its classes while NZ club events still run club teams).
8. **GliderScore adapter mapping** (compatibility boundary only) — one team
   number maps to BOTH scoring-team membership and protection membership;
   `OmitFromTeamScore=true` makes it protection-only (no contribution);
   `UseTeamProtection=false` makes it scoring-only; `UseTeams=false` makes
   it neither; anything unrepresentable is ledgered as a semantic divergence.
9. **Rules-doc fidelity** — the condensed team-classification summary in
   `docs/rules/00-general-rules.md` is corrected to record both `C.15.6.2`
   methods (placing-sum and score-sum), matching the authoritative source.
   Fidelity fix, not a scope edit; source-docs stay verbatim. The MVP
   implements score-sum only regardless.

**Consequence settled during refinement (no owner sign-off needed):** there
is no reopen/refinalise path today — `Competition.Finalise` refuses a second
competition-scope finalisation (`finalise.alreadyFinalised`). Therefore
scoring-team corrections after finalisation are **allowed** (explicit,
auditable events, no draw gate) and the declaration is **not** retroactively
mutated: the divergence becomes visible through the declared-vs-derived read
(WI-7), exactly the stance `TaskRoundReopened`'s doc comment establishes for
individual results. A reopen/re-finalise command remains the already-recorded
deferred stub (`kanban/deferred-decisions.md` §Task-round lifecycle) — this
story does not build it.

## Refined plan

### Domain model — all inside the Competition aggregate

New file `src/Soarscore.Domain/Competitions/Teams.cs`; events in
`Competitions/CompetitionEvents.cs`; folds/decides in
`Competitions/Competition.cs`. Green-field: event and API shapes may change
directly, no compatibility scaffolding.

Ids (mirror `CompetitionId`: `readonly record struct X(Guid Value)`,
`IParsable`, `New()` via `Guid.CreateVersion7()`):

- `ScoringTeamId`, `ProtectionGroupId`.

Records:

- `ScoringTeam { ScoringTeamId Id; string Name }`
- `ProtectionGroup { ProtectionGroupId Id; string Name }`
- `ScoringTeamMembership { CompetitorId CompetitorRef; ScoringTeamId TeamRef; bool Contributes }`
  — at most one per competitor (0..1 scoring team per competitor).
- `ProtectionGroupMembership { CompetitorId CompetitorRef; ProtectionGroupId GroupRef }`
  — many per competitor (0..* groups).
- `TeamClassificationConfiguration { bool Enabled; string Method }` — `Method`
  is the closed vocabulary; exactly one MVP member, the literal
  `bestThreeScoreSum`. An unknown token reaching the classification engine is
  a defect (`teamClassification.unknownMethod`) — the forward-compat guard.
- `ProtectedPair(CompetitorId A, CompetitorId B)` — canonicalised unordered
  pair; the draw engine's entire view of protection.

`Competition` gains: `ImmutableArray<ScoringTeam> ScoringTeams`,
`ImmutableArray<ProtectionGroup> ProtectionGroups`,
`ImmutableArray<ScoringTeamMembership> ScoringTeamMemberships`,
`ImmutableArray<ProtectionGroupMembership> ProtectionGroupMemberships`
(all default-empty), and `TeamClassificationConfiguration? TeamClassification`
(null until first configured).

### Events (Competition stream; union + generic `Apply` switch extended)

- `ScoringTeamDefined(ScoringTeam Team, DateTimeOffset At)`
- `ScoringTeamMembershipAssigned(ScoringTeamMembership Membership, DateTimeOffset At)`
- `ScoringTeamMembershipCleared(CompetitorId CompetitorRef, DateTimeOffset At)`
- `ProtectionGroupDefined(ProtectionGroup Group, DateTimeOffset At)`
- `ProtectionGroupMemberAdded(ProtectionGroupMembership Membership, DateTimeOffset At)`
- `ProtectionGroupMemberRemoved(CompetitorId CompetitorRef, ProtectionGroupId GroupRef, DateTimeOffset At)`
- `TeamClassificationConfigured(TeamClassificationConfiguration Configuration, DateTimeOffset At)`

Fold semantics: `ScoringTeamMembershipAssigned` **replaces** any existing
membership record for that competitor (decide guarantees it names the same
team); `ScoringTeamMembershipCleared` removes all membership records for the
competitor; `ProtectionGroupMemberAdded` adds (decide prevents duplicates);
`ProtectionGroupMemberRemoved` filters the matching pair;
`TeamClassificationConfigured` replaces the configuration (last-wins; the log
is the audit trail, `ParameterBindings` precedent).

### Decide functions (`Competition.cs`, defect-chain style, own code prefixes)

- `DefineScoringTeam(ScoringTeamId id, string name, DateTimeOffset at)` —
  name blank → `defineScoringTeam.nameBlank`; name duplicates an existing
  scoring team (case-insensitive) → `defineScoringTeam.nameTaken`. No gates.
- `DefineProtectionGroup(...)` — same shape, `defineProtectionGroup.*`.
  Names may coincide with a scoring team's name — uniqueness is enforced
  within a kind only.
- `AssignScoringTeamMembership(CompetitorId competitorRef, ScoringTeamId teamRef, bool contributes, DateTimeOffset at)` —
  team must exist → `assignTeamMembership.teamNotFound`; competitor must exist
  → `…competitorNotFound`; must not be withdrawn → `…competitorWithdrawn`;
  an existing membership naming a **different** team →
  `…competitorAlreadyAssigned` ("clear the membership first"). Re-assigning
  the **same** team is the eligibility-correction path (flips `Contributes`).
  No draw gate, no finalisation gate (see the consequence note above).
- `ClearScoringTeamMembership(CompetitorId competitorRef, DateTimeOffset at)` —
  membership must exist → `clearTeamMembership.membershipNotFound`.
- `AddProtectionGroupMember(CompetitorId competitorRef, ProtectionGroupId groupRef, DateTimeOffset at)` —
  `Phases.IsEmpty` must hold → `addProtectionMember.drawExists` ("Protection
  membership is frozen once a phase has been drawn; reject the draw first.");
  group must exist → `…groupNotFound`; competitor must exist →
  `…competitorNotFound`; must not be withdrawn → `…competitorWithdrawn`;
  must not already be a member of THIS group →
  `…duplicateMembership` (multi-group membership is allowed and expected).
- `RemoveProtectionGroupMember(...)` — same phase gate; membership must exist
  → `removeProtectionMember.membershipNotFound`.
- `ConfigureTeamClassification(bool enabled, string by, DateTimeOffset at)` —
  `by` blank → `configureTeamClassification.byBlank`. Reconfiguration allowed;
  no gates (classification-only metadata, paper design principle 3).

### Draw wiring

`DrawPhase` derives the protected-pair set in a private helper: for each
`ProtectionGroup`, every unordered pair of its **live** members (registered ∧
`WithdrawnAt is null`); union across groups, deduped by canonical key — then
passes it to `PhaseDraw.BuildGroups`. Withdrawn members drop out because the
draw field excludes them. `PrescribeDraw` is untouched: prescribed imports
stay diagnostic-only, never a rejection path.

### `PhaseDraw.cs` — protected-pair input, least-bad objective

- Keep the existing `BuildGroups(field, minPerGroupByRound)` overload — it
  delegates to the new overload with `pairs = []`. Every existing call site
  and test is unchanged (regression gate).
- New overload `BuildGroups(field, minPerGroupByRound, ImmutableArray<ProtectedPair> protectedPairs)`:
  - **Protection-budget deepening outer, repeat-ceiling escalation inner,
    per round.** For budget `v = 0, 1, 2, …` (guaranteed to terminate:
    `v = protectedPairs.Length` — every pair violated — is always feasible),
    run the existing ceiling-escalating backtracking with the extra
    constraint that the round's total protected co-occurrences ≤ `v`. The
    first `v` with a feasible partition wins → minimum-violation (least-bad)
    draw, owner decision 5.
  - `TryBuildRound` carries the remaining budget; `CandidateGroups` computes
    each candidate group's internal protected-pair count; a candidate is
    admissible iff `count ≤ remainingBudget`. Ranking among admissible
    candidates is unchanged (worst resultant pairing → sum → field order) —
    determinism preserved, and the fairness-priority invariant holds by
    construction (violation count is fixed by the outer deepening; the
    repeat objective decides among equals).
  - A violation is a protected pair co-grouped **in this round**; the budget
    is per-round. Cross-round repeat state (`pairCount`) is untouched.
  - Structural checks unchanged: every eligible competitor exactly once per
    round.
- `PhaseDraw` learns **nothing** about scoring teams — pairs only
  (draw-engine discipline). Protection is a generic input; nothing branches
  on a class or a team.

### Classification engine — new `src/Soarscore.Domain/Scoring/TeamClassification.cs`

`TeamClassificationEngine.Classify(CompetitionResult individual, teams,
memberships, config)` — pure, downstream of individual ranking (design
principle 2; never inside flight scoring, normalisation, or phase
aggregation):

- config null or `!Enabled` → empty standings — a state, not an error.
- `config.Method` is not the known token → defect `teamClassification.unknownMethod`.
- **Contributors:** the three highest individual aggregate scores among the
  team's eligible members (`Contributes == true`) that hold a competition
  placing in `individual` (`CompetitionResult.Placings`; a disqualified
  member has no placing and cannot contribute). Fewer than three available →
  all available contribute (partial teams). Zero available → no contributors.
- **Total** = sum of contributor scores. **PlacingSum** = sum of contributor
  placings (0 when none). **BestIndividualPlacing** = min contributor placing
  (null when none).
- **Team order:** Total DESC → PlacingSum ASC → BestIndividualPlacing ASC
  (nulls last) → team Name ASC (deterministic display order for shared
  places). Placings assigned with the same shared-place convention
  `RankingEngine` uses.
- **Source classification:** the competition-scope final aggregate (post-drops,
  post-aggregate-penalties), i.e. exactly what `ScoreCompetition` produces.
- **Withdrawal behaviour (stated, not accidental):** membership is not
  auto-cleared on withdrawal; a withdrawn member still contributes if the
  individual classification still carries their score and placing (scores
  survive withdrawal — deferred-decisions §Annulments, decision 8). If the CD
  wants a withdrawn member out, that is an explicit
  `ClearScoringTeamMembership` correction.

Result types live beside the engine: `TeamClassificationResult` (standings +
`Method` + source-classification label) and `TeamStanding` (team ref and
name, total, placing, placing sum, best individual placing, contributors with
their scores and placings, and every member with a contribution state:
contributor / eligible-not-counting / ineligible / no-score-yet /
disqualified) — design principle 8's "counting members and tie-break
evidence, not an unexplained team total".

### Application commands — `src/Soarscore.Application/Commands/Competitions/`

Seven commands, one handler each, following the existing Competition command
pattern (load via `CompetitionLoader` → decide → append via `IEventStore`):

`DefineScoringTeam`, `DefineProtectionGroup`, `AssignScoringTeamMembership`,
`ClearScoringTeamMembership`, `AddProtectionGroupMember`,
`RemoveProtectionGroupMember`, `ConfigureTeamClassification`.

### Application queries — derived in-handler from the Competition aggregate (no new read-model documents)

- `GetTeamRosters` → `GET /competition-teams` — scoring teams with members
  and `Contributes`; protection groups with members. Separate sections; the
  view must make the scoring/protection separation visibly structural.
- `ScoreTeamStandings` → `GET /competition-team-result` —
  `{ derived: TeamClassificationResult | null, declared: DeclaredTeamResult[] | null }`.
  `derived` is null when team classification is disabled or never configured.
  `declared` is the latest competition-scope finalisation's
  `DeclaredTeamResults` when one exists, else null. This is the
  re-derive-versus-declared comparison the paper asks for and the **only**
  read surface for declared team results (no general finalisation read
  surface exists — `kanban/deferred-decisions.md` §Task-round lifecycle).
- `GetDrawProtectionDiagnostics` → `GET /draw-diagnostics` — for the live
  phase, per round, the protected pairs co-grouped (group ordinal + the two
  competitor refs). Empty when there are none. Identical treatment for
  generated and prescribed draws (diagnostic-only); reads the live phase's
  `Group.CompetitorRefs`, not draw history (that stays deferred).

### API — `src/Soarscore.Api` (`Commands.cs` / `Queries.cs`, kebab-case)

POST `/define-scoring-team`, `/define-protection-group`,
`/assign-scoring-team-membership`, `/clear-scoring-team-membership`,
`/add-protection-group-member`, `/remove-protection-group-member`,
`/configure-team-classification`;
GET `/competition-teams`, `/competition-team-result`, `/draw-diagnostics`.
The route-shape reflection test (GET/POST only) covers the new routes
automatically.

### Finalisation — declared team results

- `Finalisation` gains `ImmutableArray<DeclaredTeamResult> DeclaredTeamResults`
  (default empty; phase-scope finalisations never carry team results in the
  MVP).
- `DeclaredTeamResult { ScoringTeamId TeamRef; string Name; decimal Total;
  int Placing; ImmutableArray<DeclaredTeamContributor> Contributors;
  int PlacingSum; int? BestIndividualPlacing }`;
  `DeclaredTeamContributor { CompetitorId CompetitorRef; decimal Score; int Placing }`.
- `Competition.Finalise` gains `declaredTeamResults` (already computed by the
  handler, exactly like `declaredResults` — the handler scores the
  competition and the team classification and maps both). Decide validation:
  team classification disabled/absent ∧ non-empty →
  `finalise.teamResultsNotPermitted`; enabled ∧ teams defined ∧ empty →
  `finalise.teamResultsMissing`.
- `FinaliseCompetition` handler extended accordingly. Green-field: the
  `Finalisation` JSON shape changes directly, no migration.

### Work items

**WI-1 — Docs: scope amendment + rules fidelity (no code).**
- `docs/non-functional-requirements.md`: dated scope amendment (2026-09-02,
  owner-approved) — team separation and team classification are in MVP
  software scope as specified by this story (Option 2); NFR-1/NFR-2's
  class-variation law is untouched (this feature adds no class-specific
  behaviour).
- `docs/rules/00-general-rules.md`: correct the condensed
  team-classification note to record both `C.15.6.2` methods (placing-sum
  and score-sum). `docs/rules/source-docs/` untouched.
- Validate: docs-only diff review against this story's decision log.

**WI-2 — Glossary + class diagram (GATE: owner sign-off).**
- Present §Glossary candidates below to the owner verbatim. **Do not edit
  `/docs` until approved** (decision 2). Record the approval in this story.
  **Gate cleared 2026-09-02** — the §Glossary candidates were shown to the
  owner verbatim and approved as written on 2026-09-02.
- Then: add the three concepts to `docs/soaring-domain-glossary.md`;
  extend `docs/soaring-domain-class-diagram.md` (entities within the
  Competition aggregate; membership associations from Competitor;
  `TeamClassificationConfiguration` as competition-level configuration;
  `Finalisation` gains `DeclaredTeamResults`).
- This WI gates WI-3 (events must not land under unapproved names).

**WI-3 — Domain events, records, decide functions, folds.**
- `Teams.cs`, `CompetitionEvents.cs`, `Competition.cs` exactly as specified
  above (records, seven events, folds, eight decide functions, `DrawPhase`
  pair derivation, generic `Apply` switch arms). `Finalisation` changes wait
  for WI-7.
- Unit tests (tests/Soarscore.Domain.Tests): every decide function's happy
  path and every defect code; the phase gate on protection commands (refused
  drawn-not-accepted AND accepted; allowed again after rejection); fold
  semantics (membership replacement, last-wins configuration).
- JSON round-trip of every new event; event registration + replay round-trip
  in tests/Soarscore.Infrastructure.Tests via `IStoreFixture` — runs on
  SQLite (fast loop) and PostgreSQL (Testcontainers, `Category="Storage"`);
  register the events in both `MartenConfig` and `FisherConfig`.

**WI-4 — Draw protection.**
- `ProtectedPair`, the `BuildGroups` overload, `DrawPhase` wiring.
- CsCheck property tests (tests/Soarscore.Domain.Tests, beside the existing
  PhaseDraw properties):
  - *Protected separation:* when a zero-violation partition exists for the
    group shape, the generated draw co-groups no protected pair.
  - *Structural preservation:* with protection input, every eligible
    competitor appears exactly once per round — never duplicated, lost, added.
  - *Fairness priority:* brute-force joint-optimum comparison at small scale
    (the existing WI-2 precedent), extended with the protection dimension:
    minimum violations first, then the repeat objective.
  - *Regression:* all existing draw properties rerun with `pairs = []` and
    pass unchanged.
- Infeasibility: a test where a protection group exceeds group size — the
  draw returns the minimum-violation partition and terminates.

**WI-5 — Classification engine.**
- `TeamClassification.cs` + result types. Unit tests: eligibility and
  omission (defending-champion case), partial teams (1–2 contributors),
  teams with no scored members, zero and negative scores, disqualified
  members, ties resolved by placing sum then best individual placing then
  shared.
- CsCheck properties: *contributor selection* (exactly the top three eligible
  members with placings, input order cannot change them), *classification
  determinism* (permuting teams, members, or individual-result input cannot
  change contributors, tie-break values, or team order), *partial-result
  monotonic availability* (adding an individual result recomputes a standing;
  absence never prevents one).

**WI-6 — Application + API surface.**
- Seven command handlers, three queries, ten routes, response records.
- tests/Soarscore.Application.Tests (through fakes): defect propagation per
  command; roster view proves scoring and protection memberships remain
  distinct; assignment/clear/correction sequences replay to accurate views;
  standings view returns derived-null / declared-null states correctly.
- Bump the `HandlerRegistrationTests` sanity floor (smaller-items.md
  precedent — the floor exists to catch a silently-stopped reflection scan).

**WI-7 — Finalisation capture.**
- `Finalisation.DeclaredTeamResults` + `DeclaredTeamResult` types;
  `Finalise` extension + validation codes; `FinaliseCompetition` handler
  derives and maps team standings at finalisation; `ScoreTeamStandings`
  declared section.
- Unit tests: the two new decide codes; declared-vs-derived divergence after
  a post-finalisation scoring-team correction is visible through the
  standings query (the individual-result divergence precedent).
- Store-backed tests (both stores): finalisation with team results replays;
  the declared section round-trips.

**WI-8 — BDD acceptance (NFR-4).**
- tests/Soarscore.Acceptance.Tests, one Given/When/Then scenario: teams
  configured → memberships assigned (one non-contributing member) → draw
  accepted → scores captured **out of order** across rounds → team standings
  available and correct after each capture, with no step gated on any other
  (NFR-4: partial standings from whatever scores are present) → finalisation
  captures declared team results equal to the derived standings at that
  moment.
- Runs under `SOARSCORE_TEST_STORE=postgres` and `=sqlite`.
- **Landed 2026-09-02 (WI-8):** `Features/ScoringTeams.feature` +
  `Steps/ScoringTeamsSteps.cs` — one scenario, *"Team standings stay correct
  while scores trickle in out of order across rounds"*. Memberships via a
  Gherkin table (Falcons 3-4-5 contributing; Harriers 1 **no** — the
  defending champion, fastest in the fixture — 2 and 6), classification
  enabled, protection pair (2, 5) set up pre-draw, F5J 4 rounds drawn and
  accepted; `/draw-diagnostics` then names the pair in all 4 rounds
  (MinPerGroup 6 → one group — infeasible protection, least-bad draw +
  diagnostics + accept, owner decision 5). 24 captures scripted out of order
  across all rounds (round 2 before round 1; rounds complete 2-1-3-4), each
  recording a standings+leaderboard snapshot; the derived section is verified
  against an independent oracle (classification rules re-derived from
  `/competition-result`, never normalisation arithmetic) after every capture
  and live at two partial checkpoints; the finished standings are pinned
  literally (Falcons 8400 1st / Harriers 5600 2nd, placing sums 12/8, best
  placings 3/2, contributors with scores and placings, the champion
  Ineligible); finalisation's declared section is asserted field-for-field
  equal to the derived standings, which re-derive unchanged. Suite 72/72 on
  SQLite and PostgreSQL; build 0 warnings.

**WI-9 — GliderScore adapter + team-bearing fixtures.**
- Extend the replay DTOs with `Team`, `OmitFromTeamScore`, `UseTeams`,
  `UseTeamProtection`, `NbrForTeamScore` (currently ignored).
- Mapping (decision 8): `UseTeams` ⇒ team number → scoring team
  `Team {n}` with `Contributes = !OmitFromTeamScore`; `UseTeamProtection` ⇒
  same number → protection group `Protection {n}`; the switches off ⇒ the
  respective membership absent. `NbrForTeamScore ≠ 3` ⇒ ledgered divergence
  (the MVP contributor count is fixed at three) — never emulated, R1
  precedent (`kanban/deferred-decisions.md` §GliderScore replay harness).
- Curate the team-bearing fixtures; replay through the harness on both
  stores; compare team contributors, totals and ranks where semantics
  overlap; ledger every semantic deviation in the fixture's
  `divergences.json` per existing practice.
- **Landed 2026-09-02 (WI-9):** DTOs widened
  (`CompPilotRow.Team`/`OmitFromTeamScore`, new `TriageTable` with the three
  switches); `ReplayDriver.MapGliderscoreTeamsAsync` implements decision 8
  verbatim (protection mappings precede `/prescribe-draw` — the
  `addProtectionMember.drawExists` gate; classification configured enabled
  only when the fixture's declared method IS the MVP's, `NbrForTeamScore ==
  3`); Comparator gains the team grain — run only where semantics overlap
  (f3j-international), asserting the MVP contract over the oracle-verified
  individual result (contributors, totals, placing sum, best placing, member
  states, order, shared places). Ledgered: one `T1` entry each for
  f3k-sample-comp (Nbr=2) and jerilderie-2010 (Nbr=4); the token is defined
  in deferred-decisions.md. Corpus facts: NO fixture anywhere in the corpus
  or its source extraction witnesses `OmitFromTeamScore=true` (protection-only
  arm implemented, unexercised by replay); no fixture carries GS
  team-standings output (the transcripts' Team column is display-only), so
  the overlap comparison pins the contract rather than GS's own team ladder;
  an inert `NbrForTeamScore=2` under `UseTeams=false`
  (f3j-international-flyoff) is not ledgered — GS computes no team scores
  there. Curated set: f3j-international (full mapping + comparison, 8 teams),
  f3k-sample-comp (Nbr≠3 + T1), jerilderie-2010 (Nbr≠3 + T1, 14 teams),
  ales-sample-comp (populated teams under `UseTeams=false` ⇒ nothing maps).
  Acceptance suite 71/71 on SQLite and PostgreSQL; build 0 warnings.

Sequencing: WI-1 anytime; WI-2 gates WI-3; WI-4 and WI-5 need WI-3; WI-6
needs WI-3/4/5; WI-7 needs WI-5/6; WI-8 needs WI-6/7; WI-9 needs WI-6. Take
the story into `in-progress/` before code (board rule 3).

### Property invariants (adopted from paper §Option 2 — named at planning, house testing rule)

1. **Individual-score independence** — for fixed class, draw, entries and
   rulings, changing team or protection metadata leaves every individual
   score and placing unchanged. (WI-5 property; scored output compared
   before/after arbitrary team-event sequences.)
2. **Protected separation** — if a zero-violation draw exists for the
   requested group shape, generated draws contain no protected pair in one
   group. (WI-4.)
3. **Structural preservation** — protection never duplicates, loses, or adds
   a competitor; every eligible competitor appears exactly once per
   task-round. (WI-4.)
4. **Fairness priority** — among draws with the same minimum
   protection-violation count, the existing repeat-co-occurrence objective
   remains the deciding fairness measure. (WI-4, brute-force comparison.)
5. **Contributor selection** — a team's contributors are exactly its three
   highest-scoring eligible members under the declared source
   classification. (WI-5.)
6. **Classification determinism** — permuting teams, members, or individual
   result input cannot change contributors, tie-break values, or team order.
   (WI-5.)
7. **Partial-result monotonic availability** — adding a newly available
   individual result can recompute a team standing, but absence never
   prevents a standing being returned. (WI-5 property; WI-8 acceptance.)

### Store and validation discipline

- Every store-backed test and the BDD suite run unchanged on PostgreSQL and
  SQLite (decide/fold/property tests stay store-free).
- New events registered in `MartenConfig` and `FisherConfig`; inline
  projections only, never the async daemon (LADR-0001). No new read-model
  documents — the three new queries derive views from the Competition
  aggregate in-handler, so no projection folds change.
- Layer rules hold: Domain stays BCL-only; Application never references a
  store; all team knowledge lives in generic data (no named-class branch
  anywhere — NFR-1/NFR-2).

### Standing exclusions (do not silently absorb)

No officials/team managers, no delegation/eligibility validation, no roster
sizes or substitutions, no event-type mandatory/prohibited protection policy,
no adjacent-group (immediately-following-group) preference, no
placing-sum method, no fly-off protection (fly-off draws remain deferred —
`kanban/deferred-decisions.md` §Draw), F3B multi-task draws remain deferred
and cap any class-support claim, no physical lanes/spots (lane assignment
shipped separately — `kanban/completed/lane-assignment.md`), no team rename
or team-removal events, no reopen/refinalise command (existing deferred
stub). Each is an Option-3 upgrade path (paper §Option 2, "Upgrade path").

### Glossary candidates (WI-2 — shown to the owner verbatim before any /docs edit)

- **Scoring Team** — a competition-scoped named team, defined for one
  competition, whose members' individual results may contribute to that
  competition's team classification. Never inferred from a person's club,
  nationality, or any other person-level fact.
- **Protection Group** — a competition-scoped named set of competitors kept
  apart — no two in the same group — by a generated draw. Draw-only meaning:
  it never affects scores, normalisation, or classification. A competitor may
  belong to any number of protection groups. A two-member protection group is
  the MVP representation of a protected pair (e.g. an F5J junior and the
  helper nominated at registration).
- **Contribution Eligibility** — the per-member property of a scoring-team
  membership that decides whether that member's individual result may count
  toward the team classification. False for a member who competes alongside
  their team without contributing to it (the defending-champion case).

### Cross-reference (house rule 2 — done during refinement, 2026-09-02)

- `docs/users.md`: no new user roles — team managers stay out (exclusion);
  the Organiser/CD/Scorer/Pilot set already covers every actor in this plan.
- NFR-1/NFR-2: no named-class branches; classification config is
  competition-level data; the draw consumes generic protected pairs.
- NFR-3: report-ready HTTP data only — no reports, badges, email.
- NFR-4: partial standings; nothing in this feature gates score capture or
  drawing (the protection phase gate gates *membership edits*, never
  capture, and is the owner-settled lifecycle rule, not an ordering
  imposition).
- `docs/rules/`: corpus untouched except the approved WI-1 fidelity fix;
  the sport corpus continues to outrank product scope (house rule 1).
- `kanban/deferred-decisions.md`: fly-off draws, F3B multi-task rounds, no
  draw-history read surface, no finalisation read surface — all still
  honoured; the GS binary64/ledger discipline (R1) extends to
  `NbrForTeamScore ≠ 3` fixtures.
- `docs/rules/00-general-rules.md` MVP scope note: superseded by the WI-1
  amendment; the story is the pointer between them.
