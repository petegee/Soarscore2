# Compliance check

Procedure for "does this design / class definition / calculation comply with the
rules?". Follow it in order; stop and report as soon as you hit a blocker.

## 1. Scope the check

Name the **classes** and the **topics** in play. A change to scoring almost never
touches all six classes equally — F3B is structurally different (per-task
normalisation, per-task discard) and is the usual source of false "it complies"
verdicts.

If the change is class-agnostic core code, the classes in scope are **all six**, and
the real question is section 4 below.

## 2. Pull the rules

For each (class, topic): `references/rule-map.md` → the class doc section. Read only
those sections.

Do not proceed on a value read from `rule-map.md` alone — it is a routing hint that
can drift. The class doc is the statement of record.

## 3. Verify numbers against source

Every constant the change introduces or relies on — a threshold, a cap, a points
rate, a table entry, a group minimum — gets traced to the verbatim rule:

```bash
.claude/skills/fai-rules/scripts/fai-rule.sh show <ref>
```

Record the ref. A constant you cannot trace to a ref is a finding, not a detail.

## 4. Check it against the architectural law

CLAUDE.md: the core system must not know about any specific competition class.

- Does honouring this rule require a branch, a flag or a lookup keyed on class
  identity **outside that class's own definition**? If yes → the design is wrong.
  Report it; do not code around it.
- Is the rule fact expressed as **data in the Competition Class definition**?
- Would **adding a seventh class** require touching this code? If yes → the core has
  leaked a class-specific assumption.
- Watch the shape assumptions in particular: that a round has one task; that a round
  produces one group score; that the drop-worst unit is a round; that a landing bonus
  exists; that flight time is the only scored quantity. **F3B, F3K, F5K and F5J each
  break at least one of those.**

## 5. Check it against the rest of the corpus

Per CLAUDE.md, a new or changed requirement is cross-referenced before it lands.
Check `docs/soaring-domain-glossary.md` (no new concepts),
`docs/non-functional-requirements.md`, and the other class docs for anything the
change contradicts or duplicates.

## 6. Report

For each finding: **what the rule says** (with ref) → **what the change does** →
**the gap**.

Classify each as:

- **Contravenes a rule** — the rules are explicit and the change is wrong. Blocker.
- **Rules are silent** — no rule governs it (F5L re-flight placement, F5K minimum
  rounds). Not a defect. State the silence, treat the behaviour as a Contest Director
  decision, and make sure it is recorded as a decision rather than hardcoded as if it
  were a rule. **Never fill the gap from another class.**
- **Architectural leak** — rule-correct but expressed as core branching (section 4).
- **Conflicts with an existing doc** — surface it with a proposed resolution and
  **ask before changing anything**.

Do not edit `docs/rules/` or `docs/rules/source-docs/` as part of a compliance fix.
If a rule doc looks wrong, that is a finding to raise with the user.
