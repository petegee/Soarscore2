---
name: fai-rules
description: Interpret, look up, cite or compliance-check the competition rules governing RC glider classes — the FAI Sporting Code (F3B, F3J, F3K, F3F, F5J, F5K, F5L) and the NZMAA New Zealand national rules (Class M ALES 200, Class N ALES 123, Class P ALES Radian). Use whenever work touches the draw, working times, group sizes, fly-offs, flight-time precision, landing bonuses, launch-height scoring, normalisation, round/final scoring, drop-worst, penalties or re-flights — or when checking that a design, class definition or calculation complies with the rules.
---

# Competition rules for RC soaring

`docs/rules/` is the rule knowledge base. This skill is a **router into it**, not a
copy of it. Never answer a rule question from memory — always land on a cited
source ref.

**Two rulebooks, two bodies.** `docs/rules/` is the **FAI Sporting Code** (CIAM).
`docs/rules/nz/` is the **NZMAA** *Flying Rules, Section 5: Soaring* — the New
Zealand national classes, which are **not** FAI classes and do not inherit from
the FAI general rules. Refs are written `NZ.3.12.3` to keep them apart, because
the clause numbering collides (both rulebooks have a §2.4). The skill is still
named `fai-rules` for continuity; the corpus is broader than the name.

## The corpus

```
docs/rules/
  00-general-rules.md      cross-class rules (CIAM General Rules)
  f3-general-rules.md      common to F3B, F3J, F3K
  f5-general-rules.md      common to F5J, F5K, F5L
  f3b.md f3j.md f3k.md     ─┐ per-class: AUTHORITATIVE on every number
  f5j.md f5k.md f5l.md     ─┘
  source-docs/             verbatim FAI text — read-only, section-at-a-time only

  nz/                      NZMAA — a SEPARATE rulebook, not FAI variations
    00-nz-general-rules.md   NZ cross-class (official flight, landing, ALS)
    nz-ales-general-rules.md common to Classes M, N, P
    class-m-ales200.md       ─┐ per-class: AUTHORITATIVE on every number
    class-n-ales123.md        │
    class-p-radian.md        ─┘
    source-docs/             verbatim NZMAA text — same read-only discipline
```

F3F is written in `SeedF3F.cs` from the F3 volume but has no
condensed doc yet; go to `source-docs/f3-soaring-2025.md` via the script for it.

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
   condensed doc is ambiguous, or when you need to quote the rule verbatim. The
   script routes `NZ.*` refs to the NZMAA volume automatically.

### Never `Read` a file in `source-docs/`

They are 100k+ tokens each. Use the script — it prints one section:

```bash
.claude/skills/fai-rules/scripts/fai-rule.sh show 5.5.11.12    # F5J scoring
.claude/skills/fai-rules/scripts/fai-rule.sh show F3J.10.5     # F3J landing table
.claude/skills/fai-rules/scripts/fai-rule.sh show C.16.2.6     # CIAM starting order
.claude/skills/fai-rules/scripts/fai-rule.sh show NZ.3.12.3    # NZ Class M scoring
.claude/skills/fai-rules/scripts/fai-rule.sh find "landing bonus" f3
.claude/skills/fai-rules/scripts/fai-rule.sh toc nz 3.12
.claude/skills/fai-rules/scripts/fai-rule.sh check-links
```

The volume is inferred from the ref: `F3*`→F3 Soaring 2025, `5.5.*`→F5 Electric
2026, `C.*`→CIAM General Rules 2026, `NZ.*`→NZMAA Section 5 Soaring 2024.
`show` includes sub-sections (`show 5.5.12.11` also prints `.11.1` and `.11.2`).
`check-links` covers both the FAI and the NZ condensed docs.

## Invariants — **FAI classes only**

True for every **FAI** class, so don't open a file to confirm these. **Every one
of them is false for at least one NZ class** — see the warning below.

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

> ### These do NOT hold for the NZ classes
>
> Carrying an FAI invariant into an NZ question is the likeliest way to get a
> wrong answer out of this corpus, so check the NZ class doc every time:
>
> | FAI invariant | NZ reality |
> |---|---|
> | Best raw result scores 1000 | **Classes N and P do not normalise at all** (`NZ.3.13.1 i`, `NZ.3.15.1 i`) — raw points are summed |
> | Round score = normalised group score | **Class M adds its landing bonus AFTER normalising** (`NZ.3.12.1 e`, `NZ.3.12.3 d`), so the round score is not purely a normalised value |
> | Every class has a drop-worst | **None of M, N or P states one.** All flights count |
> | Penalties deducted from the final aggregate | The NZ classes state per-round zeroes, not aggregate deductions |
> | Re-flights follow the common pattern | **N and P permit none at all**; M grants one and never says which score counts |
>
> Also: the launch-height and motor-run limits that name the ALES classes
> (200 m, 123 m, 20 s, 30 s) are enforced by onboard hardware (`NZ.2.8`) and are
> **not scoring data** — no metric, no parameter, no penalty.

## Rules for working with this corpus

- **Both `source-docs/` trees are read-only.** They track the sport, not the
  product. Never edit them, and never edit `docs/rules/` — FAI or NZ — to fit a
  software or MVP decision. `NZ.3.15.1 j` is a live example: the clause is
  self-contradictory, the class definition works around it, and the rule doc is
  left as written for the NZMAA to fix.
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
