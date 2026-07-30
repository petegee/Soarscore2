---
name: fai-rules
description: Interpret, look up, cite or compliance-check the FAI Sporting Code rules governing RC glider competition classes (F3B, F3J, F3K, F5J, F5K, F5L). Use whenever work touches the draw, working times, group sizes, fly-offs, flight-time precision, landing bonuses, launch-height scoring, normalisation, round/final scoring, drop-worst, penalties or re-flights — or when checking that a design, class definition or calculation complies with the rules.
---

# FAI rules for RC soaring

`docs/rules/` is the rule knowledge base. This skill is a **router into it**, not a
copy of it. Never answer a rule question from memory — always land on a cited
source ref.

## The corpus

```
docs/rules/
  00-general-rules.md      cross-class rules (CIAM General Rules)
  f3-general-rules.md      common to F3B, F3J, F3K
  f5-general-rules.md      common to F5J, F5K, F5L
  f3b.md f3j.md f3k.md     ─┐ per-class: AUTHORITATIVE on every number
  f5j.md f5k.md f5l.md     ─┘
  source-docs/             verbatim FAI text — read-only, section-at-a-time only
```

## Retrieval ladder — stop at the first rung that answers the question

1. **`references/rule-map.md`** — topic × class matrix. Answers "which class does
   what" and "which file/section owns this" in one read. Start here for anything
   comparative or when you don't know where a rule lives.
2. **The class doc** (`f5j.md` etc.) — the authoritative statement. **Read only the
   class doc.** The parents are inheritance context, not a prerequisite; open a
   parent only when the class doc explicitly defers to it ("as in the parent",
   "common draw rules per parents").
3. **`00-general-rules.md` / family doc** — only for genuinely cross-class questions
   (the draw framework, team classification, the common re-flight pattern).
4. **`source-docs/` via the script** — when a number must land in code, when the
   condensed doc is ambiguous, or when you need to quote the rule verbatim.

### Never `Read` a file in `source-docs/`

They are 100k+ tokens each. Use the script — it prints one section:

```bash
.claude/skills/fai-rules/scripts/fai-rule.sh show 5.5.11.12    # F5J scoring
.claude/skills/fai-rules/scripts/fai-rule.sh show F3J.10.5     # F3J landing table
.claude/skills/fai-rules/scripts/fai-rule.sh show C.16.2.6     # CIAM starting order
.claude/skills/fai-rules/scripts/fai-rule.sh find "landing bonus" f3
.claude/skills/fai-rules/scripts/fai-rule.sh toc f5 5.5.11
.claude/skills/fai-rules/scripts/fai-rule.sh check-links
```

The volume is inferred from the ref: `F3*`→F3 Soaring 2025, `5.5.*`→F5 Electric
2026, `C.*`→CIAM General Rules 2026. `show` includes sub-sections
(`show 5.5.12.11` also prints `.11.1` and `.11.2`).

## Universal invariants

True for every class — don't open a file to confirm these:

- Within a group the **best raw result scores 1000**; everyone else is
  `own / winner × 1000`. Inverted (`winner / own`) where lower is better (F3B speed).
- **Round score = the normalised group score** — *except F3B*, where a round is the
  sum of three separately-normalised per-task partials.
- **Aggregate = sum of round scores**, minus a drop-worst. Every class has a
  drop-worst; the threshold and the unit differ (F3B drops per *task*, not per round).
- **Penalties are deducted from the final aggregate and survive a dropped round.**
- A raw score that would go negative is recorded as **zero**; penalties still stand.

Everything else — times, precisions, group sizes, tables, thresholds — is
class-specific. Assume nothing carries across; check the map.

## Rules for working with this corpus

- **`source-docs/` is read-only.** It tracks the sport, not the product. Never edit
  it, and never edit `docs/rules/` to fit a software or MVP decision.
- **Don't edit `docs/rules/` without asking.** Per CLAUDE.md, agents ask before
  changing anything under `docs/`. That includes correcting a rule doc you believe
  is wrong — surface it instead.
- **A rule you cannot find is a question, not an inference.** Where the rules are
  silent (e.g. F5L states no re-flight placement priorities, F5K states no
  minimum-round validity), say so and treat it as a Contest Director decision —
  do not borrow another class's rule to fill the gap.
- **No new domain concepts.** If a rule seems to need a concept absent from
  `docs/soaring-domain-glossary.md`, stop and surface it.
- **Flag conflicts, don't reconcile them.** If a rule contradicts an existing
  requirement or another doc, report it with a proposed resolution and ask.

## Rules → architecture

Per CLAUDE.md's core architectural law, a rule fact belongs in the **Competition
Class definition as data**, never as a branch in core code.

Use the map's class columns as the shape test: if a row varies across classes, it is
a **field of the class model**. If honouring a rule appears to need `if class ==
"F3B"` in the core, stop — surface it rather than coding around it. (F3B is the
usual trigger: per-task normalisation and per-task discard mean "round" is not
uniformly the scoring unit.)

**Cite what you encode.** Any rule-derived constant that lands in code or a class
definition carries its source ref in a comment — `# F5J 5.5.11.12 e` — so the next
edition bump is traceable.

## Auditing a change for compliance

For "does this comply", see `references/compliance-check.md`.
