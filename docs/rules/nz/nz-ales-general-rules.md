# NZ ALES — Generally Applicable Rules

**Mid-level parent** for the New Zealand **ALES** (Altitude Limited Electric
Soaring) classes: **M — ALES 200**, **N — ALES 123 Open**, **P — ALES Radian**.
Inherits [00-nz-general-rules.md](00-nz-general-rules.md). Source: *NZMAA
Flying Rules, Section 5: Soaring*, **October 2024, Rev 3.0**.

> The NZMAA does not group these three as a family, so this file is a
> *distillation*, not a clause of the rulebook: everything below is stated
> separately by each class or comes from §4. Nothing here may be cited on its
> own — cite the per-class clause.

---

## 1. The shared shape

All three are self-launched electric thermal duration:

1. Launch under electric power, with an **Altitude Limiter Switch** cutting the
   motor at a fixed height or a fixed time, whichever comes first (`NZ.4.17`).
2. Glide, with **no further motor use**.
3. Score **1 point per second up to a target time, then minus 1 point per second
   beyond it** — cumulative bands over one metric, so an overfly is worth less
   than stopping on the target, never worth zero.
4. Add a **landing bonus** measured **to the nose of the model at rest**.

Where they diverge is exactly where the model has to be data-driven:

| | **M — ALES 200** | **N — ALES 123** | **P — Radian** |
|---|---|---|---|
| Target time | **CD-announced**, 10 min recommended | 6 min (360 s) | 7 min (420 s) |
| Launch height | 200 m / 30 s | 123 m / 20 s | 200 m / 30 s |
| Scoring basis | **man-on-man, normalised ×1000** | raw points | raw points |
| Landing bonus | `NZ.4.13` ten-row table, 50→5 | 50 / 25 / 0 at 7 m / 15 m | 50 / 25 / 0 at 7 m / 15 m |
| Bonus applied | **after normalising** | in the raw score | in the raw score |
| 75 m flight cancellation | **applies** (`NZ.4.13(c)`) | **not subject** | **not subject** |
| Rounds | not stated (NDC: 4) | 3 | 3 |
| Discard | none | none — "each flight counts" | none — "each flight counts" |
| Re-flights | entitled, **outcome unstated** | **none permitted** | **none permitted** |
| Aircraft | any electric glider | any | Radian or equivalent 2 m foam |

The two rows in bold in the M column are the ones the FAI corpus had never
required, and between them they account for three of the four model findings
recorded in `competition-class-notation.md` §12.

---

## 2. Data the timer / helper collects

| Field | Rule |
|---|---|
| **Flight time** | Starts as the model leaves the launcher's hand (`NZ.7.5(g)`, `NZ.7.7(e)(vi)`) or, for M, on release under motor pull (`NZ.7.4(b)(ix)`). Stops on first ground contact. M truncates for scoring (`NZ.7.4(d)(i)`); N and P state no precision. |
| **Landing distance** | Nose of the model at rest to the centre of the circle. M: `NZ.4.13`, "rounded to the next full metre". N and P: inside 7 m / inside 15 m / outside. |
| **Motor restart** | N and P (`NZ.7.5(h)`, `NZ.7.7(e)(vii)`): the timekeeper stops the watch **immediately** and the landing points are lost. M (`NZ.7.4(b)(xi)`): no further activation is permitted, with no stated consequence. |
| **Still airborne at the end of the round** | N (`NZ.7.5(k)`): time stops there and no landing points. P (`NZ.7.7(e)(x)`): **the text is defective — see [class-p-radian.md](class-p-radian.md)**. |
| **75 m** | **Class M only** (`NZ.4.13(c)`): the flight is cancelled and scores zero. N and P are not subject to it — outside their circles only the landing bonus is lost. |
| **Landing forfeits, M only** | Significant damage leaving the model unsafe to fly, or the model touching the pilot or helper (`NZ.7.4(c)(iv)`, `(v)`). |

One timer/helper per pilot, permitted to communicate with the pilot throughout
(`NZ.7.4(e)(iii)` states it for M; the practice is universal). Telemetry that helps
locate lift, and telecommunication with competitors in the field, are both
prohibited (`NZ.7.4(e)(iv)`, `(v)`).

---

## 3. What is *not* scoring data

Worth stating explicitly because the opposite is the natural assumption:

- **The launch height and motor-run limits.** 200 m, 123 m, 20 s, 30 s — the
  numbers these classes are named for — are enforced by the onboard ALS
  (`NZ.4.17(a)`) and never reach the scorer. No metric, no parameter, no penalty.
- **`NZ.4.17(c)`'s overrun zero is discretionary.** The CD *may* assign a zero
  for a launch more than 10 % over the designated altitude, and the same for
  "zooming" under `NZ.4.17(f)`. A ruling, not a predicate — the same category
  as `F3B.2.3 b`'s midair exception.
- **Battery and model swaps.** All three permit recharging or swapping batteries
  between flights, and M permits three airframes with parts interchanged if
  checked beforehand (`NZ.7.4(e)(ii)`). Nothing scored.

---

## 4. Re-flights

There is no common position, and the disagreement is inside one rulebook two
classes apart:

- **M (`NZ.7.4(f)(xii)`)** — "entitled to a re-flight (or a new attempt) if he was
  hindered or aborted by an unexpected event not within his control." Entitlement
  and nothing else: no placement, no statement of which score counts. A CD ruling
  is required.
- **N (`NZ.7.5(i)`) and P (`NZ.7.7(e)(viii)`)** — "No re-flights are permitted."

That contrast is the evidence that a rulebook *stating no re-flight* and a
rulebook *saying nothing* are different facts, which is why the model carries
both `NotPermitted` and `UndefinedRequiresRuling` (finding F26).

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.

- Class M: [`NZ.7.4`](source-docs/nzmaa-s5-soaring-2024.md#74-class-m--ales-200-altitude-limited-electric-soaring)
- Class N: [`NZ.7.5`](source-docs/nzmaa-s5-soaring-2024.md#75-class-n--ales-open-altitude-limited-electric-soaring)
- Class P: [`NZ.7.7`](source-docs/nzmaa-s5-soaring-2024.md#77-class-p--ales-radian-or-similar-2m-all-foam-electric-glider)
- Altitude limiters: [`NZ.4.17`](source-docs/nzmaa-s5-soaring-2024.md#417-altitude-limiters)
- Electric landing table: [`NZ.4.13`](source-docs/nzmaa-s5-soaring-2024.md#413-precision-landings-for-electric-events)
