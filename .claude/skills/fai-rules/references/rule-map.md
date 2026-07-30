# Rule map — topic × class

Routing index for `docs/rules/`. **Cells are lookup hints, not authority.** Before
any value here lands in code, a class definition or a requirement, confirm it in the
class doc named in the pointer column, and cite the source ref.

Classes: **F3B** (multi-task winch), **F3J** (thermal duration, tow), **F3K**
(hand-launch multi-task), **F5J** (electric thermal duration), **F5K** (electric
multi-task), **F5L** (electric RES).

---

## Contest shape

| | F3B | F3J | F3K | F5J | F5K | F5L |
|---|---|---|---|---|---|---|
| **Tasks per round** | 3 (A Duration, B Distance, C Speed) | 1 | 1, from catalogue A–N | 1 | 1, from catalogue A–E | 1 |
| **Working time** | per task | 10 min / 15 min fly-off | per task (7/10/15 min) | 10 min / 15 min fly-off | per task | 9 min (540 s) |
| **Min rounds for validity** | 1 round + 1 task (5 at Championships) | 4 | 5 (each a different task) | 4 | **none defined** | ≥4 |
| **Fly-off** | none | top ≥9, single group | 3–6 rounds, mandatory at WC/CC | top 30% (rounded down), single group 6–14 | optional, 3–6 rounds | top qualifiers, ≥2 rounds |

Pointers: `f3b.md#1`, `f3j.md#1`, `f3k.md#1`, `f5j.md#1`, `f5k.md#1`, `f5l.md#1`.

## The draw / group sizes

| | Min per group | Fairness floor | Notes |
|---|---|---|---|
| **F3B** | per task: A=5, B=3, C=8 or all | group annulled if only 1 valid result | drawn by frequency; order re-drawn each round |
| **F3J** | 6 (prefer 8–10) | move a pilot up if group ≤3 | matrix system; contest number from the matrix |
| **F3K** | 5 | — | as few groups as possible |
| **F5J** | 6 | move up / refill if ≤5 (≤4 in contests ≤30 pilots) | fewest groups, most competitors each |
| **F5K** | not stated | — | all pilots in a group launch simultaneously |
| **F5L** | not stated | — | fly-off group size = preliminary group size |

Cross-class draw framework (random initial draw `C.16.2.6`, anti-repeat composition,
team separation, frequency allocation): `00-general-rules.md#1`.
Frequency-follows-frequency is permitted **only** for F3B/F3J/F3K:
`f3-general-rules.md#1`.

> MVP note: team separation and frequency management are out of MVP software scope
> (individual-only, all-2.4 GHz) — see the scope note in `00-general-rules.md`.

## What the timer records

| | Flight time precision | Landing distance | Launch height |
|---|---|---|---|
| **F3B** | A: whole s · B: integer 150 m legs · C: ≥1/100 s | A only, rounded **up** to nearest metre | none |
| **F3J** | 0.1 s | yes | none |
| **F3K** | 0.1 s, **truncated** | none | none |
| **F5J** | whole s, truncated | yes | AMRT start height, whole m |
| **F5K** | whole s (tenths not rounded) | none (Pilot Area only) | AMRT, whole m, highest to 10 s after motor stop |
| **F5L** | full s | yes | not scored (hard cap 90 m / 30 s motor) |

Common field list: `00-general-rules.md#2`. F3-vs-F5 difference (no launch height in
F3): `f3-general-rules.md#2`.

Signed score card required (unsigned = 0 for the round): **F3K** (`F3K.1.2`), F5K,
F5L. Elsewhere general practice.

## Flight points and landing bonus

| | Flight points | Landing table | Pointer |
|---|---|---|---|
| **F3B** | A: 1 pt/s, max 600, **−1 pt/s over 600 s** · B: legs · C: elapsed time | 100→0 over 15 m; **none if flight > 630 s** | `f3b.md#2`, `F3B.2.3 d` |
| **F3J** | 1 pt/s | 100→0 over 15 m, 0.2 m steps near the spot | `f3j.md#2`, `F3J.10.5` |
| **F3K** | scored seconds per task rule | **none** | `f3k.md#2`, `F3K.11` |
| **F5J** | 1 pt/s, cap **600** qual / **900** fly-off | **50→0 over 10 m** (coarser than F3J/F5L) | `f5j.md#2`, `5.5.11.12 h` |
| **F5K** | 1 pt/s | none; −10 per landing outside the Pilot Area | `f5k.md#2`, `5.5.10.15` |
| **F5L** | **2 pt/s**, cap 390 s within 540 s | 100→0 over 15 m (same shape as F3J) | `f5l.md#2`, `5.5.12.11.2` |

## Launch-height scoring (F5 only)

| | Model | Values |
|---|---|---|
| **F5J** | **deduction** from raw | 0.5 pt/m ≤200 m; 3 pt/m above 200 m (`5.5.11.12 e`) |
| **F5K** | **bonus/penalty vs Nominal Launch Height** (60 m light / 70 m moderate wind) | below NLH +0.5/m; 1–10 m above −1.0/m; ≥11 m above −3.0/m; no bonus if flight <30 s (`5.5.10.3–10.4`) |
| **F5L** | not scored — hard cap only | flight = 0 if AMRT presets differ (`5.5.12.4`) |

## Normalisation and rounding

Best raw in group → 1000 for all six. Differences:

| | Unit normalised | Rounding of the normalised score |
|---|---|---|
| **F3B** | **each task separately** (A, B, C partials); C **inverted** | `F3B.2.6` |
| **F3J** | the group score | **truncated** to 0.1 |
| **F3K** | the group score | rounded to 0.1 |
| **F5J** | the group score | `5.5.11.12` |
| **F5K** | the group score | rounded to whole points (raw truncated down first) |
| **F5L** | the group score | `5.5.12.11` |

## Drop-worst

| | Threshold | Unit dropped |
|---|---|---|
| **F3B** | more than **5** rounds | lowest **partial per task** (not per round) |
| **F3J** | more than **7** qualification rounds | lowest round |
| **F3K** | **6** or more rounds | lowest round |
| **F5J** | more than **4** rounds | lowest round |
| **F5K** | **7** or more rounds | lowest round |
| **F5L** | more than **5** rounds | lowest round |

Penalties are retained even when the round they occurred in is dropped (all classes).

## Ties

| | Tie-break |
|---|---|
| **F3B** | one additional full round (all three tasks) |
| **F3J** | fly-off ties broken by qualifying position |
| **F3K** | best dropped score; then a one-task tie-break fly-off |
| **F5J** | fly-off ties broken by qualifying position |
| **F5K** | best dropped score; then a one-task tie-break fly-off |
| **F5L** | not stated |

## Re-flights

| | Mid-air collision entitles? | New-group minimum | Placement priorities stated? |
|---|---|---|---|
| **F3B** | yes (incl. launch cable fouling) | not numbered | yes (own variant) |
| **F3J** | yes (incl. towline interference) | **4** | yes |
| **F3K** | **no** — except in the **start phase** (release → highest point) | **4** | yes |
| **F5J** | yes | **6** | yes |
| **F5K** | **no** | **4** | yes (organiser's-fault case) |
| **F5L** | yes | — | **no — CD decision** |

Common pattern (claim discipline, waiver, the better-of rule):
`00-general-rules.md#7`. The scoring rule the software must enforce, in placement
cases 2 and 3: **the pilot allocated the re-flight scores the re-flight even if
worse**; every other pilot in that group scores the **better of** their two results.

F3J also has **group neutralisation** (`F3J.5.2`): fly-off rounds and the last group
of a qualification round only, event within the first 30 s → CD may restart the whole
group. No other class has this.

## Where a topic lives when the class doc is silent

| Topic | File |
|---|---|
| Initial random draw, starting order, team separation | `00-general-rules.md#1` (`C.16.2.6`) |
| Frequency allocation, spread-spectrum exemption | `00-general-rules.md#1` (`C.16.2`) |
| Timekeeping equipment, results display during the contest | `00-general-rules.md#2` (`C.16.1`) |
| Team classification (three best, tie-breaks) | `00-general-rules.md#5` (`C.15.6.2`) |
| CD penalty powers up to disqualification | `00-general-rules.md#6` (`C.19.1`) |
| Results published in classification order | `00-general-rules.md#5` (`C.13.7 h`) |
