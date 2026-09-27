# NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)

Three 6-minute flights, **raw points summed**, no normalisation, no re-flights.
Inherits [00-nz-general-rules.md](00-nz-general-rules.md) and
[nz-ales-general-rules.md](nz-ales-general-rules.md). Source refs `NZ.7.5(...)`
(NZMAA Section 5: Soaring, **October 2024 Rev 3.0**; general clauses cited as
`NZ.3.x` / `NZ.4.x`).

Stated objective (`NZ.7.5(a)`): "to fly three 6 minutes flights over 3 rounds with
a bonus for landing. Launch height is limited to 123m (400ft) and motor run time
to 20 seconds."

The whole class is twelve lettered clauses. It is the simplest definition in
`tools/Soarscore.SeedData/`, and it still found two model gaps.

---

## 1. Pilot assignment to groups (the draw)

**§7.5 never mentions groups.** The class is scored individually; the round is a
window, not a simultaneous launch, and pilots take their flight within it. There
is no man-on-man element and nothing to normalise against.

Round duration is set by the CD "taking into account the number of competitors,
weather conditions and any other pertinent factors" (`NZ.7.5(l)`) — entirely
open, so a `no default` parameter.

---

## 2. Launch (`NZ.7.5(e)`, `(g)`, `(h)`)

- Launch height limited to **123 m (400 ft)**, controlled by an **Altimeter
  switch placed in line with the throttle channel** (`NZ.7.5(e)`, and `NZ.4.17`
  — the clause's own "See 2.8" is a stale March-edition cross-reference).
- Maximum motor run **20 seconds**, controlled by the onboard switch.
- Timing starts **from the moment the model leaves the launcher's hand**, and
  stops as soon as it touches the ground (`NZ.7.5(g)`).
- **The motor may not be restarted.** If it is, "the timekeeper will stop the
  watch immediately and landing points will be lost" (`NZ.7.5(h)`) — two
  distinct consequences: the flight time is truncated at the restart, *and* the
  bonus is forfeited.

No restrictions on motor, airframe or battery chemistry; batteries may be
recharged or swapped between flights (`NZ.7.5(b)`, `(c)`).

---

## 3. Data the timer / helper collects

| Field | Precision / rule |
|---|---|
| **Flight time** | Hand-release to ground contact (`NZ.7.5(g)`). **No precision stated.** |
| **Landing distance** | Nose of the model at rest, against two circles (`NZ.7.5(f)`). **No capture precision stated.** |
| **Motor restarted** | Watch stopped at the restart; landing points lost (`NZ.7.5(h)`). |
| **Still airborne at the end of the round** | Flight time stops at that point, and no landing points are awarded (`NZ.7.5(k)`). |
| **75 m** | **Not subject.** The 75 m flight cancellation is `NZ.4.13(c)`, scoped to classes that adopt the electric precision-landing table (see [00-nz-general-rules.md §3](00-nz-general-rules.md#3-landing-nz411nz413)); this class uses its own three-step bonus and never references that table. Outside 15 m the bonus is zero; the flight stands. (The superseded March 2024 edition carried the rule as a general clause and was read as reaching this class.) |

**Landing bonus** (`NZ.7.5(f)`) — three steps, **not** the `NZ.4.13` electric
table:

| Nose at rest | Pts |
|---|---|
| inside 7 m radius | 50 |
| inside 15 m radius | 25 |
| outside 15 m | 0 |

The April 2018 revision changed this measurement to the **nose** of the model
(a change carried in the Rev 3.0 revision history as "3.13.1 (e) Class N Change
Landing measurement", under the then-current numbering).

---

## 4. The task (`NZ.7.5(d)`)

- **+1 point per second** flown, up to 6 minutes — **360 points**.
- **−1 point per second** flown over that time.

Cumulative over one metric: a 400 s flight scores `360×1 + 40×(−1)` = **320**.

---

## 5. Score (`NZ.7.5(j)`)

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

**Three rounds, all count, no discard** (`NZ.7.5(a)`, `NZ.7.5(j)`).

---

## 7. Re-flights (`NZ.7.5(i)`)

**"No re-flights are permitted."** Flat, with no exceptions and no CD discretion.

> This is finding F26. It is a *definite* rule, distinguishable from a rulebook
> that is silent — Class M, in the same document, grants a re-flight and leaves
> the outcome unstated. The model carries both `NotPermitted` and
> `UndefinedRequiresRuling` because this rulebook needs both.

Note also that `NZ.3.6(b)`, the general repeat-attempt clause, is scoped to "all
NZ tow launched classes" and does not reach this class in any case.

---

## 8. What is not stated

- **Tie-break** — none.
- **Flight-time and landing-distance capture precision** — none.
- **`NZ.4.17(c)`'s launch-overrun zero** is discretionary; unlike Class M this
  class carries no penalty definition for it, since there is no group scoring
  here for an overrun launch to distort.

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.
Sub-clause letters are cited in the link text; they all live under one heading.

- Class N: [`NZ.7.5`](source-docs/nzmaa-s5-soaring-2024.md#75-class-n--ales-open-altitude-limited-electric-soaring)
  — (a) objective, (b)–(c) airframe/battery freedom, (d) task, (e) launch
  height, (f) landing bonus, (g) timing, (h) motor restart, (i) re-flights,
  (j) score, (k) still airborne, (l) round duration
- Altitude limiters: [`NZ.4.17`](source-docs/nzmaa-s5-soaring-2024.md#417-altitude-limiters)
- Electric landing table (and its 75 m cancellation — **does not reach this
  class**): [`NZ.4.13`](source-docs/nzmaa-s5-soaring-2024.md#413-precision-landings-for-electric-events)
