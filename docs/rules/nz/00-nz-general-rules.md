# NZ Soaring — Generally Applicable Rules

**Top-level parent** for the New Zealand national soaring classes. Source:
*NZMAA Flying Rules, Section 5: Soaring*, March 2024.

> **This is a different rulebook, maintained by a different body.** The files in
> `docs/rules/` above this directory condense the **FAI Sporting Code**, kept by
> the CIAM. These condense the **NZMAA** rules. They are not a variation of the
> FAI rules and do not inherit from
> [`../00-general-rules.md`](../00-general-rules.md); where the NZ rules cover an
> FAI class they say so and defer (§0.0, and the PREFACE), and where they define
> a national class the NZ text is the whole rule.
>
> House-keeping rule 1 applies here exactly as it does to the FAI tree: this
> directory is derived from `source-docs/` and is **read-only to the software
> process**. It tracks the sport, not the product.

Source refs in this directory and in `seed-data/` are written `NZ.<clause>` —
`NZ.3.12.3 c` — to keep them unambiguous against FAI refs, which share the same
numeric shape.

---

## 1. Scope and the FAI classes

§2.6 lists eighteen NZ classes, A–R. §2.7 lists the FAI classes flown in NZ,
which are governed by the FAI Sporting Code with the NZ variations in the
PREFACE and the NDC formats in §0.0 — those are **not** modelled here.

Three NZ classes are currently modelled in `seed-data/`, all in the ALES
(Altitude Limited Electric Soaring) family:

| Class | Name | Doc | Definition |
|---|---|---|---|
| M | ALES 200 | [class-m-ales200.md](class-m-ales200.md) | `80-nz-m-ales200.class`, `81-nz-m-ndc.class` |
| N | ALES 123 Open | [class-n-ales123.md](class-n-ales123.md) | `83-nz-n-ales123.class` |
| P | ALES Radian | [class-p-radian.md](class-p-radian.md) | `85-nz-p-radian.class` |

Their common ground is in
[nz-ales-general-rules.md](nz-ales-general-rules.md).

---

## 2. Official flight and repeat attempts (`NZ.1.6`, `NZ.1.7`)

- **One official flight per round** unless the class says otherwise (`NZ.1.6`).
  There is an official flight once the model has left the hands of the
  competitor or helper under the pull of the launching apparatus.
- **`NZ.1.6.1` repeat attempts apply to tow-launched classes only** — "all NZ
  tow launched classes except Premier Duration" — so the whole clause, including
  its four grounds and its four notes, is **out of scope for the ALES classes**,
  which are self-launched. Each ALES class states its own re-flight position and
  two of the three state it as *none*.
- **`NZ.1.7` annulment**, with no repeat attempt: a model not conforming to the
  rules, a model losing a part during launch or flight (losing a part *on
  landing* is permitted), or a flight flown outside the frequency control system.

---

## 3. Landing (`NZ.2.4`)

Two precision-landing tables, and which one applies is decided by the class, not
by this section:

- **`NZ.2.4.4` — gliding events.** 15 m circle, 100 → 30 points over 23 rows,
  measured nose-to-centre. This is the same table as FAI F3J/F5L.
- **`NZ.2.4.5` — electric events.** 10 m circle, 50 → 5 points in ten rows,
  **"rounded to the next full metre"** (so a capture rounding of `Ceiling 1`).
  This is the same table as FAI F5J. Class M uses it; Classes N and P use their
  own three-step bonus instead.

**`NZ.2.4.6` is universal and load-bearing.** A flight is *cancelled and recorded
as a zero score* if the nose does not come to rest within **75 m** of the centre
of the competitor's designated landing spot. It applies to every class in the
document, and in the class definitions it is a `flightValidWhen` gate rather
than a landing-bonus condition — it zeroes the flight, not the bonus.

---

## 4. Contests (`NZ.2.5`)

- **`NZ.2.5.1`** — a contestants meeting no later than 15 minutes before round 1,
  to advise competitors of "any matters pertaining to the contest". This is the
  moment a CD-announced value is fixed, and it is why parameters such as Class
  M's target time bind at `BeforeFlying`.
- **`NZ.2.5.2`** — the start and finish of rounds must be clearly identified,
  preferably by an audible alarm.

---

## 5. Altitude limiters (`NZ.2.8`)

Provisional, and it **overrides `NZ.3.13.6`**.

- Every ALES model carries an **Altitude Limiter Switch (ALS)** that cuts the
  motor at the designated altitude, *and* cuts it at the class's time limit if
  that altitude has not been reached. Any brand.
- The ESC must run through the ALS in series, never direct to the receiver, and
  the connectors must be accessible so the CD can fit an ALS reader on demand.
- **`NZ.2.8.3`** — a launch exceeding the designated altitude **by more than 10 %**
  through insufficient static venting: the CD **may** assign a score of zero for
  that round. Note the discretion; see [§3 of the ALES
  parent](nz-ales-general-rules.md#3-what-is-not-scoring-data).

**Consequence for the model:** the launch-height and motor-run limits that name
these classes — 200 m, 123 m, 20 s, 30 s — are enforced in hardware and never
reach the scorer. They are not metrics, not parameters and not penalties.

---

## 6. What this rulebook does not state

Recorded because the pattern is consistent across all three modelled classes and
each gap is a `no default` parameter or an open finding rather than an oversight
on our side:

- **No tie-break, anywhere.** None of §3.12, §3.13 or §3.15 states one.
- **No group-size minimum** for the one man-on-man class (M).
- **No round count** for M outside its NDC format.
- **No normalised-score rounding precision** for M.
- **No flight-time or landing-distance capture precision** for N and P.

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.

- Definitions: [`NZ.1.1`](source-docs/nzmaa-s5-soaring-2024.md#11-definitions)
- Official flight: [`NZ.1.6`](source-docs/nzmaa-s5-soaring-2024.md#16-official-flight)
- Flight annulment: [`NZ.1.7`](source-docs/nzmaa-s5-soaring-2024.md#17-flight-annulment)
- Thermal soaring: [`NZ.2.1`](source-docs/nzmaa-s5-soaring-2024.md#21-thermal-soaring)
- Landing, gliding table: [`NZ.2.4.4`](source-docs/nzmaa-s5-soaring-2024.md#244-precision-landings-for-gliding-events)
- Landing, electric table: [`NZ.2.4.5`](source-docs/nzmaa-s5-soaring-2024.md#245-precision-landings-for-electric-events)
- The 75 m rule: [`NZ.2.4.6`](source-docs/nzmaa-s5-soaring-2024.md#246-the-flight-is-cancelled-and-recorded-as-a-zero-score-if-during-landing-the-nose-of-the-model)
- Contests: [`NZ.2.5`](source-docs/nzmaa-s5-soaring-2024.md#25-contests)
- NZ class list: [`NZ.2.6`](source-docs/nzmaa-s5-soaring-2024.md#26-nz-classes)
- Altitude limiters: [`NZ.2.8`](source-docs/nzmaa-s5-soaring-2024.md#28-altitude-limiters--provisional)
- Full document text: [nzmaa-s5-soaring-2024.md](source-docs/nzmaa-s5-soaring-2024.md)
