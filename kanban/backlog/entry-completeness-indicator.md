# Entry-completeness indicator

**Status:** Backlog · **Raised:** 2026-08-18

## What

A read-side query answering, per task-round: how much of the expected data is
actually recorded. Roughly — of the competitors drawn into this task-round and
not withdrawn, how many have an Entry; of those, how many have at least one
flight; and does any flight lack a metric the task declares.

Surfaced to the Contest Director as a prompt — *"Round 3 — 20/20 entries, no
gaps"* or *"18/20 entries, 2 missing"* — so they can see at a glance whether a
task-round is ready to be marked complete, without walking the field asking.

**A query, never a state.** It computes nothing that gates anything, emits no
event, and does not transition a `TaskRound`. The CD presses the button; the
system only tells them what it can see.

## Why it matters

`kanban/in-progress/task-round-lifecycle.md` makes `TaskRoundCompleted` an
explicit act of the CD, deliberately — completion is the CD asserting a
task-round's *scores are in and settled*, which no amount of data inspection can
establish (see below). That is the right call, but it leaves the CD doing
bookkeeping by hand at the one moment it matters: deciding whether the contest
can be finalised.

This removes that burden without moving the authority. The CD still decides; they
just decide informed.

## Why it cannot simply be derived — the reason this is an indicator, not a state

Asked directly (user, 2026-08-18): if every metric for a task-round's groups is
entered, is the task-round complete? The honest answer is that **presence of data
can prove a task-round is not ready; absence can never prove that it is.** Three
legitimate domain outcomes are indistinguishable from "nobody has typed it yet":

- **The number of flights is data, not a constant.** `FlightSelection` has five
  kinds and only `ExactlyNInOrder` pins a count; for `last`, `all` and `bestN` —
  most of the corpus — a task's launch allowance is a ceiling, not a requirement.
  A pilot who took three of five allowed launches looks exactly like one who took
  five and entered three.
- **Metrics carry no required/optional flag.** `MetricDefinition` declares a name,
  kind, unit, precision and whether it is nominated before launch — nothing that
  says a value must be present. A landing metric absent because the pilot missed
  the box has the same shape as one absent because the Scorer has not got there.
- **`NoResult` is a first-class outcome that looks like missing data.** The
  glossary is emphatic that a flight never validly completed has *no result*,
  which is not zero; flight selection returns `NoResult` when an Entry has no
  flights at all. The domain deliberately treats absence as meaningful, which is
  exactly what stops absence from also meaning "not recorded yet". Overloading
  the one signal with both meanings would destroy the distinction that decides
  whether a contest is valid: "eighteen flew, two did not" versus "twenty flew,
  two are not entered".

A fourth, once re-flights land: a protest can append a group to a task-round
after it looked finished, so even a genuinely complete task-round can grow.

## Design constraints

- **Never phrase the output as "complete".** *"No gaps detected"*, *"18/20
  recorded"* — a factual count, never a verdict. If the output reads as
  authoritative, CDs will treat it as authoritative within a week and the project
  will have derived completeness by convention instead of by decision. This is
  the single most important line in this story.
- **A query, so it cannot gate.** It lives with the other read-side queries and
  answers on demand; nothing in a write path consults it. That is what keeps it
  compatible with [NFR-4](../../docs/non-functional-requirements.md) — an
  indicator that started gating anything would be the exact behaviour NFR-4
  forbids.
- **No new read model.** `IEntryQuery.FindAsync` already slices `entry_index` by
  task-round coordinate, and LADR-0001 §3 is explicit that scores are never
  projected. Counting *entries* is cheap from the index; establishing whether a
  flight lacks a declared metric needs the Entry streams themselves, so decide
  deliberately how far the indicator goes — an entry count alone may be most of
  the value for a fraction of the cost.
- **Class-agnostic, per CLAUDE.md's core architectural law.** The declared metric
  list comes from the adopted class definition's task; nothing here may branch on
  discipline.

## Before starting

- Decide the depth: entry-count-only (cheap, `entry_index` alone) versus
  metric-level gap detection (needs folding Entry streams for the task-round).
  Establish whether the CD actually wants the second before paying for it.
- Settle what "expected" means for a competitor with no Entry at all. A pilot who
  never launched may have no Entry, in which case the count can never reach 20/20
  and the indicator misleads in the opposite direction. This is the same ambiguity
  the section above describes, one level up, and it may want an explicit
  "did not fly" record — which would be a new concept and therefore needs approval
  (CLAUDE.md), not an inference.
- Check against `kanban/in-progress/task-round-lifecycle.md` once it completes:
  that thread owns `TaskRoundCompleted` and the reopen path, and this story must
  not quietly become the thing that decides completion.

## Not blocked by, and does not block

Independent of the task-round lifecycle thread. It reads state that thread
introduces, so it is more useful afterwards, but nothing in it needs to wait.
