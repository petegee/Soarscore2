# NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)

Class N's shape with a 7-minute target and a 200 m limit, on a restricted
airframe. Inherits [00-nz-general-rules.md](00-nz-general-rules.md) and
[nz-ales-general-rules.md](nz-ales-general-rules.md). Source refs `NZ.7.7(...)`
(NZMAA Section 5: Soaring, **October 2024 Rev 3.0**; general clauses cited as
`NZ.3.x` / `NZ.4.x`).

Stated intent (`NZ.7.7(b)`): "to provide a simple set of rules for a fun event.
Open to Radian electric gliders or equivalent 2m all foam models. Major
modifications to the aircraft may lead to condemnation by your fellow pilots."

Objective (`NZ.7.7(c)`): "to fly three 7 minutes flights over 3 rounds with a
bonus for landing. Launch height is limited to 200m and motor run time to 30
seconds."

---

## 1. Pilot assignment to groups (the draw) — **and the open problem**

Individually scored by default. But the preamble makes the *scoring basis itself*
a CD choice (`NZ.7.7(d)`):

> "A Contest Director may decide to mass launch groups of pilots to add to the
> fun of the event. The CD may use group scoring in this instance but points will
> not be eligible for any record claims or NDC."

So one class covers two pipelines, selected at setup, and unlike Class M's NDC
variant the rulebook gives no separate clause numbering to split them on.

> **This is not currently expressible.** Whether the task normalises would have
> to be a bound parameter, and `Normalisation` is a value object, so no
> `ParameterRef` slot reaches it — the same residual that finding F12 hit on
> `Rounding`. `SeedNzPRadian.cs` writes the **individual** form, which is the
> one eligible for NDC and records. **A group-scored Class P contest cannot be
> scored by that definition.** Recorded in `competition-class-notation.md` §12,
> Left open.

Round duration is set by the CD, "for example each round could be 1 hour"
(`NZ.7.7(e)(xi)`) — a `no default` parameter.

---

## 2. Launch (`NZ.7.7(e)(iv)`, `(vi)`, `(vii)`)

- Launch height limited to **200 m**, controlled by an **Altimeter switch placed
  in line with the throttle channel** (`NZ.7.7(e)(iv)`, and `NZ.4.17`).
- Maximum motor run **30 seconds**, controlled by the onboard switch.
- Timing starts from the moment the model leaves the launcher's hand, and stops
  as soon as it touches the ground (`NZ.7.7(e)(vi)`).
- **The motor may not be restarted**; if it is, the watch is stopped immediately
  and landing points are lost (`NZ.7.7(e)(vii)`).

No restrictions on motor, airframe or battery chemistry beyond the 2 m all-foam
class limit; batteries may be recharged or swapped between flights
(`NZ.7.7(e)(i)`, `(ii)`).

---

## 3. Data the timer / helper collects

As Class N. **Landing bonus** (`NZ.7.7(e)(v)`), identical to Class N and again
**not** the `NZ.4.13` electric table:

| Nose at rest | Pts |
|---|---|
| inside 7 m radius | 50 |
| inside 15 m radius | 25 |
| outside 15 m | 0 |

The April 2018 revision changed this measurement to the **nose** of the model.

**The 75 m flight cancellation does not reach this class.** It is `NZ.4.13(c)`,
scoped to classes that adopt the electric precision-landing table (see
[00-nz-general-rules.md §3](00-nz-general-rules.md#3-landing-nz411nz413)); this
class uses its own three-step bonus and never references that table. Outside
15 m the bonus is zero; the flight stands. (The superseded March 2024 edition
carried the rule as a general clause and was read as reaching this class.)

---

## 4. The task (`NZ.7.7(e)(iii)`)

- **+1 point per second** flown, up to 7 minutes — **420 points**.
- **−1 point per second** flown over that time.

Cumulative: a 450 s flight scores `420×1 + 30×(−1)` = **390**.

---

## 5. Score (`NZ.7.7(e)(ix)`)

```
round score = flight points + landing bonus
final score = sum of the three round scores
```

**No normalisation** in the individual form: "Each flight counts. The final score
is the total of all points over three flights." See
[class-n-ales123.md §5](class-n-ales123.md#5-score-nz75j) — this is the second
of the two classes behind finding F25.

**NDC** (`NZ.7.7(f)`): "Group scored contest results are not eligible for NDC
contests." Which is the rulebook confirming that the individual form is the
default and the group form is the variant.

---

## 6. Rounds

**Three rounds, all count, no discard** (`NZ.7.7(c)`, `NZ.7.7(e)(ix)`).

---

## 7. Re-flights (`NZ.7.7(e)(viii)`)

**"No re-flights are permitted."** As Class N — a definite rule, not a silence
(finding F26).

---

## 8. A defect in the rule text — `NZ.7.7(e)(x)`

The clause reads, verbatim:

> "The model must be airborne at the end of the round the flight time for the
> flight & landing to count."

As written this requires a model to be **still flying** for its landing to score,
which cannot be meant — a landing bonus presupposes a landing. The parallel
Class N clause `NZ.7.5(k)` states the sensible rule and the opposite one:

> "If the model is still airborne at the end of the round the flight time stops
> at that point as well as no landing points awarded."

`SeedNzPRadian.cs` follows Class N and flags the reading in the file.

**Per house-keeping rule 1 this document has not been altered to match, and must
not be.** It tracks the sport. The reading should be confirmed with the NZMAA
before Class P is used to score a contest; if they confirm the Class N reading,
the fix belongs in their rulebook, not in ours.

---

## 9. What is not stated

- **Tie-break** — none.
- **Flight-time and landing-distance capture precision** — none.
- **What "equivalent 2 m all foam model" means.** Enforcement is explicitly
  social: "major modifications to the aircraft may lead to condemnation by your
  fellow pilots." Not scoring data, and not a scrutineering rule either.

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.
Sub-clause letters are cited in the link text; they all live under one heading.

- Class P: [`NZ.7.7`](source-docs/nzmaa-s5-soaring-2024.md#77-class-p--ales-radian-or-similar-2m-all-foam-electric-glider)
  — (a)–(d) intent, objective, group-scoring option; (e) contest rules
  (i)–(xi); (f) NDC eligibility
- Altitude limiters: [`NZ.4.17`](source-docs/nzmaa-s5-soaring-2024.md#417-altitude-limiters)
- Electric landing table (and its 75 m cancellation — **does not reach this
  class**): [`NZ.4.13`](source-docs/nzmaa-s5-soaring-2024.md#413-precision-landings-for-electric-events)
