# LADR-0004 — Authentication and authorisation

**Status:** Accepted · **Date:** 2026-09-15 · **Follows:** LADR-0003

Decided with the owner 2026-09-14 and signed off 2026-09-15 as decisions D1–D12
of the story (`authentication-and-authorisation.md`). This record states the
architecture; the plan — work items, the per-command policy table, the test
inventory — stays in the story. Settled elsewhere, not re-argued here: the four
read models and the unique-index-as-arbiter rule (LADR-0001); the in-box-lean
library rule the two new packages are admitted under (LADR-0003).

## Context

The documented trust model was a closed club tool with no authentication. It is
amended by owner decision 2026-09-14: Soarscore hosts in the cloud and opens to
competitors and organisers, so it must know who is acting. People register via
social login through an external OIDC provider. Organisers can do everything; a
competitor's powers are governed by **per-competition capture policy**, not by a
role hierarchy. There is no score sign-off, and the immutable event log of all
mutations remains the auditability backbone. The scale facts are unchanged — a
club-level tool for a small, trusted NZ group, ≤ 20 pilots, ≤ 8 rounds/day,
1–2 day events — and NFR-4 still forbids gating the contest on scores being up
to date.

Two boundaries frame everything below. None of the rule corpus (FAI or NZMAA)
touches authentication or capture authority, so this decision owes no rule
cross-reference. And capture policy is competition-level configuration, not
class data, so NFR-1/NFR-2 and the core architectural law are untouched: the
core interprets the policy and never branches on a competition class.

## Decision

### Auth0 is the identity provider

Social logins through Auth0, with email magic-link as the fallback for members
without social accounts. Auth0 stays a **pure** identity provider: no Auth0
Actions, rules or roles are configured, which is also why there is nothing to
drift (D2). Auth0 RBAC may still be used tenant-side to limit what tokens
Auth0 will issue per client, but the API keeps ignoring every claim beyond
`sub`/`email`/`name` (D12).

### JwtBearer in the Api — the only token-validating layer

Token validation lives in `src/Soarscore.Api` and nowhere else: the in-box
`AddJwtBearer` handler. The only new packages are
`Microsoft.AspNetCore.Authentication.JwtBearer` and its
`Microsoft.IdentityModel.*` token dependencies, pinned via Central Package
Management and licence-checked by the existing CI step (LADR-0003).

The Api parses `sub` on the first `|` into `(provider, subject)`; a `sub`
without `|` is a machine actor — provider `client-credentials`, subject the
client id (D12); a malformed `sub` leaves the principal unauthenticated and
the pipeline 401s it. Claims beyond `sub`, `email` and `name` are ignored:
scope-in-token authorisation is rejected because it would duplicate roles +
capture policy as a second, token-resident authority, contradict D2, and
drift from the event-store truth (D12). Roles never live in tokens — not even
in mock mode, where a claim-based role override is explicitly rejected: it
would test a lookup that never happens in production (D11).

### `ICurrentUser` — the port in Application

The Application layer consumes a current-user port, `ICurrentUser`
(authenticated?, provider, subject, email, `PersonId?`, roles). The Api
implements it as `HttpCurrentUser`, built from the validated principal and
the read model; `AnonymousCurrentUser` is registered under none-mode;
`SystemCurrentUser` exists only inside the seeding host's scope. Handlers see
identity through the port, never through HTTP types — which is what keeps the
Application layer transport-blind and the existing dispatcher-driven tests
working unchanged.

### Enforcement in the Dispatcher pipeline, via one explicit per-command policy table

`Dispatcher.Invoke` gains exactly one step (D1): resolve
`IAuthorizationPipeline` via `GetService` — when absent (none-mode, and every
existing test that constructs a bare `Dispatcher`), behave exactly as today;
when present, authorise first and map a denial straight to
`Result<T>.Failure` before the handler is resolved. The dispatcher header's
"no behaviour pipeline, no decorators" claim is amended by the same decision:
this is the one behaviour step, it is the owner-decided enforcement point
(2026-09-14), and it is inspectable by reading the pipeline class.

Authorisation is per-command policy, never scattered in handlers. A literal,
hand-written table maps every command and query to one of four policy kinds —
**A**uthenticated, **O**rganiser, **S**elf-or-organiser, **C**apture policy
(the complete table lives in the story, §Per-command policy table). The
pipeline **fails closed**: a message type absent from the table denies with
`auth.policyMissing` (a 500 bug alarm), and an architecture totality test
fails the build if a mapped command or query is missing from the table. Under
`oidc` the default is secure: every query is `Authenticated`, and per-query
public reads are a later per-query opt-out, stubbed as backlog work (D4).
`GET /who-am-i` is in scope because the SPA/BFF cannot derive the Soarscore
`PersonId` from the token and needs it to decide what to render — the one
read that cannot be reconstructed from the JWT alone (D9).

### Capture policy is competition configuration (D10)

Who may enter which scores is policy as data: per-competition configuration,
stored on the Competition aggregate and projected into the competitions read
model.

```
enum CapturePolicyMode { OrganisersOnly, AnyRegisteredPerson, AllowList }
record CapturePolicy(CapturePolicyMode Mode, IReadOnlyList<PersonId> Capturers)
```

Evaluation for a capture command, in order, first match wins: not
authenticated → 401; Organiser role → allow; mode `OrganisersOnly` → deny; no
linked `PersonId` (authenticated at the IdP but not a known person) → deny —
an unlinked identity is nobody; mode `AnyRegisteredPerson` → allow; mode
`AllowList` → allow iff the acting `PersonId` is in `Capturers`. The policy
reads **only** competition configuration and the principal — never task-round
state, never what has been captured (NFR-4). A competition with no configured
policy evaluates as `OrganisersOnly`, today's single-operator behaviour.
Reconfiguration is allowed at any time, including mid-contest; it changes
*who may enter*, never *what has been entered*.

The core never branches on who is acting beyond applying the policy — the
same generic-interpretation rule the Competition Class model obeys. Roles for
v1 are Competitor and Organiser only, the Contest Director folded into
Organiser. Who *enters* scores is deliberately independent of the role that
person played at the contest: organiser retro-entering, a scorer recording
for another pilot, and pilot self-scoring are all legitimate depending on the
competition. Competitor powers in v1 are exactly the capture policy (D7):
self-service entry, self-withdrawal and invite-email delivery stay
organiser-side actions in v1, stubbed as backlog work. Machine actors get no
new role either: a client-credentials client is bound to a Person by an
organiser (`BindIdentity`) and earns authority through the same vocabulary —
roles for administrative reach and, above all, the capture-policy
`AllowList`; a scoring-rig client is allow-listed on the competition it
serves, and a results-display client needs nothing beyond authenticated
reads. No delegation, no scopes (D12).

### Identity resolution is a per-request read-model lookup (D2)

The validated JWT supplies `(provider, subject)` and `email` only. `PersonId`
and roles come from the `people` read model on every request
(`FindIdentityAsync`). Roles change organiser-side and take effect on the
next request with no token-refresh dance; at club scale the indexed lookup is
free. This is what keeps Auth0 a pure identity provider.

Sign-in is get-or-create, matching identity first, email second (D5):
identity link found → return that person; else a person with the token's
email exists → link the identity to that person; else create the person from
token claims and link. The unique index on `(provider, subject)` — not the
pre-reads — is the arbiter, exactly the Person email precedent (LADR-0001
§2): pre-reads are a best-effort for the common path, the adapter translates
the index violation, and the handler retries the resolution exactly once
before propagating the failure. One clarification to LADR-0001 §3, and it is
not a fifth read model: identity rows are part of the **people** read model,
so the four-model inventory stands.

### Bootstrap of the first organiser (D3)

`Soarscore:Auth:BootstrapOrganisers` is an array of email addresses; at
sign-in/link time a person whose email is on the list is granted Organiser
automatically, idempotently. The list is empty by default; a deployment
without it has no organiser, which is a visible, correct state. Rejected: a
one-off CLI command (more moving parts, same outcome — someone still runs it
against the deployment) and hand-editing the store (forbidden).

### Auth modes and the Production guard (D8)

`Soarscore:Auth:Mode` selects how the Api boots:

| Mode | Behaviour |
|---|---|
| `none` (default) | Byte-identical to today: no JwtBearer, no pipeline enforcement, `AnonymousCurrentUser` injected. Refused at startup in Production. |
| `mock` | The real enforcement path with a local issuer: same JwtBearer validation, same pipeline, same policies — only the signing key/issuer come from config, and dev personas are seeded at startup with their bearer tokens printed to the console (D11). Production refuses it. |
| `oidc` | JwtBearer validates the token against the Auth0 domain; the pipeline authorises every command and query; anonymous callers get 401s, unauthorised ones 403s. |

Production boots only under `oidc`: `Composition.Build` throws at startup
when the mode is `none`, unset or `mock` while
`builder.Environment.IsProduction()`. A release deployment that wanted to
dodge auth gets a loud crash at boot, not a silently open or mock-keyed API.
"Release always applies AuthN and AuthZ" is pinned three ways: this guard and
its composition test; the acceptance suite exercising the full enforcement
path in `mock` on every run; and D11's rule that `mock` is the shipped
enforcement code with a local issuer — never a bypass that tests something
that never ships.

### Actor attribution is deferred (D6)

The immutable event log remains the auditability backbone for *what happened,
when*; *who* is not on events today, and this decision does not put it there.
Adding it means either an `Actor` field on every event (invasive across all
aggregates and folds) or append-metadata on the store (a port signature
change to `IEventStore.AppendAsync` plus both adapters and the event-log
endpoint). Both are real designs deserving their own LADR amendment; both are
stubbed as backlog work (`event-actor-attribution.md`) and this story does
neither.

## Consequences

- **The whole existing test corpus runs unchanged under `none`.** Enforcement
  is opt-in per composition, which is what makes the story landable without
  touching the unit, property, store-backed and BDD suites. The cost is
  accepted: a non-Production composition that forgets to register the
  pipeline is silently open — which is why the Production guard, the
  acceptance suite running `mock` on every run, and the fail-closed table all
  exist.
- **Fail-closed, total, inspectable.** A missing table entry denies with
  `auth.policyMissing` and the totality test makes that backstop unreachable;
  the table is one hand-written registration list — no assembly scanning, no
  attributes.
- **Roles are always live.** Grants and revocations take effect on the next
  request with no token refresh and nothing to invalidate, because roles are
  never claims.
- **Auth0 carries no authority.** No Actions, rules, roles or permissions to
  configure or drift; the tenant-side RBAC switch only limits what tokens
  Auth0 will issue.
- **Two new packages, nothing more.** The in-box JwtBearer handler and its
  `Microsoft.IdentityModel.*` token dependencies (LADR-0003).
- **No fifth read model.** Identity rows join the people read model; ports
  gain methods, not new abstractions (LADR-0001).
- **One behaviour step in the Dispatcher.** The "no behaviour pipeline, no
  decorators" claim is amended — this is the exception, by owner decision.
- **Attribution gap, known.** Until the actor-attribution story lands, the
  event log answers what-and-when, not who (D6).
- **v1 boundaries, stated.** Role set fixed at Competitor + Organiser;
  competitor powers are exactly the capture policy (D7); machine actors are
  persons, not a new authority kind (D12); queries are authenticated by
  default with public reads as a later opt-out (D4).

## Compliance mapping

**Layer rules** (ArchUnitNET, enforced at build):

- The token-validating layer is the Api only. Application gains `ICurrentUser`,
  `IAuthorizationPipeline`, the policy interfaces and the policies — the Auth
  folder references Application and Domain types only, so `LayerRuleTests`
  needs no change.
- Domain gains events, folds and decides only (Person roles and identity
  links; Competition capture policy) with no dependency outside the BCL;
  nothing in Domain knows about tokens, providers or claims.
- Infrastructure gains projection folds, the unique compound index on
  `(provider, subject)` — the link arbiter — and query-port methods on both
  backends, under LADR-0001's port and projection rules; inline projections
  only, never the async daemon.
- Routes stay verbs (`MapCommand`/`MapQuery`); the new commands and query go
  through the unchanged GET/POST route-shape reflection test.

**NFR-3 (headless core):** auth adds a transport concern (JwtBearer, Api
only), one Application port (`ICurrentUser`), and policy interpretation in
the Application layer. No UI, no UX assumptions; the SPA/BFF remains a
separate consuming system, which is why auth suits an SPA redirect/BFF flow
from day one and why `/who-am-i` exists. In `mock`, persona tokens print to
the console — no mock-token endpoint and no UI.

**NFR-4 (no imposed ordering on score capture):** the capture policy gates
*who*, never *when*. D10's evaluation reads configuration and the principal
only, and the temporal-blindness property pins it: the capture decision for a
(principal, competition) pair is invariant under any insertion, removal or
reordering of capture events. No capture command gains a precondition on
round state.

**NFR-1/NFR-2 and the core architectural law:** capture policy is
competition-level configuration, not class data; no class-specific branch
appears anywhere in this design.

**LADR-0001:** no new read-model count — identity rows belong to the people
read model; the unique index remains the arbiter, with no read-check-write on
aggregate invariants; D5's single bounded retry is resolution, not
enforcement.

**LADR-0003:** the only new packages are the in-box JwtBearer handler and its
token dependencies, pinned via CPM and licence-checked by the existing CI
step.

**Rules:** none of the rule corpus — FAI or NZMAA — touches authentication or
capture authority; no rule cross-reference is owed.
