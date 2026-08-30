# Teams: Findings and Implementation Options

## Status and purpose

This is a research and option paper. It is not a requirement, an architecture
decision, a delivery plan, or a set of stories. Its purpose is to support a
direction decision before implementation work is refined into stories.

The paper answers four questions:

1. What the teams feature is for and when it affects a competition.
2. What GliderScore implements in practice.
3. What the FAI rules distinguish that GliderScore does not.
4. What three credible implementation envelopes would mean for Soarscore.

No option is approved by this paper. Each introduces concepts that are not in
the current domain glossary. Choosing an option therefore requires explicit
approval before the glossary, requirements, class diagram, or other files under
`docs/` are changed.

## Executive summary

The original hypothesis that teams affect only the draw and a team ranking at
the end is directionally correct but incomplete.

Teams do not normally alter an individual flight's measurements, raw score,
group normalisation, penalties, discard, or individual placing. The two primary
sporting effects are:

- draw protection, which keeps related competitors apart where the applicable
  rules require or permit it; and
- a separate team classification derived from individual results.

The operational footprint is wider. Team information is needed during event
registration, competitor entry, draw feasibility checking, late changes, flying
order and sometimes lane allocation, interim and final reporting, publication,
and team-manager administration.

The most important finding is that **team is not one domain concept**:

| Concept | Purpose | Typical effect |
|---|---|---|
| National or scoring team | Represents a country or other entered team in a team classification | Select eligible individual results and produce a team placing |
| Working team | Groups competitors who share operational support or equipment | Draw protection and field organisation |
| Protected relationship | Keeps particular competitors apart without making them one scoring team | Draw only; F5J junior and nominated helper is the clearest example |
| Team manager or assistant | Represents and assists an entered national team | Registration, administration, disputes, awards; not a scoring member merely by holding the role |

GliderScore largely collapses these distinctions into one competition-scoped
integer `Team` field. The same value controls draw protection, lane and sequence
behaviour, team-score aggregation, display, sorting, email grouping, and online
publication. An `OmitFromTeamScore` flag handles the common defending-champion
exception, but does not remove the underlying conflation.

The three options are:

| Option | Summary | Principal advantage | Principal cost or risk |
|---|---|---|---|
| 1. GliderScore parity only | Adopt one numbered team per competitor, an omit flag, optional protection, and configurable top-N score aggregation | Fastest route to familiar GliderScore behaviour and fixture comparison | Deliberately inherits a model known not to represent the rules cleanly |
| 2. Rule-spirit MVP | Separate scoring-team membership from draw-protection membership, while implementing only same-group protection and one score-sum classification | Smallest model that avoids the central GliderScore mistake | Does not enforce championship eligibility, event-specific applicability, adjacent-group rules, officials, or every classification method |
| 3. Complete rule-aware implementation | Model delegations, national and working teams, protected relationships, officials, applicability policy, complete draw constraints, and classification policies | Provides a credible foundation for international championship use | Broadest domain, class-model, draw, lifecycle, and user-model change |

**Provisional recommendation:** Option 2 is the best default direction if the
immediate goal is wider adoption without claiming full championship-management
compliance. It creates an upgrade path to Option 3 without embedding the known
GliderScore conflation. Option 1 is justified only if compatibility speed is
more important than preserving the distinctions already visible in the rules.

## Research findings

### Who teams serve

Teams create needs for several users and participants:

- The **Organiser** records entered teams, members, exclusions, officials, and
  any working-team arrangements before producing the draw.
- The **Contest Director** needs a defensible draw, visibility of unavoidable
  protection violations, control of overrides, and a trustworthy declared team
  result.
- The **Scorer** normally remains unaffected. Raw measurements and individual
  score capture do not become team-scoped.
- The **competitor** needs the team-aware draw and both individual and team
  standings.
- The **team manager** represents the team to the organiser and Jury and may
  assist competitors. FAI `C.5.2` makes the role obligatory at World and
  Continental Championships.
- The **national aero club or delegation** is the entity represented by the
  national team and may receive the championship trophy.

The current user document names Organiser, Contest Director, Scorer, and Pilot,
but not Team Manager or delegation. A complete option would therefore require a
user-model decision, not just additional fields on `Competitor`.

### When team information matters

| Competition stage | Team-related need |
|---|---|
| Entry and registration | Record official team membership, working grouping, contribution eligibility, protected helper relationships, and officials |
| Before drawing | Resolve which protection policy applies and whether the requested group shape is feasible |
| Drawing | Avoid protected co-occurrences, minimise repeat meetings, account for group order, and report any unavoidable violations |
| Field operation | Present team-aware draw information and, where supported, coordinate sequence, lanes, spots, helpers, or equipment |
| During scoring | Leave individual score calculation unchanged; derive partial team standings from available individual results |
| Finalisation | Capture the declared individual and team classifications and their contributors/tie-break evidence |
| Publication | Expose team rosters, draw context, counting and non-counting members, and team standings |

Changing a team assignment after accepting a draw can invalidate the assumptions
under which that draw was accepted. Changing contribution eligibility after
results exist can change the team classification without changing any flight.
Both are legitimate corrections in an event-sourced system, but they require
explicit lifecycle rules and an audit trail.

### FAI rules that shape the feature

The following are the significant rule facts, not a complete championship entry
manual:

- FAI `C.5.2`: a team manager may assist competitors and is the only person
  normally permitted to deal with the Jury or organiser over disputes,
  complaints, and protests. RC Soaring permits an assistant team manager.
- FAI `C.5.3`: a national team at a World or Continental Championship may have
  up to four or five competitors depending on championship structure, plus a
  manager. A reigning champion can compete outside the national team and then
  does not contribute to its team result.
- FAI `C.15.6.2`: national-team classification uses one of two methods: sum the
  final placings of the three best members, or sum the final scores of the three
  best-scoring members. Completeness ordering and tie-breaks differ by method.
- FAI `C.16.2.6`: initial starting order is random and includes team-related
  separation provisions. F3B, F3J, and F3K add an immediately-following-group
  consideration where possible.
- F5J `5.5.11.8.1(c)`: team protection is mandatory at World and Continental
  Championships except fly-offs, prohibited at Open International and World Cup
  events, and separately required between a junior and the competing helper
  nominated at registration.
- F3 rules use **working teams**, including combining incomplete teams and
  attaching a reigning champion for operational purposes. This need not match
  scoring-team membership.
- Class rules can identify which individual score feeds the team result. It is
  unsafe to assume every class always uses a post-fly-off final score; some
  class wording refers specifically to qualifying aggregates.
- NZMAA domestic variants explicitly remove references to national teams and
  team managers for the relevant international classes. Team behaviour cannot
  be switched on merely because the competition class has an FAI name.

These facts mean that complete applicability depends on at least the governing
rules, event type, competition class, phase, and protected relationship. A
single class-wide `UseTeamProtection` flag cannot represent the complete rule.

### What GliderScore implements

GliderScore's practical model consists chiefly of:

- competition switches `UseTeams` and `UseTeamProtection`;
- a configurable `NbrForTeamScore`;
- an integer `CompPilots.Team` on each competition entrant; and
- a Boolean `CompPilots.OmitFromTeamScore`.

The help describes the setup at
`/home/pete/source/gliderscore/GliderScore_Master/Information_MOD.vb:1313-1317`
and `:1344-1350`. The defending-champion use of the omit flag is documented at
`:1953-1965`.

The team score is calculated at report time:

1. Start with each competitor's calculated individual result.
2. Remove entrants marked `OmitFromTeamScore`.
3. Sort eligible members of each numbered team by score.
4. Retain up to `NbrForTeamScore` members.
5. Sum their scores and rank teams by the sum.

The implementation is in
`/home/pete/source/gliderscore/GliderScore_Master/Rpt_Results_TeamResults_MOD.vb:303-468`.
It does not change persisted individual scores.

GliderScore also uses the team number for:

- generated-draw protection and feasibility;
- late-pilot placement and manual-move warnings;
- pair-meeting diagnostics;
- speed/F5B flying sequence;
- F3J-style team and closest-lane allocation;
- team draw and pilot-list reports;
- team columns and sorting across ordinary reports;
- badges, scoring sheets, score cards, email grouping, and online upload.

Important limitations and discrepancies include:

- National scoring team and working team cannot be represented independently.
- A protected junior/helper pair cannot be represented directly.
- Team managers are loose entrant roles rather than officials attached to a
  team.
- Country and club are metadata; neither independently creates protection or a
  team classification.
- Protection is operator-configurable even where F5J mandates or prohibits it.
- The generated grouped draw principally enforces same-group separation, not
  the F3 immediately-following-group preference.
- GliderScore implements score-sum classification, not both current FAI
  methods, and permits a configurable contributor count rather than requiring
  three.
- Its tie resolver is only invoked in selected class modes and does not provide
  a clean current-FAI rule implementation across all classes.
- A single team number is used as a communication boundary, including sending
  related electronic scoring links. This is useful operationally but creates an
  access and privacy consequence for consuming systems.

GliderScore parity should therefore mean an explicitly selected compatibility
contract, not uncritical reproduction of every observed quirk.

## Current Soarscore baseline

Soarscore currently has no team, nation, delegation, working-team, protected
relationship, helper, or team-official production type.

The principal existing seams are:

- `Competitor` in
  `src/Soarscore.Domain/Competitions/Competition.cs`: competition-specific
  participation is the right home for membership references and eligibility;
- `Competition`: already owns the field, draw, acceptance, withdrawal, and
  finalisation lifecycle;
- `PhaseDraw` in
  `src/Soarscore.Domain/Competitions/PhaseDraw.cs`: currently optimises only
  pairwise repeat co-occurrence;
- `ClassDefinition` and `PhaseDefinition` in
  `src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs`: currently
  contain no draw-protection or team-classification policies;
- `ScoringService`, `RankingEngine`, and the result types under
  `src/Soarscore.Domain/Scoring/`: produce individual classifications only;
- `CompetitionScoreView` in
  `src/Soarscore.Application/Queries/Scoring/ScoreCompetition.cs`: the natural
  API read seam for additional derived classifications; and
- `Finalisation` and `DeclaredResult` in `Competition.cs`: currently capture
  only competitor results.

The GliderScore fixture corpus already carries team-related fields, but the
acceptance replay DTO deliberately ignores them. Team-bearing fixtures can
eventually provide parity evidence without sourcing a new corpus.

Relevant existing constraints are:

- NFR-1 and NFR-2 require class differences to remain data-driven. The core
  must not branch on F3B, F3J, F5J, or any other named class.
- NFR-3 keeps Soarscore headless. It should expose report-ready data, not build
  GliderScore-style printable reports, badges, or email workflows.
- NFR-4 requires partial standings from whatever scores are present. Team
  standings must not require earlier rounds or all team members to be complete.
- The repository is green-field with no production users or data. Event and
  API shapes may be changed directly; compatibility scaffolding is unnecessary.
- Fly-off draws and F3B multi-task draws are already deferred. A complete teams
  claim cannot quietly imply those existing gaps are solved.
- The current prescribed-draw path intentionally preserves an imported realised
  schedule rather than validating draw fairness. Team diagnostics should not
  silently turn prescribed draw import into a rejection path.

## Design principles common to all options

Whichever option is chosen, the following should hold:

1. Team information is competition-specific. Do not infer it from a person's
   club or permanently attach current competition membership to `Person`.
2. Team classification is downstream of individual ranking. Its tie-breaks can
   consume individual placings, so it does not belong inside flight scoring,
   normalisation, or phase aggregation.
3. For a fixed draw and fixed entry data, adding or changing team classification
   metadata must not change any individual score or placing.
4. Generated-draw protection and prescribed-draw preservation are different
   concerns. A prescribed schedule may carry diagnostics without being rejected.
5. Protection infeasibility must be explicit. Large protected groups and small
   fields can make perfect separation impossible.
6. Derived team standings must work with partial score capture in accordance
   with NFR-4.
7. Draw-affecting configuration must be frozen with, or invalidate, an accepted
   draw. Classification-only corrections need an auditable event and clear
   finalisation behaviour.
8. Result output should identify counting members and tie-break evidence, not
   present an unexplained team total.
9. Any class or phase variation belongs in adopted class data, not named-class
   branches.
10. Sequence position is not automatically a physical lane, launch spot,
    landing spot, or winch line. Do not overload the existing ordered competitor
    array without deciding which fact is being represented.

## Option 1: GliderScore parity only

### Scope

This option deliberately adopts GliderScore's single-team abstraction:

- teams can be enabled for a competition;
- each competitor has zero or one numeric team number;
- a competitor may be omitted from the team score while retaining team
  protection;
- team protection can be enabled or disabled;
- the number of scores counting for each team is configurable;
- generated draws avoid same-team competitors in one group where feasible;
- a team classification sums each team's best N individual scores;
- API results expose team numbers, contributors, team totals, percentages,
  placing, and tie-break data; and
- team-aware ordering or lane allocation is added only to the extent selected
  as part of the explicit parity contract.

NFR-3 excludes parity for desktop presentation concerns such as print layouts,
badges, email composition, and rendered reports. Soarscore would expose the data
needed by a consuming application to build them.

Before implementation, "parity" must settle whether it means:

- semantic parity with GliderScore's supported controls; or
- bug-for-bug and class-mode-for-class-mode output parity.

Semantic parity is strongly preferable. Exact reproduction of class-specific
tie behaviour and known inconsistencies would conflict with the architecture
and current FAI rules.

### Changes required

- Add competition configuration equivalent to `UseTeams`,
  `UseTeamProtection`, and `NbrForTeamScore`.
- Extend competition participation with `TeamNumber?` and
  `OmitFromTeamScore`.
- Add auditable commands/events for assigning and correcting those values.
- Define lifecycle rules for team changes before and after draw acceptance and
  finalisation.
- Pass team membership into `PhaseDraw` and add same-group conflict count ahead
  of repeat-meeting count in candidate ordering.
- Add draw diagnostics listing protected conflicts and whether they were
  unavoidable or explicitly overridden.
- Add a team-classification function after individual ranking.
- Extend `CompetitionScoreView` with team standings.
- Decide whether declared finalisation captures team results or always derives
  them. Capturing them is more consistent with the existing meaning of
  finalisation.
- Extend fixture replay DTOs to ingest `Team`, `OmitFromTeamScore`,
  `UseTeams`, `UseTeamProtection`, and `NbrForTeamScore`, and add team-result
  oracles.
- If lane parity is included, introduce explicit per-group lane assignments;
  do not treat the current competitor-array index as a lane without evidence.

Likely direct code areas include `Competition.cs`, `CompetitionEvents.cs`,
`PhaseDraw.cs`, competition commands, event aliases, competition and scoring
queries, result types, finalisation, and the corresponding tests in every
layer.

### Rough sequence and validation

| Step | Change | Validation |
|---|---|---|
| 1 | Define the exact parity contract and deliberate deviations from GliderScore quirks | Review against source findings and selected team-bearing fixtures; no production code yet |
| 2 | Add competition team settings and per-competitor assignment/omit events | Decide/fold/model-based property tests; JSON round trips; both event stores |
| 3 | Surface team roster data through competition queries | Projection and API contract tests; assignment corrections replay deterministically |
| 4 | Add generated-draw protection and diagnostics | Property: every competitor appears once; feasible same-team pairs never co-occur; fixed seed/order is deterministic; prescribed draws remain unchanged |
| 5 | Add top-N team classification | Examples for omission, partial teams, ties, and zero scores; property: selected contributors are exactly the top eligible N and input order cannot change the result |
| 6 | Add declared team results to finalisation | Re-derive-versus-declared comparison; reopen/refinalise behaviour; no mutation of individual declarations |
| 7 | Extend GliderScore replay | Compare team contributors, totals, and ranks on team-bearing fixtures while separately ledgering intentional deviations |
| 8 | Add optional sequence/lane parity if included | Acceptance tests over actual lane assignments and redraw stability, not just array order |

### Impacts, risks, and mitigations

| Impact or risk | Mitigation |
|---|---|
| The same team number conflates scoring and protection membership | Name and document it as a deliberate compatibility boundary; do not claim complete FAI semantics |
| Future separation into national and working teams becomes a data-model migration | Use a typed `GliderScoreTeamId`-style concept internally rather than a generic future-facing `TeamId`; preserve assignment events so conversion remains auditable |
| A hard protection rule can make draws impossible | Treat parity protection as a prioritised soft constraint, return diagnostics, and require explicit acceptance of violations |
| GliderScore tie rules are not uniformly current-FAI compliant | Define a data-driven parity tie policy or ledger deviations; never add `if F3J`/`if F5J` branches |
| Configurable N permits non-FAI team results | Expose it as local/compatibility configuration and do not label every result an FAI national-team classification |
| Team assignment can change after a draw | Reject changes after acceptance unless the draw is rejected/invalidated first |
| Classification changes after finalisation | Require reopen and refinalise, matching individual-result lifecycle |
| Scope expands into reports and email | Stop at report-ready HTTP data under NFR-3 |

### Salient exclusions

- No distinction between national and working teams.
- No protected relationship independent of team number.
- No event-type enforcement of mandatory or prohibited protection.
- No formal team-manager or assistant model.
- No placing-sum classification unless added only as a compatibility extension.
- No complete championship eligibility or substitution workflow.
- No complete claim of F3 compliance while multi-task F3B drawing remains
  deferred.

### Best fit

Choose this option when the primary objective is rapid familiarity for existing
GliderScore operators, fixture-based compatibility, and a small conceptual
surface, and when its known limitations are acceptable.

## Option 2: Minimal rule-spirit MVP

### Scope

This option implements the smallest model that preserves the central sporting
distinction instead of copying GliderScore's conflation.

It provides:

- competition-scoped **scoring teams**;
- independent **draw-protection groups** or equivalent protected membership;
- eligibility of each scoring-team member to contribute;
- same-group protection in generated draws where feasible;
- protection diagnostics and explicit handling of infeasibility;
- one team-classification policy: sum the best three eligible individual scores,
  then tie-break by their placing sum and best individual placing; and
- team rosters and partial/final team standings through the API.

A competitor may therefore be attached to one scoring team and independently to
one protection grouping. This covers the main national-team versus working-team
distinction and allows a defending champion to be protected with countrymen
without contributing to their team score.

For a junior/helper relationship, the MVP could create a two-person protection
group without claiming that it is a team. If requirements show that competitors
must belong to several simultaneous protection sets, the representation should
move directly to explicit protected pairs or many-to-many protection groups
rather than extending a singular field.

This option deliberately leaves applicability with the organiser. It can
execute a configured rule but does not yet determine from event type and class
whether protection is mandatory or forbidden.

### Changes required

- Add stable identities and names for scoring teams and protection groups within
  a competition.
- Add independent membership and contribution-eligibility events.
- Keep officials out of team membership at this depth.
- Freeze draw-protection membership with draw acceptance. Permit scoring-team
  corrections only through explicit events and require reopen/refinalise after
  declaration.
- Generalise `PhaseDraw` input to protected pairs or a protection lookup rather
  than teaching it about scoring teams.
- Rank candidate groups first by protection violations, then by existing repeat
  fairness measures.
- Add read-side diagnostics alongside pairwise co-occurrence.
- Add a separate team-classification engine consuming completed or partial
  individual standings plus scoring-team membership.
- Include counting members, non-counting eligible members, omission reason or
  eligibility state, placing sum, and best individual place in result output.
- Capture the declared team classification at competition finalisation.
- Extend class definitions only if the fixed MVP team method is to be declared
  by class data. An alternative is to make it an explicit competition-level
  local classification configuration, clearly not a complete FAI policy.

The option should avoid the terms "national team" and "FAI team
classification" unless the competition actually supplies that context. A
generic scoring team can also support local club or invitational team formats.

### Rough sequence and validation

| Step | Change | Validation |
|---|---|---|
| 1 | Approve glossary concepts and settle singular protection group versus many protected relationships | Domain review using national team, F3 working team, defending champion, and F5J junior/helper examples |
| 2 | Add scoring-team and protection-group registration and membership events | Decide/fold/model-based properties; duplicate membership and unknown-member rejection; both stores |
| 3 | Add competition roster/read views | Query tests prove scoring and protection memberships remain distinct and corrections replay accurately |
| 4 | Add generic protected-pair input and diagnostics to the draw | Property: feasible protected pairs do not co-occur; structural and repeat-fairness invariants remain; prescribed imports are diagnostic-only |
| 5 | Add best-three score-sum classification | Rule examples and properties for eligibility, partial teams, ties, negative/absent scores, and input-order invariance |
| 6 | Integrate partial team standings | Acceptance test with out-of-order score capture proves NFR-4: team standings derive from present individual results without gating capture or drawing |
| 7 | Capture team declarations at finalisation | Re-derivation comparison, reopening, revisions, and audit tests |
| 8 | Exercise GliderScore fixtures through an adapter | Map one GliderScore team number to both scoring and protection membership only at the compatibility boundary; compare team totals/ranks where semantics overlap |

### Explicit property invariants

The following properties should be named during story refinement:

- **Individual-score independence:** for fixed class, draw, entries, and
  rulings, changing team or protection metadata leaves every individual score
  and placing unchanged.
- **Protected separation:** if a zero-violation draw exists for the requested
  group shape, generated draws contain no protected pair in one group.
- **Structural preservation:** protection never duplicates, loses, or adds a
  competitor; every eligible competitor appears exactly once per task-round.
- **Fairness priority:** among draws with the same minimum protection-violation
  count, the existing repeat-co-occurrence objective remains the deciding
  fairness measure.
- **Contributor selection:** a team's contributors are exactly its three
  highest-scoring eligible members under the declared source classification.
- **Classification determinism:** permuting teams, members, or individual result
  input cannot change contributors, tie-break values, or team order.
- **Partial-result monotonic availability:** adding a newly available individual
  result can recompute a team standing but absence never prevents a standing
  being returned.

### Impacts, risks, and mitigations

| Impact or risk | Mitigation |
|---|---|
| More concepts than GliderScore parity | Keep only scoring team, protection grouping, membership, and contribution eligibility; defer officials, delegations, and event policy |
| Singular protection membership may be too narrow | Decide this before events land; prefer explicit protected pairs if real examples require overlapping relationships |
| Organiser can configure protection contrary to an event's rules | Label policy as organiser-supplied, expose it in competition output, and avoid a compliance claim until Option 3 |
| Fixed score-sum method omits placing-sum classification | State the supported classification method in result metadata; add policy vocabulary only when the complete option is selected |
| Same-group protection omits F3 adjacent-group preference | Return this as a declared limitation; do not describe the generated draw as fully F3 championship compliant |
| No lanes or officials | Expose team/protection roster data so consuming systems can coordinate manually; avoid fake lane semantics |
| Added objective could regress current draw fairness | Preserve the existing pairwise objective after protection count and rerun all draw properties against competitions without protection |
| Membership corrections affect two lifecycles differently | Separate protection membership events from scoring membership events and enforce their respective draw/finalisation gates |

### Salient exclusions

- No national delegation eligibility validation.
- No team manager or assistant registration.
- No sex/age eligibility facts or championship roster-size enforcement.
- No registration/model-processing substitution cutoff.
- No automatic mandatory/prohibited policy based on event type.
- No immediately-following-group protection.
- No physical lane, spot, winch, or equipment allocation.
- No placing-sum method or arbitrary class-specific source classification.
- No fly-off protection enforcement until fly-off drawing itself exists.

### Upgrade path

Option 2 is intentionally shaped to grow into Option 3:

- a scoring team can later be attached to a delegation and classified as a
  national team;
- a protection group can later gain a reason, applicability window, and
  relationship type;
- the fixed score-sum function can become one variant in a closed
  classification-policy vocabulary;
- organiser-supplied protection can later be checked against event context and
  phase policy; and
- officials can later reference the team without changing competitor identity.

### Best fit

Choose this option when Soarscore needs credible team-aware competition support
for broader club, national, and some international use, but is not yet claiming
to administer every FAI championship rule.

## Option 3: Complete rule-aware implementation

### Scope

This option treats teams as a complete competition-management capability rather
than two extra fields. It aims to represent the distinctions and applicability
conditions found in the current rules without named-class branches.

It includes:

- event context: governing body/rules, event level or type, and age/category
  context needed to resolve applicability;
- delegations or nations, formal national/scoring teams, and independent working
  teams;
- competition participants who may be competitors, team managers, assistant
  managers, or hold more than one role;
- contribution eligibility and reigning-champion-outside-team cases;
- helper nomination where it creates a protected relationship;
- auditable team membership, substitutions, and roster finalisation;
- data-driven per-phase draw-protection policy;
- same-group and immediately-following-group constraints;
- mandatory, prohibited, optional, and inapplicable protection modes;
- explicit protected pairs/groups and working-team-derived protection;
- complete infeasibility reporting and controlled overrides where rules permit;
- physical lane/spot or operational allocation only where its requirements are
  separately approved;
- both `C.15.6.2` classification methods and class-defined source results;
- partial, phase, and final team standings;
- declared team classifications, counting members, tie-breaks, officials, and
  award-facing output; and
- compatibility adapters for GliderScore's one-number representation.

"Complete" must still be bounded. It should mean complete for the team-related
rules of explicitly supported competition classes and event types, not a general
membership, accreditation, travel, finance, or medal-management platform.

### Changes required

#### Domain and lifecycle

- Introduce competition participants or officials without making every person a
  `Competitor`.
- Model a delegation separately from a team and separately from a person's club
  or nationality metadata.
- Model national/scoring team, working team, and protected relationship as
  separate concepts.
- Allow a person to be both competitor and team manager without duplicate person
  records.
- Record nomination, membership, eligibility, substitutions, and roster lock as
  immutable events.
- Define what invalidates a draw, what requires refinalisation, and which
  administrative correction remains harmless to both.
- Decide whether junior/female championship eligibility requires durable person
  facts, competition declarations, or externally validated eligibility records.
  This introduces privacy and glossary implications and must not be inferred.

#### Competition and class model

- Add competition event context needed to resolve event-level policies.
- Extend `ClassDefinition`/`PhaseDefinition` with a closed vocabulary for:
  protection applicability, protected sources, same/adjacent-group scope,
  classification source, classification method, contributor count,
  completeness ordering, and tie-break ladder.
- Keep event-specific facts in `Competition`; keep class variation in adopted
  class data.
- Recompute canonical class hashes and regenerate every seed definition and JSON
  artefact after the schema changes.
- Validate class definitions statically so contradictory or incomplete team
  policies cannot be adopted.

#### Draw

- Change draw input from bare competitors to competitors plus resolved protected
  relationships and ordered-group constraints.
- Incorporate previous/next group relationships into search state so adjacent
  protection is real, not a post-processing label.
- Resolve mandatory versus soft constraints before drawing.
- Return a structured explanation of feasibility, violations, and objective
  trade-offs for draw review.
- Apply the same resolved protection rules to late changes, manual/prescribed
  diagnostics, and reflight-filler selection where class rules require it.
- Add auditable randomisation if claiming compliance with random initial-order
  rules; preserve deterministic replay by recording the resolved random seed or
  realised order.
- Complete existing fly-off and F3B multi-task draw gaps before claiming complete
  support for those team rules.

#### Classification and finalisation

- Introduce a closed team-classification policy vocabulary rather than one
  hard-coded formula.
- Support score-sum and placing-sum methods.
- Support completeness ordering for placing-sum classification.
- Resolve the individual source classification or aggregate generically by
  phase and class policy.
- Publish contributors, excluded members, placement sums, best individual place,
  and every applied tie-break key.
- Capture team declarations and officials/award recipients alongside, but not
  inside, individual declared results.

#### Application, API, and infrastructure

- Add commands for team/delegation creation, role assignment, membership,
  nomination, substitution, protection, and roster lock.
- Add roster, draw-diagnostic, team-standing, and declared-result views.
- Extend task-round operational views only if approved lane/helper information
  is needed at score capture time.
- Register all event types and verify replay/projection parity on PostgreSQL and
  SQLite.
- Keep report rendering, email, accreditation UI, and access policy in consuming
  systems under NFR-3.

### Rough sequence and validation

| Step | Change | Validation |
|---|---|---|
| 1 | Approve terminology and complete supported-rule matrix | Rule traceability review for each supported class/event/phase; explicit exclusions recorded before coding |
| 2 | Add event context, participants, delegations, team types, roles, and membership lifecycle | Model-based aggregate tests; role overlap examples; substitution and roster-lock decision tables; both stores |
| 3 | Add class/phase policy vocabulary and validation | Every seed definition adopts and round-trips; invalid combinations rejected; hashes and canonical JSON deterministic |
| 4 | Resolve configured policy into concrete protected relationships | Table tests for mandatory/prohibited/optional/inapplicable modes, fly-off exception, working teams, and junior/helper pair |
| 5 | Extend draw search to same-group and adjacent-group constraints | Property tests over feasibility and minimal violations; actual ordered-group tests; deterministic replay/random-seed audit |
| 6 | Apply policy to draw changes and reflight placement | Acceptance tests for redraw invalidation, prescribed diagnostics, late changes, and class-required filler compatibility |
| 7 | Implement classification-policy variants | Official rule examples for both methods, partial teams, completeness ordering, phase sources, and tie ladders; property invariants per variant |
| 8 | Integrate partial standings and finalisation | NFR-4 out-of-order scenarios; declared-versus-derived team and individual results; reopen/refinalise revisions |
| 9 | Add API read models and compatibility adapter | Contract tests for organisers, CDs, competitors, and managers; GliderScore team-bearing fixture comparisons |
| 10 | Complete prerequisite draw gaps for each claimed class | End-to-end BDD workflows against both stores for F3B, F3J, F3K, F5J, F5K, and F5L only as each becomes genuinely supported |

### Explicit property invariants

Option 3 retains all Option 2 invariants and adds:

- **Policy applicability:** resolved protection is exactly the policy selected by
  governing rules, event context, phase, and relationship facts.
- **Prohibition:** when protection is prohibited, scoring-team or nationality
  membership alone cannot alter the draw.
- **Adjacency:** a feasible ordered draw contains no protected relationship in
  the prohibited same or immediately-following group positions.
- **Role separation:** officials do not become competitors or scoring
  contributors solely through their role.
- **Membership eligibility:** only formally entered, eligible team members can
  contribute; same-nationality non-members cannot.
- **Method correctness:** each classification variant selects contributors,
  orders incomplete teams, and applies tie-breaks exactly as its policy declares.
- **Audit replay:** the same event stream and adopted class definition always
  resolve the same protection set, draw diagnostics, and classifications.

### Impacts, risks, and mitigations

| Impact or risk | Mitigation |
|---|---|
| Large domain expansion | Deliver vertical slices by concept and rule evidence; reject general event-management features not required by supported team rules |
| New glossary concepts are unavoidable | Obtain explicit approval before changing glossary/class diagram; use the rule distinctions as the justification |
| Event context can become an uncontrolled rules engine | Use closed enumerations/policy variants with static validation, following the existing scoring vocabulary approach |
| Class-definition schema change touches every seed and hash | Make policy optional/inapplicable by explicit validated shape, regenerate canonical JSON once, and verify old individual scoring is byte-for-byte/decimal-for-decimal unchanged |
| Draw search complexity increases sharply | Separate hard feasibility from soft optimisation; benchmark at the stated scale of at most 20 pilots and 8 rounds/day; retain prescribed-draw escape hatch and diagnostics |
| Adjacent constraints can conflict with group fairness | Publish objective ordering and violation evidence; never silently sacrifice one criterion |
| Demographic eligibility introduces sensitive facts | Store only the minimum rule-relevant declaration, define provenance and correction, and keep access policy outside the headless core unless a concrete requirement says otherwise |
| "Complete" is interpreted as all championship administration | Define a supported-rule matrix and exclusions; no travel, fees, model processing, accreditation UI, or medal inventory without separate requirements |
| Existing deferred fly-off/F3B work expands the effort | Treat those as explicit prerequisites for class-level completeness, not hidden scope inside teams |
| Consuming clients face a much richer API | Supply purpose-built roster and standings views rather than returning the entire aggregate as the only integration surface |

### Salient exclusions unless separately approved

- Entry fees, accommodation, travel, and delegation finance.
- Jury workflow beyond identifying authorised officials.
- Protest case management.
- Model-processing workflow except a recorded roster-lock trigger if needed.
- Medal, diploma, and trophy inventory.
- Rendered reports, email delivery, badges, and UI.
- Authentication or authorisation, which remains outside the current trust model.

### Best fit

Choose this option when Soarscore intends to claim credible support for World or
Continental Championship team administration and rule-driven draws, rather than
merely offering team-aware local competition functionality.

## Comparison and decision factors

| Decision factor | Option 1: GliderScore parity | Option 2: Rule-spirit MVP | Option 3: Complete |
|---|---|---|---|
| Familiar to GliderScore operators | Highest | Moderate; adapter can present familiar fields | Moderate; richer model needs client design |
| GliderScore fixture leverage | Highest | High through adapter | High but mappings may be lossy |
| Avoids scoring/protection conflation | No | Yes | Yes |
| Same-group draw protection | Yes | Yes | Yes |
| Adjacent-group protection | No | No | Yes |
| Event/phase mandatory or prohibited policy | No | No; organiser supplied | Yes |
| Independent protected helper relationship | No | Limited through protection grouping | Yes |
| Team manager and assistant | Loose compatibility role at most | No | Yes |
| National versus working team | No | Yes at membership level | Yes with full context/lifecycle |
| Score-sum classification | Configurable top N | Best three | Policy driven |
| Placing-sum classification | No | No | Yes |
| Championship eligibility/substitution | No | No | Yes |
| Lane/spot support | Optional parity add-on | No | Only if separately justified and approved |
| Class-model impact | Low to moderate | Moderate if policy declared in class | High |
| Draw-engine impact | Moderate | Moderate | High |
| Upgrade cost to complete model | Highest | Controlled | Not applicable |
| Compliance claim | GliderScore-compatible only | Limited, configured subset | Per supported-rule matrix |

The choice should be driven by the intended claim:

- "Existing GliderScore users can run familiar team formats" points to Option 1.
- "Soarscore supports team-aware competitions without knowingly corrupting the
  main sport distinctions" points to Option 2.
- "Soarscore can administer FAI championship teams and enforce their applicable
  draw/classification rules" points to Option 3.

## Decisions needed before story refinement

The following questions should be answered after an option is selected and
before it is decomposed into stories:

1. Is compatibility semantic, or must selected GliderScore fixtures reproduce
   exact team output including quirks?
2. Must team standings be captured at finalisation, or is re-derivation alone
   acceptable? The existing definition of finalisation favours capture.
3. When perfect protection is impossible, does the system reject the generated
   draw, return the least-bad draw for explicit acceptance, or support both by
   policy?
4. Can one competitor participate in more than one simultaneous protection
   relationship? If yes, use explicit pairs or many-to-many groups from the
   outset.
5. Does the first release need physical lanes/spots, or only group membership
   and sequence? These are different facts.
6. Are team assignments editable after a draw is produced but before it is
   accepted, and does that automatically discard the candidate draw?
7. What exactly is declared at finalisation: team total and place only, or also
   contributors and tie-break evidence?
8. Which event types and classes will the product claim to support with teams?
9. Is a Team Manager a direct system user, an indirect subject of competition
   data, or only report metadata under the current trust model?
10. Is championship eligibility in scope, especially junior/female declarations
    and substitution deadlines, or will Soarscore accept organiser-certified
    membership facts?
11. Does the GliderScore adapter map one team number to both scoring and
    protection membership by default, and how are exceptions surfaced?
12. Does team configuration belong in adopted class policy, competition
    parameters, event context, or a combination? Option 3 requires a combination.

## Cross-reference and conflict check

No current requirement is directly contradicted by adding teams, provided the
implementation observes these boundaries:

- NFR-1/NFR-2 prohibit named-class branches and require varying draw and
  classification policies to be data-driven.
- NFR-3 excludes GliderScore desktop-report, badge, email, and UI parity from the
  core; only the necessary data and commands belong in Soarscore.
- NFR-4 requires partial team standings and forbids team calculations from
  imposing score-capture order.
- `docs/users.md` would need approved expansion if Team Manager becomes a system
  participant or user under Option 3.
- `docs/soaring-domain-glossary.md` and the class diagram would need approved new
  concepts under every option, especially Options 2 and 3.
- The MVP scope note in `docs/rules/00-general-rules.md` currently says team
  separation and team classification are outside MVP. Selecting an option would
  require an approved requirements/scope change; the sport-reference rules file
  itself must not be edited to fit product scope without following the repository
  documentation rules.
- Existing deferrals for fly-off draws and F3B multi-task rounds remain in force
  and limit any complete class-support claim.

One documentation issue should be resolved during later refinement: the
condensed general rule summary currently describes only score-sum team
classification, while current FAI `C.15.6.2` permits both placing-sum and
score-sum methods. The official source is authoritative. This paper does not
change the read-only rule corpus.

## Sources

Primary rule references:

- FAI CIAM General Rules 2026: `C.5.2`, `C.5.3`, `C.15.6.2`, `C.16.2.6`.
- FAI F5 Electric 2026: F5J `5.5.11.8.1(c)`.
- FAI F3 Soaring 2025: working-team and class team-classification provisions.
- NZMAA Section 5 Soaring 2024: preface variations removing national-team and
  team-manager provisions from relevant domestic classes.

Repository references:

- `docs/soaring-domain-glossary.md`
- `docs/non-functional-requirements.md`
- `docs/users.md`
- `docs/rules/00-general-rules.md`
- `docs/rules/f3-general-rules.md`
- `docs/rules/f5j.md`
- `kanban/deferred-decisions.md`
- `src/Soarscore.Domain/Competitions/Competition.cs`
- `src/Soarscore.Domain/Competitions/CompetitionEvents.cs`
- `src/Soarscore.Domain/Competitions/PhaseDraw.cs`
- `src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs`
- `src/Soarscore.Domain/Scoring/ScoringService.cs`
- `src/Soarscore.Domain/Scoring/RankingEngine.cs`
- `src/Soarscore.Application/Queries/Scoring/ScoreCompetition.cs`

GliderScore source references:

- `/home/pete/source/gliderscore/GliderScore_Master/Information_MOD.vb`
- `/home/pete/source/gliderscore/GliderScore_Master/ChoosePilots.vb`
- `/home/pete/source/gliderscore/GliderScore_Master/CreateDraw.vb`
- `/home/pete/source/gliderscore/GliderScore_Master/Rpt_Results_TeamResults_MOD.vb`
- `/home/pete/source/gliderscore/GliderScore_Master/Rpt_DrawTeams_MOD.vb`
- `/home/pete/source/gliderscore/GliderScore_Master/EmailReports.vb`
- `/home/pete/source/gliderscore/GliderScore_Master/ScoringOnLine_MOD.vb`
