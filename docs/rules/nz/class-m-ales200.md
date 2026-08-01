# NZ Class M — ALES 200 (Altitude Limited Electric Soaring)

Man-on-man electric thermal duration against a **CD-announced target time**, with
the landing bonus added **after** normalisation. Inherits
[00-nz-general-rules.md](00-nz-general-rules.md) and
[nz-ales-general-rules.md](nz-ales-general-rules.md). Source refs `NZ.3.12.x`
(NZMAA Section 5: Soaring, March 2024).

Stated objective (`NZ.3.12`): "to provide a Man-On-Man (Group scored), electric
launched, thermal duration soaring event with a consistent launch altitude for
all competitors."

---

## 1. Pilot assignment to groups (the draw)

Group scored, and that is all §3.12 says about it. **No group size, minimum or
maximum, is stated anywhere**, nor a round count outside the NDC format. Both are
`no default` parameters in the class definition, bound at competition setup and
recorded in the event log.

---

## 2. Launch (`NZ.3.12.1`)

- The round opens with a **10-second launch buzzer**. Every pilot must launch
  within it; launching **before or after scores 0 for the round** (`NZ.3.12.1 h`).
- The model must leave the hand **under the pull of the electric drive motor**.
  The CD may permit a power-off launch for safety (`NZ.3.12.1 i`).
- **No wing-tip launches** — no discus, no side-arm (`NZ.3.12.1 j`).
- Motor run limited to **30 seconds or 200 m, whichever comes first**, by the ALS
  (`NZ.3.12.1 c`, and `NZ.2.8`). **No further motor activation** afterwards
  (`NZ.3.12.1 k`).
- Timing ends when the model touches the ground or any ground-based object
  (`NZ.3.12.1 l`).

---

## 3. Data the timer / helper collects

| Field | Precision / rule |
|---|---|
| **Flight time** | **Truncated** for scoring (`NZ.3.12.3 a`). |
| **Landing distance** | Nose-to-centre, on a 10 m tape marked in 1 m increments (`NZ.3.12.2 a, c`), **rounded to the next full metre** (`NZ.2.4.5`). |
| **Damage** | No landing points if the model is significantly damaged in the landing and, in the CD's opinion, is not safely flyable (`NZ.3.12.2 d`). |
| **Contact** | No landing points if the model touches the pilot or helper during the landing (`NZ.3.12.2 e`). |
| **Field boundary** | Landing beyond the field boundary set by the CD: **0 for the round**. Any part touching the boundary counts as in-bounds; **shed parts landing in-bounds do not** make the flight in-bounds (`NZ.3.12.4 a`). |
| **75 m** | Flight cancelled, zero (`NZ.2.4.6`, parent). |

**Landing-bonus table** (`NZ.3.12.2 b`, table at `NZ.2.4.5`) — the same table as
FAI F5J:

| Dist (m) | Pts | Dist (m) | Pts |
|---|---|---|---|
| 1 | 50 | 6 | 25 |
| 2 | 45 | 7 | 20 |
| 3 | 40 | 8 | 15 |
| 4 | 35 | 9 | 10 |
| 5 | 30 | 10 | 5 |
| | | over 10 | 0 |

---

## 4. The task (`NZ.3.12.1 f, g, m, n`)

- The task is a **target time announced by the CD**; **10 minutes is
  recommended**, and the CD "may choose to change the target time based on local
  conditions".
- **+1 point per second** up to and including the target time.
- **−1 point per second** for each second beyond it.

The bands are cumulative over one metric: at a 600 s target, a 700 s flight
scores `600×1 + 100×(−1)` = **500**.

This is the first class in the corpus whose band boundary is a **parameter**
rather than a rule constant — finding F27.

---

## 5. Group score (`NZ.3.12.3`)

```
normalised = own raw flight points × 1000 / highest raw flight points in the group
final      = normalised + landing points
```

**The order matters and the rule states it twice.** `NZ.3.12.1 e`: "Landing
points will be added to the normalized flight score to determine the overall
score." `NZ.3.12.3 d`: "the sum of the pilot's normalized flight score and the
landing score."

> **Contrast with FAI F5J**, which is otherwise the closest class: F5J puts the
> landing bonus *in the raw score* and normalises the sum
> ([../f5j.md](../f5j.md) §3). Same landing table, opposite order, different
> results. This is finding F24, and the worked example is in
> `competition-class-notation.md` §12.

No normalised-score rounding precision is stated.

---

## 6. Round and final score

Round score = the normalised flight score plus the landing score. Rounds are
summed; **no discard is stated**.

---

## 7. Re-flights (`NZ.3.12.5 l`)

Entitlement only: "the competitor is entitled to a re-flight (or a new attempt)
if he was hindered or aborted by an unexpected event not within his control."

Nothing about placement, group formation, or which score counts. A CD ruling is
required — and note that Classes N and P, in the same document, forbid re-flights
outright.

The CD may also **require a re-launch with a self-contained altimeter** to verify
compliance with the launch height (`NZ.3.12.5 j` — the clause is lettered `(j)`
twice in the source).

---

## 8. NDC format (`NZ.3.12.7`) — a different scoring pipeline

The National Decentralized Contest format of the same class:

- **4 rounds, each of 10 minutes.**
- Contest rules as per §3.12.1, **except** scoring.
- **"For NDC only, scoring will be the sum of the four rounds Raw Scores"** — no
  normalisation at all.
- The rule states its own maxima: flight time max 600 points plus landing max 50
  = **650 per round**; **max NDC score 2600**.

Because this changes the *pipeline* and not a number, it is modelled as a second
class definition (`81-nz-m-ndc.class`) rather than a parameter binding. The
stated 650/2600 maxima make a useful arithmetic check on any evaluator.

> Note: `NZ.3.12.7 b` cross-references "3.13.1" and "3.13.7.c" where it means
> §3.12.1 and §3.12.7 c — a stale numbering left from the January 2013 revision,
> which added Classes M and N in the opposite order to their final clause
> numbers. Harmless, but it misleads a reader following the reference.

---

## 9. Aircraft (`NZ.3.12.5`)

Any electric powered sailplane: max 150 dm² surface, max 5 kg, max 75 g/dm²
loading, rechargeable cells only, any electric motor. **No arresting devices.**
Up to three models, parts interchangeable if checked before the contest
(`NZ.3.12.4 b`). None of this is scoring data.

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.

- Event rules, task, launch: [`NZ.3.12.1`](source-docs/nzmaa-s5-soaring-2024.md#3121-event-rules)
- Landing: [`NZ.3.12.2`](source-docs/nzmaa-s5-soaring-2024.md#3122-landing)
- Scoring & normalisation: [`NZ.3.12.3`](source-docs/nzmaa-s5-soaring-2024.md#3123-scoring)
- General requirements, field boundary: [`NZ.3.12.4`](source-docs/nzmaa-s5-soaring-2024.md#3124-general-requirements)
- Aircraft definition & re-flight: [`NZ.3.12.5`](source-docs/nzmaa-s5-soaring-2024.md#3125-definition-of-electric-powered-model-glider)
- NDC format: [`NZ.3.12.7`](source-docs/nzmaa-s5-soaring-2024.md#3127-national-decentralized-contest-format-ndc)
- Electric landing table: [`NZ.2.4.5`](source-docs/nzmaa-s5-soaring-2024.md#245-precision-landings-for-electric-events)
