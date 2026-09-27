# NZ Soaring — Generally Applicable Rules

**Top-level parent** for the New Zealand national soaring classes. Source:
*NZMAA Flying Rules, Section 5: Soaring*, **October 2024, Rev 3.0** (it
supersedes the March 2024 edition, whose numbering differs — see the header of
[source-docs/nzmaa-s5-soaring-2024.md](source-docs/nzmaa-s5-soaring-2024.md)).

> **This is a different rulebook, maintained by a different body.** The files in
> `docs/rules/` above this directory condense the **FAI Sporting Code**, kept by
> the CIAM. These condense the **NZMAA** rules. They are not a variation of the
> FAI rules and do not inherit from
> [`../00-general-rules.md`](../00-general-rules.md); where the NZ rules cover an
> FAI class they say so and defer (§1.2–1.8, and §1.1), and where they define
> a national class the NZ text is the whole rule.
>
> House-keeping rule 1 applies here exactly as it does to the FAI tree: this
> directory is derived from `source-docs/` and is **read-only to the software
> process**. It tracks the sport, not the product.

Source refs in this directory and in the seed definitions are written `NZ.<clause>`
— `NZ.7.4(d)(iii)` — to keep them unambiguous against FAI refs, which share the
same numeric shape. Rev 3.0's clauses are `chapter.number` with lettered — and,
one level deeper, roman-numbered — sub-clauses in the clause body.

---

## 1. Scope and the FAI classes

§4.15 lists the NZ classes, A–R (with the E2/E400P/G1–G3 variants among them).
§4.16 lists the FAI classes flown in NZ, which are governed by the FAI Sporting
Code with the NZ adaptations in §1.2–1.8 and the NDC formats in §2 — those are
**not** modelled here.

Three NZ classes are currently modelled in `tools/Soarscore.SeedData/`, all in the ALES
(Altitude Limited Electric Soaring) family:

| Class | Name | Doc | Definition |
|---|---|---|---|
| M | ALES 200 | [class-m-ales200.md](class-m-ales200.md) | `SeedNzMAles200.cs`, `SeedNzMNdc.cs` |
| N | ALES 123 Open | [class-n-ales123.md](class-n-ales123.md) | `SeedNzNAles123.cs` |
| P | ALES Radian | [class-p-radian.md](class-p-radian.md) | `SeedNzPRadian.cs` |

Their common ground is in
[nz-ales-general-rules.md](nz-ales-general-rules.md).

---

## 2. Official flight and repeat attempts (`NZ.3.6`, `NZ.3.7`)

- **One official flight per round** unless the class says otherwise (`NZ.3.6`).
  There is an official flight once the model has left the hands of the
  competitor or helper under the pull of the launching apparatus.
- **`NZ.3.6(b)` repeat attempts apply to tow-launched classes only** — "all NZ
  tow launched classes except Premier Duration" — so the whole clause, including
  its grounds and notes, is **out of scope for the ALES classes**, which are
  self-launched. Each ALES class states its own re-flight position and
  two of the three state it as *none*.
- **`NZ.3.7` annulment**, with no repeat attempt: a model not conforming to the
  rules, a model losing a part during launch or flight (losing a part *on
  landing* is permitted), or a flight flown outside the frequency control system.

---

## 3. Landing (`NZ.4.11`–`NZ.4.13`)

General landing conduct is `NZ.4.11` (right of way; pilot and timekeeper stand
upwind of the spot; score and retrieve with haste). Then two precision-landing
tables, and which one applies is decided by the class, not by this section:

- **`NZ.4.12` — gliding events.** 15 m circle, 100 → 30 points over 23 rows,
  measured nose-to-centre. This is the same table as FAI F3J/F5L.
- **`NZ.4.13` — electric events.** 10 m circle, 50 → 5 points in ten rows,
  **"rounded to the next full metre"** (so a capture rounding of `Ceiling 1`).
  This is the same table as FAI F5J. Class M adopts it explicitly
  (`NZ.7.4(c)(ii)`); Classes N and P do not — they use their own three-step
  bonus instead.

**The 75 m rule is `NZ.4.13(c)`, and it reaches only what `NZ.4.13` reaches.**
A flight is *cancelled and recorded as a zero score* if the nose does not come
to rest within **75 m** of the centre of the competitor's designated landing
spot — but `NZ.4.13` opens "The following applies to Electric soaring classes
that call for precision landings", so the cancellation is scoped to the classes
that adopt the electric precision-landing table. That is a change from the March
2024 edition, which carried the same sentence as a standalone clause 2.4.6
under the general LANDING section and therefore read as universal. In the class
definitions that adopt it (Class M) it is a `flightValidWhen` gate rather than
a landing-bonus condition — it zeroes the flight, not the bonus. Classes N and
P, which do not reference the electric table, are **not** subject to it: outside
their circles they forfeit only the bonus.

> Two stale cross-references survive in Rev 3.0's class text and are left as
> written: `NZ.7.4(c)(ii)` still says "as per clause 2.4.5" and `NZ.7.5(e)`
> still says "See 2.8" — both cite the superseded March numbering; the clauses
> they mean are `NZ.4.13` and `NZ.4.17`.

---

## 4. Contests (`NZ.4.14`)

- **`NZ.4.14(a)`** — a contestants meeting no later than 15 minutes before
  round 1, to advise competitors of "any matters pertaining to the contest".
  This is the moment a CD-announced value is fixed, and it is why parameters
  such as Class M's target time bind at `BeforeFlying`.
- **`NZ.4.14(b)`** — the start and finish of rounds must be clearly identified,
  preferably by an audible alarm.

---

## 5. Altitude limiters (`NZ.4.17`)

Rev 3.0 no longer tags the clause *Provisional* in its heading, as the March
edition did, and the March note "overrides clause 3.13.6" is gone (its target
numbering no longer exists; Class N defers to the limiter rules via
`NZ.7.5(e)` instead).

- **`NZ.4.17(a)`** — every ALES model carries an **Altitude Limiter Switch
  (ALS)** that cuts the motor at the designated altitude, *and* cuts it at the
  class's time limit if that altitude has not been reached. Any brand.
- **`NZ.4.17(d)`, `(e)`** — the ESC must run through the ALS in series, never
  direct to the receiver, and the connectors must be accessible so the CD can
  fit an ALS reader on demand.
- **`NZ.4.17(c)`** — a launch exceeding the designated altitude **by more than
  10 %** through insufficient static venting: the CD **may** assign a score of
  zero for that round. **`NZ.4.17(f)`** — the same discretionary zero for
  "zooming". Note the discretion in both; see [§3 of the ALES
  parent](nz-ales-general-rules.md#3-what-is-not-scoring-data).
- **`NZ.4.17(g)`** — subverting the limiter rule set is grounds for
  disqualification as unsportsmanlike conduct.

**Consequence for the model:** the launch-height and motor-run limits that name
these classes — 200 m, 123 m, 20 s, 30 s — are enforced in hardware and never
reach the scorer. They are not metrics, not parameters and not penalties.

---

## 6. What this rulebook does not state

Recorded because the pattern is consistent across all three modelled classes and
each gap is a `no default` parameter or an open finding rather than an oversight
on our side:

- **No tie-break, anywhere.** None of §7.4, §7.5 or §7.7 states one.
- **No group-size minimum** for the one man-on-man class (M).
- **No round count** for M outside its NDC format.
- **No normalised-score rounding precision** for M.
- **No flight-time or landing-distance capture precision** for N and P.

---

## Source references

Deep-links into the verbatim extracted rule text (see
[source-docs/](source-docs/)). The official NZMAA PDF remains authoritative.

- Definitions: [`NZ.3.1`](source-docs/nzmaa-s5-soaring-2024.md#31-definitions)
- Official flight: [`NZ.3.6`](source-docs/nzmaa-s5-soaring-2024.md#36-official-flight)
- Flight annulment: [`NZ.3.7`](source-docs/nzmaa-s5-soaring-2024.md#37-flight-annulment)
- Thermal soaring: [`NZ.4.1`](source-docs/nzmaa-s5-soaring-2024.md#41-thermal-soaring)
- Landing, gliding table: [`NZ.4.12`](source-docs/nzmaa-s5-soaring-2024.md#412-precision-landings-for-gliding-events)
- Landing, electric table: [`NZ.4.13`](source-docs/nzmaa-s5-soaring-2024.md#413-precision-landings-for-electric-events)
- The 75 m rule: [`NZ.4.13(c)`](source-docs/nzmaa-s5-soaring-2024.md#413-precision-landings-for-electric-events)
- Contests: [`NZ.4.14`](source-docs/nzmaa-s5-soaring-2024.md#414-contests)
- NZ class list: [`NZ.4.15`](source-docs/nzmaa-s5-soaring-2024.md#415-nz-classes)
- Altitude limiters: [`NZ.4.17`](source-docs/nzmaa-s5-soaring-2024.md#417-altitude-limiters)
- Full document text: [nzmaa-s5-soaring-2024.md](source-docs/nzmaa-s5-soaring-2024.md)
