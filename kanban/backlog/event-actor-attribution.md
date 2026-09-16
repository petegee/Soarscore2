# Story — Event-actor attribution (who is on the immutable event log)

**Status:** Backlog · **Raised:** 2026-09-16 (WI-11 of
`kanban/completed/authentication-and-authorisation.md`, decision D6)

## What

Record *who* acted on the immutable event log. Today the log is the
auditability backbone for *what happened, when*; the acting identity
(authenticated person, machine client) lives only in the ephemeral
`ICurrentUser` of the request that appended the event. Two candidate designs,
to be argued on their own merits (each deserves its own LADR amendment, and
the choice is a genuine design decision, not an implementation detail):

1. **An `Actor` field on every event record.** Every aggregate event carries
   the acting `PersonId` (or client identity). Authoritative — the actor is
   part of the fact — but invasive: every event record, every fold, every
   existing stream replays through a schema widening across all aggregates.
2. **Append-metadata on the store.** The actor is not on the event; it
   accompanies the append — an `IEventStore.AppendAsync` port signature
   change, both adapters (Marten, Fisher), and the event-log endpoint
   surfacing it. Non-invasive to the domain; the actor is store-level
   provenance rather than domain fact.

## Why it matters

The audit story is half-told: an organiser investigating "who entered this
score / who granted this role" can see the event but not the principal behind
it. The trust model (immutable event log as the auditability backbone) and
the capture-policy model both lean on being able to answer "who" eventually;
the auth story deliberately did neither design (D6) so it would not grow a
second LADR mid-flight.

## Before starting

- Read D6 of
  `kanban/completed/authentication-and-authorisation.md` for the
  already-rejected "do it inside the auth story" context.
- Check `docs/ladr/ladr-0001-event-store.md` — either option is an amendment
  to it (event record shape vs port surface), so the LADR process is part of
  the work.
- Decide the "system actor" answer early: seeding and bootstrap appends
  (`ClassCorpusSeederHost`, mock persona seeder) act with
  `SystemCurrentUser`, not a person — both designs must say what those
  appends record.
- House rule 2 cross-check: glossary and class diagram untouched (no new
  domain concept — this is provenance, not aggregate state, if option 2
  wins; if option 1 wins, that argument belongs in the LADR).
