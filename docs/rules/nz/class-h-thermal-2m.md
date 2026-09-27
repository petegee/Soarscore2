# NZ Class H — Thermal 2 Metre

Five flights against **fixed targets of 3, 4, 5, 6 and 7 minutes**, flown in
**any order** inside a CD-set contest window, with all flight and landing points
**summed raw** — no normalisation, no groups, no re-flights stated in the class
itself. Inherits [00-nz-general-rules.md](00-nz-general-rules.md) only (it is
**not** an ALES class — hand tow, not electric; see
[nz-ales-general-rules.md](nz-ales-general-rules.md) for why that parent does
not reach it). Source refs `NZ.5.5(...)` (NZMAA Section 5: Soaring, **October
2024 Rev 3.0**; general clauses cited as `NZ.3.x` / `NZ.4.x`).

Stated objective (`NZ.5.5(a)`): "A straightforward contest in which basic models
can be used".

This is the first NZ class modelled in `tools/Soarscore.SeedData/` that is
**tow-launched**, so `NZ.3.6(b)`'s repeat-attempt grounds reach it — the ALES
classes are self-launched and that clause excludes them. Its landing bonus is
the class's **own** single-step rule, not one of the two general tables
([00-nz-general-rules.md §3](00-nz-general-rules.md#3-landing-nz411nz413)).

---

## 1. The format — one contest window, five self-scheduled flights

- **`NZ.5.5(d)(i)`** — the CD "may determine a maximum contest time suitable
  for the weather conditions and the number of competitors"; "**It is suggested
  that 3 hours is a suitable contest time**". A CD choice with a suggestion, not
  a stated number — a `no default` parameter (`contestTime`, seconds, bound
  before flying per `NZ.4.14(a)`), consumed by **no scoring stage**: the expiry
  rule below is contest flow, not scoring arithmetic.
- **`NZ.5.5(d)(ii)`** — during the contest time, pilots complete **flights of
  3, 4, 5, 6 & 7 minutes in any order**, but "the contestant must **nominate to
  which task the previous flight will be recorded before attempting the next
  flight**. When to launch is up to the individual." Self-scheduled throughout:
  the nomination is a post-landing act, so a flight is attributed to a task
  after it flies, not before.
- **`NZ.5.5(d)(iii)`** — when contest time expires, "only flights already
  completed or models in flight that have already been released from the tow or
  bungee and are being timed will be counted." A model released before expiry
  and still airborne at expiry **keeps being timed** — contrast Class N's
  `NZ.7.5(k)`, where the watch stops.
- **No groups anywhere.** Individual scoring; there is no man-on-man element
  and nothing to normalise against.

## 2. The model and launching

- **`NZ.5.5(b)(i)`** — maximum **projected** wingspan **2 m**; maximum **three
  servos**; the optional third servo is for **spoiler control only**, and
  spoiler control "must not utilise trailing edge flaps". Equipment rules,
  enforced at processing — **not scoring data** (the same discipline as the
  ALES altitude limits).
- **`NZ.5.5(c)(i)`** — launching by **Hand Tow or Hand Operated Pulley Tow**;
  **bungees at the CD's discretion**; **winches may not be used**. The clause's
  "Refer to Rule 2.2.2 for launching definitions" is a **stale cross-reference**
  (same disease as `NZ.7.5(e)`'s "See 2.8"): §2.2.2 does not exist in Rev 3.0 —
  §2 is the NDC rules for FAI events — and the definitions it means are
  [`NZ.4.2 Launching`](source-docs/nzmaa-s5-soaring-2024.md#42-launching),
  [NZ.4.4 Hand towing](source-docs/nzmaa-s5-soaring-2024.md#44-hand-towing),
  [NZ.4.6 Hand Operated Pulleys](source-docs/nzmaa-s5-soaring-2024.md#46-hand-operated-pulleys)
  and [NZ.4.7 Bungee](source-docs/nzmaa-s5-soaring-2024.md#47-bungee).
  Not scoring data either.
- Tow-launched ⇒ **`NZ.3.6(b)` repeat attempts reach this class** (grounds
  `NZ.3.6(c)(i)–(vii)`, at most one repeat per official flight). The class text
  itself states nothing about re-flights.

## 3. Data the timer collects

| Field | Precision / rule |
|---|---|
| **Flight time** | Released from tow → ground contact. **No precision stated** — whole-second scoring makes 1 s truncated the natural capture. |
| **Landing distance** | Nose of the model at rest, against a **15 m radius** circle (`NZ.5.5(f)(i)`). **No capture precision stated.** |
| **Field boundary** | Landing outside the appointed flying field: **all flight and landing points for that flight forfeited** (`NZ.5.5(f)(i)`, second sentence). |

**Landing bonus** (`NZ.5.5(f)(i)`) — a single step, measured to the **nose**:

| Nose at rest | Pts |
|---|---|
| inside 15 m radius | 50 |
| outside 15 m | 0 |

Not the `NZ.4.12` gliding table (15 m circle but **100→30 over 23 rows**) and
not the `NZ.4.13` electric table (10 m, 50→5) — Class H states its own.

## 4. Scoring per flight (`NZ.5.5(e)`)

- **(i)** **+1 point per second** of flight **up to the target time** — an
  under-target flight scores its flown seconds (partial credit).
- **(ii)** **−1 point per second** more than the target. Cumulative over one
  metric, the Classes M/N/P reading (identical wording; the Class N worked
  example `NZ.7.5(d)` scores a 400 s flight against 360 as 320): against the
  3-minute target, a 200 s flight scores `180×1 + 20×(−1)` = **160**.
- **(iii)** **More than 60 s over the target forfeits all the points for that
  flight.** A cliff, not a clamp: against the 3-minute target a 240 s flight
  scores `180 − 60` = **120**, a 241 s flight scores **0**.

## 5. Score (`NZ.5.5(e)(iv)`)

```
flight score  = seconds up to target − seconds over target, forfeited (0) beyond 60 s over
flight total  = flight score + landing bonus (per flight)
final score   = sum of the five flight totals   — raw, no normalisation
```

"All flight and landing scores will count towards the individual's total. The
competitor with the highest accumulated score wins." **There is no
normalisation** — the same finding-F25 territory as Classes N and P: the raw
score is the task result, and rounds aggregate raw points.

Maximum: per task, target + 50 landing → **230 / 290 / 350 / 410 / 470**;
contest max **(180+240+300+360+420) + 5×50 = 1750**.

## 6. Rounds

**Five flights, one per target, in any order** (`NZ.5.5(d)(ii)`) — the round is
a bookkeeping slot for a task, not a scheduled window; the draw's running order
is a convenience. **No discard** — every flight counts (`(e)(iv)`).

Software call (owner decision 2026-09-27): **validity requires all five** —
`(d)(ii)` says pilots are "required to complete flights of 3,4,5,6 & 7
minutes". Accepted consequence: a weather-truncated contest (`(d)(iii)`) scores
invalid for a competitor with fewer than five flights.

## 7. Re-flights

**`NZ.5.5` states nothing.** But `NZ.3.6(b)` — "Applies to all NZ tow launched
classes except Premier Duration" — reaches this class, granting repeat attempts
on the `NZ.3.6(c)` grounds (collision, mis-judged timing, launch-system
malfunction, model fails to detach), at most once per official flight
(`(c)(vii)`), with an unstated exception cross-reference ("1.6.1(a)", stale
March-edition numbering, presumably the collision/interference case).

The grounds are stated; the **scoring outcome is not** — so the definition
carries `UndefinedRequiresRuling` (the Class M-NDC precedent for stated
entitlement, unstated outcome) and treats a repeat as CD workflow. Software
call (owner decision 2026-09-27): **nothing encoded** — no launch limit, and a
repeat's recorded evidence is an additional attempt that the last-flight
selection ignores.

## 8. What is not stated

- **Tie-break** — none (the NZ rules state none anywhere;
  [00-nz-general-rules.md §6](00-nz-general-rules.md#6-what-this-rulebook-does-not-state)).
- **Flight-time and landing-distance capture precision** — none.
- **Whether a truncated contest stays valid** — `(d)(iii)` makes fewer than
  five flights possible; resolved by the owner decision in §6.
- **Whether `(e)(iii)`'s forfeit takes the landing bonus too** — resolved by
  owner decision 2026-09-27: the whole flight forfeits ("all the points for
  that flight" read inclusively; `(f)`'s "flight and landing" enumeration read
  as scope-clarifying, not contrastive).

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.
Sub-clause letters are cited in the link text; they all live under one heading.

- Class H: [`NZ.5.5`](source-docs/nzmaa-s5-soaring-2024.md#55-class-h--new-zealand-thermal-2-metre-rules)
  — (a) objective, (b) the model, (c) launching, (d) flying, (e) scoring,
  (f) landing
- Launching: [`NZ.4.2`](source-docs/nzmaa-s5-soaring-2024.md#42-launching)
  (hand tow `NZ.4.4`, pulleys `NZ.4.6`, bungee `NZ.4.7`)
- Official flight and repeat attempts: [`NZ.3.6`](source-docs/nzmaa-s5-soaring-2024.md#36-official-flight)