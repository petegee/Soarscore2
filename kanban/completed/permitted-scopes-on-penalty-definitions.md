# Story — Permitted scopes on PenaltyDefinition (adoption-time scope hardening)

**Status:** Completed 2026-08-31 — all WIs landed as planned; WI-0 answer
GRANTED (both mirror doc edits). · **Raised:** 2026-08-30 — WI-6 of
`kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md`,
spun out at that story's closeout by user approval.
**Scoped:** 2026-08-31 — plan below is implementation-ready; decisions D-1..D-5
settled against the tree as it stands; every file/line cite re-verified 2026-08-31.

## What

Let a `PenaltyDefinition` optionally declare the scopes it may be recorded at:
an optional `PenaltyScope[]? PermittedScopes` field (null ⇒ any scope, so every
existing class definition is untouched). The write side enforces it —
`Competition.RecordPenalty` and `Entry.RecordPenalty` gain a check after the
infraction-type lookup that refuses a record whose scope is not permitted,
defect `recordPenalty.scopeNotAllowed` — plus an adoption check and unit tests,
and the glossary / class-diagram `PenaltyDefinition` note.

## Why it matters

A definition today may be recorded at any scope the write side allows, and the
parent story proved scope+effect *combinations* cannot be judged at adoption
because the definition carries no scope knowledge. This field lets a class
author say "this infraction is a flight-level fact" and get
`recordPenalty.scopeNotAllowed` at recording time instead of discovering at
score time that the record landed where its effects cannot act. It is the
adoption-time mirror of that story's D-A3 record-time completeness check, and
it is data-driven (NFR-1) — the core system reads the list generically, never
branching on a class.

## Before starting

- **/docs approval: GRANTED 2026-08-30** (housekeeping rule 4) — the user
  approved exactly the argument in the parent story's WI-6, which is the basis
  of this story: **the glossary and class-diagram `PenaltyDefinition` notes**.
  Two *further* /docs files are forced as mirrors by the same change and are
  **not** covered by that approval as recorded — see WI-0's single gate:
  `docs/competition-class-notation.md` (every existing `PenaltyDefinition`
  field has a notation keyword; a field without one breaks the notation doc's
  isomorphism claim) and `docs/high-level-architecture.md` (the numbered
  adoption-check inventory, lines ~216-244, which `ClassDefinitionValidation.cs`
  cites as canonical — a check 20 in code with no inventory line makes the
  inventory a lie).
- Green-field: no events to preserve, no migration (CLAUDE.md project status).
- Scope discipline: one optional field, two decide-function checks, one
  adoption check, unit tests. No engine changes — the read path already routes
  correctly (D-A1/D-A2 of the parent story); this only refuses bad records
  earlier.
- **Cross-references checked (housekeeping rule 2) — no conflicts:**
  - NFR-1/NFR-2: the field lives on the class model; both checks read it
    generically; extension is additive-only (optional, default null). Compliant.
  - NFR-4: this is record-time validation constraining *payload correctness*,
    not *when* a record may be made — the identical argument that cleared the
    parent story's D-A3. Penalties stay recordable at any time.
  - `deferred-decisions.md`, `tech-debt.md`: nothing owed by or conflicting
    with this story. The "task-round coordinate read by nothing" bullet was
    closed by the parent story; unaffected here.
  - Rule docs: no rule text is touched; the field is authoring metadata, not a
    new sporting rule. No zeroing or scoring rule changes.
  - Glossary: no new concept — `permittedScopes` is a field on the existing
    `PenaltyDefinition` concept; the glossary's Penalty section gains one
    sentence (draft below). No glossary approval beyond what is recorded.

## Design decisions (settled here, cited from code)

### D-1 — Field shape: `PenaltyScope[]?`, exactly as approved

```csharp
/// <summary>…</summary>
public PenaltyScope[]? PermittedScopes { get; init; }
```

on `PenaltyDefinition`
(`src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs:74-91`).
`PenaltyScope` is the shared enum at `src/Soarscore.Domain/Shared.cs:69`
(Flight, Entry, TaskRound, Competition). Semantics:

- **null (absent)** ⇒ any scope the write side allows. Every existing class
  definition and every seed stays untouched; the canonical JSON omits a null
  (`ClassDefinitionHashing.cs:49`, `JsonIgnoreCondition.WhenWritingNull`), so
  **all seed content hashes are byte-for-byte unchanged** — pinned by the
  existing `ClassDefinitionHashingTests` and `SeedCorpusIngestionTests`, which
  must stay green unmodified.
- **populated** ⇒ `penalty.Scope` must be in the list; otherwise refused.
- The ImmutableArray convention for class-model collections is deliberately
  not followed: null must mean *unset* (unrestricted), and a nullable array
  states that directly. This is also the exact type the parent story's
  approved WI-6 argument named.

Wire form: `"permittedScopes": ["flight", "entry"]` (camelCase policy +
`JsonStringEnumConverter`, same as every other enum in the definition).

### D-2 — Check placement and precedence: scope refusal outranks payload completeness

Both checks need the definition, so they sit **immediately after the
infraction-type lookup** in each chain, sharing that lookup — no re-scan
(the `FindPenaltyDefinition` helper precedent,
`Competition.cs:1745-1758`).

- `Competition.RecordPenalty` (`Competition.cs:1684-1697`) chain order becomes:
  wrongScope → competitorRequired → competitorExists → taskRoundNotFound →
  **infractionTypeNotDeclared → scopeNotAllowed (new)** → zeroEffectRequiresTaskRound
  → byBlank. Rationale: a record whose scope the definition forbids should be
  told *that* before being quibbled about payload completeness; and
  scopeNotAllowed must precede zeroEffectRequiresTaskRound so a mis-scoped
  Zero* record reports the scope, not the missing coordinate.
- `Entry.RecordPenalty` (`Entry.cs:514-552`): after the infraction-type block
  (`Entry.cs:537-542`), before the By check. The aggregate-pairing checks
  (annulled, wrongScope, subjectNotAllowed) stay first — a Competition-scoped
  record against an Entry still reports `recordPenalty.wrongScope`, not
  `scopeNotAllowed`.
- Defect code `recordPenalty.scopeNotAllowed` on both aggregates, matching the
  existing `recordPenalty.*` family. Competition half carries pointer
  `$.scope` (Defect style of its chain); Entry half uses the plain
  `Failure(code, message)` two-arg style of its chain.

### D-3 — Adoption check 20 rejects only the empty list; no effect×scope cross-check

New check 20 in `ClassDefinitionValidation.cs` (the file header's count and
the `Validate()` call list at `ClassDefinitionValidation.cs:1-2, 49-73` both
grow): **a populated `PermittedScopes` must not be empty.** An infraction no
scope permits can never be recorded — provably inert, the exact check-19
precedent (`ClassDefinitionValidation.cs:500-521`). Codes:

- `class-definition.check-20.permitted-scopes-empty`, pointer
  `$.penalties[i].permittedScopes`.

Deliberately **not** checked:

- **Duplicates** in the list — harmless (membership test), no inertness, no
  misbehaviour; adding a canonical-dedupe would be scope creep.
- **Effect×scope combinations** — post parent story, every scope can host
  every effect: aggregate-scoped Zero* routes to the task-round stage
  (parent D-A1/D-A2) and entry-scoped Disqualify flags (parent D-B1/D-B2). A
  cross-check would re-invent the scope policy the parent story's option-1
  ruling explicitly declined. If a definition is permitted only at scopes
  where one aggregate cannot accept it (e.g. permitted only `[flight]`), it is
  simply recordable at the other aggregate — not inert.

### D-4 — Read path untouched; engine, views, handlers: zero edits

`PenaltyEngine`, `ScoringService`, `NormalisationEngine`, `ScoreTaskRound`,
and both handlers (`RecordCompetitionPenalty.cs`, `RecordEntryPenalty.cs` —
they propagate decide failures verbatim) take no change. The defect flows out
through the existing `Result` → handler → route plumbing. No API surface
change beyond the new defect code. No acceptance-test scenario: this is a
record-time validation refusal with no user-facing workflow of its own — the
same coverage decision as `recordPenalty.zeroEffectRequiresTaskRound`
(unit tests only).

### D-5 — Seeds and fixtures untouched

No seed class sets the field; no seed or fixture records any newly-refused
shape (they record none today at all — the parent story's D5 guarantee). The
seed corpus JSON in `tools/Soarscore.SeedData/json/` is regenerated identical
(null omitted) — verify, don't assume: `git status` must show no diff in
`tools/Soarscore.SeedData/json/` after the checkpoints.

## Work items

WIs are sequential (WI-2 reads the field WI-1 adds). Each WI lands compiling
with its checkpoint green. Code cites work items as
`kanban/completed/permitted-scopes-on-penalty-definitions.md#wi-n`.
Each WI is self-contained enough to hand to a sub-agent on its own.

### WI-0 — Board, and the one /docs gate

1. `git mv` this story to `in-progress/`, status header updated in the same
   commit.
2. **Single user question** (housekeeping rule 4): the 2026-08-30 approval
   covers the glossary and class-diagram notes. Does it extend to the two
   mirror edits the field forces?
   - (a) a short `recordableAt` paragraph in `docs/competition-class-notation.md`
     §3's penalty block (draft wording in WI-1's notes below);
   - (b) the check-20 line in `docs/high-level-architecture.md`'s
     adoption-check inventory (~lines 216-244).
   Record the answer here. **Fallback if declined/unanswered by the time the
   dependent WI lands:** the affected WI omits the gated edit and records the
   gap in `kanban/deferred-decisions.md` (one bullet: field landed, mirror
   doc edit not granted, re-ask) — never a silent third-doc edit, and never a
   self-inconsistent inventory *silently* left stale (the deferred-decisions
   bullet is the loud record).
3. No code.

Checkpoint: story in `in-progress/`; the WI-0 answer recorded below this line
before any code lands.

**WI-0 answer:** GRANTED 2026-08-31 — the user extended the 2026-08-30
approval to both mirror edits: (a) the `recordableAt` paragraph in
`docs/competition-class-notation.md` §3 (WI-3) and (b) the check-20 line in
`docs/high-level-architecture.md`'s adoption-check inventory (WI-2).

### WI-1 — Domain: the field, two decide checks, approved /docs notes, decide tests

**Code:**

1. `src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs` —
   `PenaltyDefinition` gains, after `Effects`:

   ```csharp
   /// <summary>
   /// OPTIONAL: the scopes a record of this infraction may carry. Null (absent)
   /// means any scope the write side allows — every definition that omits it is
   /// unchanged. Populated, the write side (RecordPenalty on both aggregates)
   /// refuses any scope not listed with recordPenalty.scopeNotAllowed; the
   /// adoption pipeline rejects an empty list (check 20) because it could never
   /// be recorded. Read generically, never branched per class (NFR-1).
   /// kanban/completed/permitted-scopes-on-penalty-definitions.md#wi-1.
   /// </summary>
   public PenaltyScope[]? PermittedScopes { get; init; }
   ```

2. `src/Soarscore.Domain/Competitions/Competition.cs` — new private check,
   wired into `RecordPenalty`'s `??` chain between `ValidateInfractionType`
   and `ValidateZeroEffectHasTaskRound` (D-2), sharing `FindPenaltyDefinition`
   (`Competition.cs:1751`):

   ```csharp
   private Defect? ValidatePermittedScopes(Penalty penalty) =>
       FindPenaltyDefinition(penalty.InfractionType) is { } def
       && def.PermittedScopes is { } permitted
       && !permitted.Contains(penalty.Scope)
           ? new Defect("recordPenalty.scopeNotAllowed", "$.scope",
               $"'{penalty.InfractionType}' declares permitted scopes "
               + $"[{string.Join(", ", permitted)}]; {penalty.Scope} is not one of them.")
           : null;
   ```

3. `src/Soarscore.Domain/Entries/Entry.cs` — in `RecordPenalty`
   (`Entry.cs:514-552`), after the infraction-type block
   (`Entry.cs:537-542`), before the By check:

   ```csharp
   if (penaltyDefinitions.FirstOrDefault(d => d.InfractionType == penalty.InfractionType)
       is { } definition && definition.PermittedScopes is { } permitted
       && !permitted.Contains(penalty.Scope))
   {
       return Result<PenaltyRecorded>.Failure(
           "recordPenalty.scopeNotAllowed",
           $"'{penalty.InfractionType}' declares permitted scopes "
           + $"[{string.Join(", ", permitted)}]; {penalty.Scope} is not one of them.");
   }
   ```

   (First-match-wins lookup, the same discipline
   `PenaltyEngine.BuildDefinitionLookup` uses; comment cites the story WI and
   D-2's precedence rationale.)

**/docs — approved, land in the same commit as the field:**

4. `docs/soaring-domain-glossary.md`, end of the Penalty section (after the
   line-71 paragraph), one sentence:

   > A class states what each penalty it defines costs, and may also state the
   > scope or scopes at which that infraction may be recorded — declaring it, say,
   > a flight-level fact — and the system then refuses to record it anywhere else.
   > A penalty the class does not restrict may be recorded at any scope.

5. `docs/soaring-domain-class-diagram.md`, `PenaltyDefinition` block
   (~line 510): add attribute `+PenaltyScope[] permittedScopes` and extend the
   `%%` note beneath the block:

   > %% permittedScopes is optional and nullable: absent means the infraction
   > %% may be recorded at any scope the write side allows — every seed
   > %% definition omits it, and a null is omitted from the canonical JSON, so
   > %% seed content hashes are unchanged. Populated, it lists the only scopes a
   > %% record may carry; the write side refuses any other with
   > %% recordPenalty.scopeNotAllowed. An EMPTY list is rejected at adoption
   > %% (check 20): an infraction no scope permits can never be recorded — the
   > %% check-19 precedent. There is deliberately no effect×scope cross-check:
   > %% since D-A1/D-B1 of
   > %% kanban/completed/aggregated-scoped-zero-effects-and-entry-scoped-disqualify-no-op.md
   > %% every scope can host every effect, so refusing combinations would
   > %% re-invent the scope policy that story declined.

**Tests** (`tests/Soarscore.Domain.Tests/`):

6. `RecordCompetitionPenaltyDecideTests.cs` — the file's `SeedCompetitionWith`
   helper (`:67`) takes a custom definition; add a local helper building a
   minimal one-penalty `ClassDefinition` (mirror the existing test fixtures'
   shape) with `PermittedScopes` set. Facts:
   - definition permitting only `[PenaltyScope.TaskRound]`, record at
     `Competition` scope → `recordPenalty.scopeNotAllowed`. (Note: a
     definition permitting only Flight/Entry is unreachable here —
     `ValidatePenaltyScope`'s `wrongScope` fires first; test the reachable
     subset.)
   - definition permitting `[Competition]`, record at `Competition` scope →
     success (payload round-trips, existing happy-path assertions).
   - precedence pin (D-2): definition permitting only `[TaskRound]` **and**
     carrying a Zero effect, recorded at `Competition` scope with no
     coordinate → `recordPenalty.scopeNotAllowed` (not
     `zeroEffectRequiresTaskRound`).
   - property **P-ScopeGate** (CsCheck, style of the file's existing P3
     generator): over `Gen.OneOfConst` of the two aggregate-legal scopes and
     permitted sets {null, [TaskRound], [Competition], [TaskRound, Competition]},
     a record succeeds **iff** the definition's `PermittedScopes` is null or
     contains the recorded scope.
7. `RecordEntryPenaltyDecideTests.cs` — add a definitions array alongside
   `SamplePenaltyDefinitions` (`:28`) with one definition carrying
   `PermittedScopes = [PenaltyScope.Flight]`. Facts:
   - record at `Flight` → success;
   - record at `Entry` → `recordPenalty.scopeNotAllowed`;
   - property **P-ScopeGate** (Entry half): over scopes {Flight, Entry} ×
     permitted sets {null, [Flight], [Entry], [Flight, Entry]}, same iff rule.

Existing tests are the regression guard: every seed definition leaves
`PermittedScopes` null, so all current decide tests stay green unmodified —
that is precisely the null ⇒ any-scope guarantee, pinned by the corpus.

Checkpoint: `dotnet build Soarscore.sln`; `dotnet test
tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests
tests/Soarscore.Architecture.Tests` — all green, with only the new tests
added. `git status` shows no diff under `tools/Soarscore.SeedData/json/`.

### WI-2 — Application: adoption check 20 (+ gated inventory line)

1. `src/Soarscore.Application/Commands/CompetitionClasses/ClassDefinitionValidation.cs`:
   - header comment line 1: "nineteen" → "twenty" (with this story's citation
     added, matching how checks 17-19 annotated theirs);
   - `Validate()` (`:49-73`) calls the new check after
     `CheckExclusionGroupsAreDeductOnly`;
   - new method in the check-16 style (`:424-447`):

   ```csharp
   /// <summary>
   /// Check 20 — a PenaltyDefinition.permittedScopes that is present must not
   /// be empty (diagram §2): an infraction no scope permits can never be
   /// recorded, so the declaration is provably inert — the check-19 precedent.
   /// Absent (null) is the unrestricted default and needs nothing.
   /// </summary>
   private static void CheckPermittedScopesNotEmpty(ClassDefinition definition, List<Defect> defects)
   {
       for (var i = 0; i < definition.Penalties.Length; i++)
       {
           if (definition.Penalties[i].PermittedScopes is { Length: 0 })
           {
               defects.Add(new Defect("class-definition.check-20.permitted-scopes-empty",
                   $"$.penalties[{i}].permittedScopes",
                   $"Penalty '{definition.Penalties[i].InfractionType}' permits no scope, so it could never be recorded."));
           }
       }
   }
   ```

2. **Gated on WI-0 (b):** `docs/high-level-architecture.md`, adoption-check
   inventory after entry 19 (~line 244):

   > 20. A `PenaltyDefinition.permittedScopes` that is present but empty is
   >     rejected — diagram §2. An infraction no scope permits can never be
   >     recorded, so the declaration is provably inert (the check-19
   >     precedent). Absent, the infraction is recordable at any scope.

   If WI-0 declined, skip and record in `deferred-decisions.md` instead.

**Tests** (`tests/Soarscore.Application.Tests/Commands/CompetitionClasses/ClassDefinitionValidationTests.cs`,
pattern-matching the check-16 tests there):

3. populated `PermittedScopes` → no check-20 defects;
4. empty `PermittedScopes` → exactly the
   `class-definition.check-20.permitted-scopes-empty` defect at
   `$.penalties[0].permittedScopes`;
5. absent (null) → no defects (already pinned by every existing test; one
   explicit null assertion keeps the matrix complete).
6. No property test is added for check 20: the invariant is a one-field
   sanity rule exhaustively covered by the null/empty/populated examples —
   the story's named planning-time invariant is P-ScopeGate (WI-1).

Checkpoint: `dotnet build Soarscore.sln`; `dotnet test
tests/Soarscore.Application.Tests tests/Soarscore.Architecture.Tests` — green
with only new tests; `ClassDefinitionHashingTests` and
`SeedCorpusIngestionTests` green unmodified (seed hashes unchanged, D-5).

### WI-3 — Gated notation paragraph (only if WI-0 (a) granted)

`docs/competition-class-notation.md`, in the §3 penalty block after the
`perOccurrence` paragraph (~line 216), insert:

> **`recordableAt`** sets `PenaltyDefinition.permittedScopes` — the scopes at
> which the infraction may be recorded:
>
> ```
>   penalty "launchInfraction" recordableAt "flight" deduct 100
> ```
>
> The keyword takes one or more scope names (`"flight"`, `"entry"`,
> `"taskRound"`, `"competition"`). Absent — every definition in the corpus —
> the infraction may be recorded at any scope the write side allows. An empty
> list is rejected at adoption (check 20): an infraction nothing can record is
> a dead rule, and dead rules belong in the rulebook, not the data.

Also confirm no F-table row is owed: permittedScopes is authoring metadata,
not a rulebook finding, so it takes no `F` number.

If WI-0 declined: skip, and confirm the deferred-decisions bullet from WI-1/2
covers it. (This WI exists separately so the gated doc edit never blocks the
code; it may be done any time after WI-0.)

Checkpoint: nothing to run beyond a build; diff touches only the one doc.

### WI-4 — Closeout

1. `git mv` to `completed/`, set the status header, fill in the WI-0 answer
   if still pending.
2. Reconcile `kanban/tech-debt.md` (expect: nothing owed) and
   `kanban/deferred-decisions.md` (only if a gated doc edit was declined).
3. `graphify update .`.

## Out of scope

- Any seed class or fixture setting `PermittedScopes` (D-5 — the corpus is
  untouched; if a class author later wants F3B's `safetyPlaneCrossing`
  restricted, that is an authored change to that class, not this story).
- Any engine, view, normalisation, or read-path change (D-4).
- Any API shape change beyond the new defect code; both handlers need zero
  edits (verified: `RecordCompetitionPenalty.cs`, `RecordEntryPenalty.cs` —
  decide failures propagate through the existing `Result` plumbing).
- Acceptance/Gherkin scenarios (D-4: record-time validation refusal, unit
  coverage only — the `zeroEffectRequiresTaskRound` precedent).
- Duplicate-scope or effect×scope adoption checks (D-3).
- Event shapes, store aliases, migration — none exist to make (green-field).
