# NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)

Class N's shape with a 7-minute target and a 200 m limit, on a restricted
airframe. Inherits [00-nz-general-rules.md](00-nz-general-rules.md) and
[nz-ales-general-rules.md](nz-ales-general-rules.md). Source refs `NZ.3.15.x`
(NZMAA Section 5: Soaring, March 2024).

Stated intent (`NZ.3.15`): "to provide a simple set of rules for a fun event.
Open to Radian electric gliders or equivalent 2m all foam models. Major
modifications to the aircraft may lead to condemnation by your fellow pilots."

Objective: "to fly three 7 minutes flights over 3 rounds with a bonus for
landing. Launch height is limited to 200m and motor run time to 30 seconds."

---

## 1. Pilot assignment to groups (the draw) — **and the open problem**

Individually scored by default. But the preamble makes the *scoring basis itself*
a CD choice:

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
(`NZ.3.15.1 k`) — a `no default` parameter.

---

## 2. Launch (`NZ.3.15.1 d, f, g`)

- Launch height limited to **200 m**, controlled by an **Altimeter switch placed
  in line with the throttle channel** (`NZ.3.15.1 d`, and `NZ.2.8`).
- Maximum motor run **30 seconds**, controlled by the onboard switch.
- Timing starts from the moment the model leaves the launcher's hand, and stops
  as soon as it touches the ground.
- **The motor may not be restarted**; if it is, the watch is stopped immediately
  and landing points are lost (`NZ.3.15.1 g`).

No restrictions on motor, airframe or battery chemistry beyond the 2 m all-foam
class limit; batteries may be recharged or swapped between flights.

---

## 3. Data the timer / helper collects

As Class N. **Landing bonus** (`NZ.3.15.1 e`), identical to Class N and again
**not** the `NZ.2.4.5` electric table:

| Nose at rest | Pts |
|---|---|
| inside 7 m radius | 50 |
| inside 15 m radius | 25 |
| outside 15 m | 0 |

The April 2018 revision changed this measurement to the **nose** of the model.

---

## 4. The task (`NZ.3.15.1 c`)

- **+1 point per second** flown, up to 7 minutes — **420 points**.
- **−1 point per second** flown over that time.

Cumulative: a 450 s flight scores `420×1 + 30×(−1)` = **390**.

---

## 5. Score (`NZ.3.15.1 i`)

```
round score = flight points + landing bonus
final score = sum of the three round scores
```

**No normalisation** in the individual form: "Each flight counts. The final score
is the total of all points over three flights." See
[class-n-ales123.md §5](class-n-ales123.md#5-score-nz3131-i) — this is the second
of the two classes behind finding F25.

**NDC** (`NZ.3.15.2`): "Group scored contest results are not eligible for NDC
contests." Which is the rulebook confirming that the individual form is the
default and the group form is the variant.

---

## 6. Rounds

**Three rounds, all count, no discard** (`NZ.3.15`, `NZ.3.15.1 i`).

---

## 7. Re-flights (`NZ.3.15.1 h`)

**"No re-flights are permitted."** As Class N — a definite rule, not a silence
(finding F26).

---

## 8. A defect in the rule text — `NZ.3.15.1 j`

The clause reads, verbatim:

> "The model must be airborne at the end of the round the flight time for the
> flight & landing to count."

As written this requires a model to be **still flying** for its landing to score,
which cannot be meant — a landing bonus presupposes a landing. The parallel
Class N clause `NZ.3.13.1 j` states the sensible rule and the opposite one:

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

- Class P: [`NZ.3.15`](source-docs/nzmaa-s5-soaring-2024.md#315-class-p-ales-radian-or-similar-2m-all-foam-electric-glider)
- Contest rules: [`NZ.3.15.1`](source-docs/nzmaa-s5-soaring-2024.md#3151-contest-rules)
- NDC eligibility: [`NZ.3.15.2`](source-docs/nzmaa-s5-soaring-2024.md#3152-ndc-rules)
- Altitude limiters: [`NZ.2.8`](source-docs/nzmaa-s5-soaring-2024.md#28-altitude-limiters--provisional)
- The 75 m rule: [`NZ.2.4.6`](source-docs/nzmaa-s5-soaring-2024.md#246-the-flight-is-cancelled-and-recorded-as-a-zero-score-if-during-landing-the-nose-of-the-model)
