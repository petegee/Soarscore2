# NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)

Three 6-minute flights, **raw points summed**, no normalisation, no re-flights.
Inherits [00-nz-general-rules.md](00-nz-general-rules.md) and
[nz-ales-general-rules.md](nz-ales-general-rules.md). Source refs `NZ.3.13.x`
(NZMAA Section 5: Soaring, March 2024).

Stated objective (`NZ.3.13`): "to fly three 6 minutes flights over 3 rounds with
a bonus for landing. Launch height is limited to 123m (400ft) and motor run time
to 20 seconds."

The whole class is eleven lettered clauses. It is the simplest definition in
`tools/Soarscore.SeedData/`, and it still found two model gaps.

---

## 1. Pilot assignment to groups (the draw)

**§3.13 never mentions groups.** The class is scored individually; the round is a
window, not a simultaneous launch, and pilots take their flight within it. There
is no man-on-man element and nothing to normalise against.

Round duration is set by the CD "taking into account the number of competitors,
weather conditions and any other pertinent factors" (`NZ.3.13.1 k`) — entirely
open, so a `no default` parameter.

---

## 2. Launch (`NZ.3.13.1 d, f, g`)

- Launch height limited to **123 m (400 ft)**, controlled by an **Altimeter
  switch placed in line with the throttle channel** (`NZ.3.13.1 d`, and `NZ.2.8`).
- Maximum motor run **20 seconds**, controlled by the onboard switch.
- Timing starts **from the moment the model leaves the launcher's hand**, and
  stops as soon as it touches the ground.
- **The motor may not be restarted.** If it is, "the timekeeper will stop the
  watch immediately and landing points will be lost" (`NZ.3.13.1 g`) — two
  distinct consequences: the flight time is truncated at the restart, *and* the
  bonus is forfeited.

No restrictions on motor, airframe or battery chemistry; batteries may be
recharged or swapped between flights (`NZ.3.13.1 a, b`).

---

## 3. Data the timer / helper collects

| Field | Precision / rule |
|---|---|
| **Flight time** | Hand-release to ground contact (`NZ.3.13.1 f`). **No precision stated.** |
| **Landing distance** | Nose of the model at rest, against two circles (`NZ.3.13.1 e`). **No capture precision stated.** |
| **Motor restarted** | Watch stopped at the restart; landing points lost (`NZ.3.13.1 g`). |
| **Still airborne at the end of the round** | Flight time stops at that point, and no landing points are awarded (`NZ.3.13.1 j`). |
| **75 m** | Flight cancelled, zero (`NZ.2.4.6`, parent). |

**Landing bonus** (`NZ.3.13.1 e`) — three steps, **not** the `NZ.2.4.5` electric
table:

| Nose at rest | Pts |
|---|---|
| inside 7 m radius | 50 |
| inside 15 m radius | 25 |
| outside 15 m | 0 |

The April 2018 revision changed this measurement to the **nose** of the model.

---

## 4. The task (`NZ.3.13.1 c`)

- **+1 point per second** flown, up to 6 minutes — **360 points**.
- **−1 point per second** flown over that time.

Cumulative over one metric: a 400 s flight scores `360×1 + 40×(−1)` = **320**.

---

## 5. Score (`NZ.3.13.1 i`)

```
round score = flight points + landing bonus
final score = sum of the three round scores
```

**There is no normalisation.** "Each flight counts. The final score is the total
of all points over three flights."

> This is finding F25. Every FAI class in the corpus normalises, so
> `Normalisation` was mandatory on a Task until this class and Class P were
> written. There is no normalisation that leaves scores unchanged — writing
> `winner 1000` to satisfy the multiplicity would have invented a rule — so the
> multiplicity was wrong, not the class.

---

## 6. Rounds

**Three rounds, all count, no discard** (`NZ.3.13`, `NZ.3.13.1 i`).

---

## 7. Re-flights (`NZ.3.13.1 h`)

**"No re-flights are permitted."** Flat, with no exceptions and no CD discretion.

> This is finding F26. It is a *definite* rule, distinguishable from a rulebook
> that is silent — Class M, in the same document, grants a re-flight and leaves
> the outcome unstated. The model carries both `NotPermitted` and
> `UndefinedRequiresRuling` because this rulebook needs both.

Note also that `NZ.1.6.1`, the general repeat-attempt clause, is scoped to "all
NZ tow launched classes" and does not reach this class in any case.

---

## 8. What is not stated

- **Tie-break** — none.
- **Flight-time and landing-distance capture precision** — none.
- **`NZ.2.8.3`'s launch-overrun zero** is discretionary; unlike Class M this
  class carries no penalty definition for it, since there is no group scoring
  here for an overrun launch to distort.

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.

- Class N: [`NZ.3.13`](source-docs/nzmaa-s5-soaring-2024.md#313-class-n-ales-123-open-altitude-limited-electric-soaring)
- Contest rules: [`NZ.3.13.1`](source-docs/nzmaa-s5-soaring-2024.md#3131-contest-rules)
- Altitude limiters: [`NZ.2.8`](source-docs/nzmaa-s5-soaring-2024.md#28-altitude-limiters--provisional)
- The 75 m rule: [`NZ.2.4.6`](source-docs/nzmaa-s5-soaring-2024.md#246-the-flight-is-cancelled-and-recorded-as-a-zero-score-if-during-landing-the-nose-of-the-model)
