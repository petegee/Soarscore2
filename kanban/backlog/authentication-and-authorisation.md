# Authentication & authorisation

**Status:** backlog — plan written 2026-09-14; WI-1 owner sign-off gates everything below it

## What

Add authentication and authorisation to the Soarscore core API so it can be
hosted in the cloud, with competitors and organisers registering via social
login. Decisions recorded with the owner (2026-09-14):

- **Identity provider:** Auth0 — social logins, with email magic-link as a
  fallback for members without social accounts.
- **API auth:** JWT bearer validation in `src/Soarscore.Api`; identity exposed
  to the Application layer via a current-user port; enforcement in the
  `Dispatcher` pipeline as per-command policies, not scattered in handlers.
- **Roles for v1:** Competitor + Organiser only — Contest Director folds into
  Organiser. Who *enters* scores is deliberately independent of the role that
  person played at the contest (organiser retro-entering, a scorer recording
  for another pilot, or a pilot self-scoring are all legitimate depending on
  the competition).
- **Capture policy is competition configuration:** who may enter which scores
  is per-competition data (policy as data); the core never branches on who is
  acting, it interprets the policy.
- **Role assignment:** invite flow — organisers invite/promote. Bootstrap of
  the *first* organiser is settled as D3 below.
- **Front-end:** a web front-end is planned as a separate consuming system
  (NFR-3); auth must suit an SPA redirect/BFF flow from day one.
- **Mock auth (owner requirement, 2026-09-14):** a config item must make OIDC
  mockable locally with pre-canned persona tokens, so different roles can be
  exercised without a live Auth0 — and without a bypass that tests something
  that never ships. Release builds always enforce (D8/D11).
- **Third-party integrations (raised 2026-09-14):** a client-credentials
  (M2M) flow is wanted; treated as machine actors, no delegation (D12).
- **Hosting:** Fly.io (Sydney) + managed PostgreSQL for Marten; SQLite stays
  dev/test.

## Why it matters

Hosting in the cloud and opening the system to competitors and organisers
requires knowing who is acting. The documented trust model assumed a closed
club tool with no auth (CLAUDE.md, amended owner-approved 2026-09-14).

## Before starting

Everything in this section is WI-1 (owner sign-off) or WI-2 (docs). No code
WI below may start until WI-1 is discharged.

- **Glossary approval** for the new concepts this introduces — proposed text
  in §Glossary-and-diagram-amendments. Do not add without owner approval
  (CLAUDE.md glossary rule).
- **users.md drift (flagged, unresolved):** per-competition capture policy
  makes Soarscore *police* who may enter scores, where users.md currently says
  Soarscore "neither enables nor forbids" competitor entry and that pilots do
  not self-score. Owner chose to amend the trust model only; the users.md
  amendment is owed in this story (proposed text in
  §Glossary-and-diagram-amendments).
- Cross-check (done, recorded in §Cross-checks): NFR-3 (headless core — auth
  is transport + a port + policy interpretation), NFR-4 (auth must not impose
  ordering on score capture), NFR-1/2 untouched (capture policy is
  competition configuration, not class data), LADR-0001 (new projections stay
  inside the four permitted read models), LADR-0003 (no new libraries beyond
  the in-box JwtBearer handler and its token packages).

---

# Plan

## Shape of the change

Five layers, each with a single responsibility. Every new type lives in the
layer named; nothing crosses a layer boundary that LayerRuleTests does not
already police.

```
Auth0 (IdP, external) ──JWT──▶ Soarscore.Api
                                │  JwtBearer validation, sub → (provider, subject)
                                │  HttpCurrentUser: (provider, subject) ─IPeopleQuery─▶ PersonId + Roles
                                ▼
                        Soarscore.Application
                                │  ICurrentUser port (identity of the actor)
                                │  AuthorizationPipeline: command type → policy → allow/deny
                                │  (Dispatcher.Invoke consults the pipeline before resolving a handler)
                                ▼
                         Soarscore.Domain
                                │  Person: Roles, IdentityLinks (events + decides)
                                │  Competition: CapturePolicy (event + decide)
                                ▼
                      Soarscore.Infrastructure
                                │  projections: identity rows, roles, capture policy
                                │  unique index (provider, subject) — the link arbiter
```

Auth modes (`Soarscore:Auth:Mode`):

| Mode | Where | Behaviour |
|---|---|---|
| `none` (default) | local dev, every existing test | **Byte-identical to today.** No JwtBearer, no pipeline enforcement, `AnonymousCurrentUser` injected. Refused at startup in Production (D8). |
| `mock` | local role-testing, the acceptance suite | **The real enforcement path with a local issuer**: same JwtBearer validation, same pipeline, same policies — only the signing key/issuer come from config, and dev personas are seeded at startup with their bearer tokens printed to the console. Production refuses it (D8). See D11. |
| `oidc` | Fly.io deployment | JwtBearer validates the token against the Auth0 domain; the pipeline authorises every command and query; anonymous callers get 401s, unauthorised ones 403s. **The only mode Production allows** (D8). |

The entire existing test corpus (unit, property, store-backed, BDD) runs
unchanged under `none` — enforcement is opt-in per composition (D1), which is
what makes this story landable without touching forty-odd test files.

## Design decisions (D1–D12)

Recommendations are made here; WI-1 asks the owner to tick each one. Anything
not ticked blocks the WIs that depend on it.

**D1 — Enforcement is opt-in per composition.** `Dispatcher.Invoke`
(`src/Soarscore.Application/Dispatcher.cs:45`) gains one step: resolve
`IAuthorizationPipeline` via `GetService` — when absent (none-mode, every
existing test that constructs a bare `Dispatcher`), behave exactly as today;
when present, await `AuthorizeAsync` first and map a denial straight to
`Result<T>.Failure` without resolving the handler. The header comment's
"no behaviour pipeline, no decorators" claim is amended: this is the one
behaviour step, it is the owner-decided enforcement point (2026-09-14), and
it is inspectable by reading the pipeline class.

**D2 — Identity resolution is a per-request read-model lookup; roles never
live in tokens.** The validated JWT supplies `(provider, subject)` and `email`
only. `PersonId` and roles come from the `people` read model
(`IPeopleQuery.FindIdentityAsync`) on every request. Roles change
organiser-side and take effect on the next request with no token-refresh
dance; at club scale the indexed lookup is free. This keeps Auth0 a pure
identity provider — no Auth0 Actions/rules/roles to configure or drift.

**D3 — First-organiser bootstrap = config-listed emails.**
`Soarscore:Auth:BootstrapOrganisers` is an array of email addresses. At
sign-in/link time (`LinkSignIn`, WI-7), a person whose email is on the list is
granted Organiser automatically (idempotent). Rejected: a one-off CLI command
(more moving parts, same outcome — someone still runs it against the
deployment); hand-editing the store (forbidden). The list is empty by default;
a deployment without it has no organiser, which is a visible, correct state.

**D4 — Queries require an authenticated principal under `oidc`.** Default
secure: every query is `Authenticated`; per-query public reads (e.g. a
public leaderboard) are a later per-query opt-out, stubbed as a backlog story.
Rationale: pilots sign in to read results anyway (the SPA needs an account for
capture policy to mean anything), and "which reads are public" deserves its
own decision, not a default.

**D5 — Sign-in get-or-create matches on identity first, email second.**
`LinkSignIn` (WI-7): identity link found → return that person. Else a person
with the token's email exists → link the identity to that person. Else create
the person from token claims (name, email) and link. The unique index on
`(provider, subject)` — not the pre-reads — is the arbiter, exactly the
`RegisterPerson` email precedent (`src/Soarscore.Application/Commands/People/RegisterPerson.cs:5-9`):
pre-reads are a best-effort for the common path, the adapter translates the
index violation, and the handler retries the resolution exactly once on
`eventStore.uniqueConstraintViolation` before propagating the failure.

**D6 — Event-actor attribution is deferred to its own story.** The immutable
event log remains the auditability backbone (what happened, when); *who* is
not on events today. Adding it means either an `Actor` field on every event
(invasive across all aggregates and folds) or append-metadata on the store
(port signature change to `IEventStore.AppendAsync` + both adapters + the
event-log endpoint). Both are real designs deserving their own LADR amendment;
this story does neither. Backlog stub `event-actor-attribution.md` created in
WI-11 with both options sketched.

**D7 — Competitor powers in v1 are exactly the capture policy.**
Self-service entry registration, self-withdrawal, and invite-email delivery
stay organiser-side actions in v1 (`RegisterCompetitor`/`WithdrawCompetitor`
are `Organiser` policy). Backlog stubs created in WI-11; none of them change
this story's shape.

**D8 — Production boots only under `oidc`.** `Composition.Build` throws at
startup when `Soarscore:Auth:Mode` is `none` (or unset) **or `mock`** while
`builder.Environment.IsProduction()` — the only Production-legal mode is
`oidc`. A release deployment that wanted to dodge auth gets a loud crash at
boot, not a silently open or mock-keyed API. "Release always applies AuthN
and AuthZ" is pinned three ways: this guard (and its composition test, WI-9),
the acceptance suite exercising the full enforcement path in `mock` mode on
every run (WI-10), and D11's rule that `mock` is the shipped code with a
local issuer — never a bypass. The two architecture tests that build
`Composition` in the default (Production) environment
(`RouteShapeTests.cs:22`, `HandlerRegistrationTests.cs:41`) gain
`--environment=Development` args.

**D9 — `GET /who-am-i` is in scope.** The SPA/BFF cannot derive the
Soarscore `PersonId` from the token (D2), and needs it to decide what to
render. One query, `WhoAmI : IQuery<CurrentUserView>`, `Authenticated`
policy, returning `PersonId` + roles for the current principal. This is the
one read that cannot be reconstructed from the JWT alone; NFR-3's
"no imagined features" does not apply to the identity bridge the front-end
flow requires.

**D10 — Capture policy shape and semantics.** Per-competition configuration,
stored on the Competition aggregate and projected into the competitions read
model:

```
enum CapturePolicyMode { OrganisersOnly, AnyRegisteredPerson, AllowList }
record CapturePolicy(CapturePolicyMode Mode, IReadOnlyList<PersonId> Capturers)
```

Evaluation for a capture command, in order (first match wins):

1. Not authenticated → `auth.notAuthenticated` (401).
2. Organiser role → **allow** (the organiser can always enter scores —
   "organisers can do everything", trust model).
3. Mode `OrganisersOnly` → deny `auth.capturePolicy.denied` (403).
4. No linked `PersonId` (authenticated at the IdP but not yet a known person)
   → deny `auth.capturePolicy.denied` — an unlinked identity is nobody.
5. Mode `AnyRegisteredPerson` → allow.
6. Mode `AllowList` → allow iff `Capturers` contains the acting `PersonId`,
   else deny.

The policy reads **only** competition configuration and the principal — never
task-round state, never what has been captured (NFR-4). A competition with no
configured policy evaluates as `OrganisersOnly` (the safe default; today's
single-operator behaviour). Reconfiguration is allowed at any time, including
mid-contest; it changes *who may enter*, never *what has been entered*.

**D11 — Mock OIDC (`mock` mode) is the shipped enforcement path with a local
issuer, plus dev personas.** Owner requirement (2026-09-14): a config item
must make OIDC mockable locally, and must make testing different roles easy —
without a bypass that tests something that never ships.

- **Validation is identical to `oidc`.** Same JwtBearer middleware, same
  audience check, same lifetime check, same pipeline, same policy table. Only
  two things differ: the issuer is local (a config-pinned signing key +
  static issuer instead of Auth0 domain discovery), and a dev-only seeder
  provisions personas. This is exactly the static-key path the acceptance
  suite already uses (WI-10) — `mock` promotes it from test plumbing to a
  first-class mode.
- **Config** (`dev/test only; refused in Production` per D8):

  ```json
  "Soarscore": {
    "Auth": {
      "Mode": "mock",
      "Mock": {
        "Audience": "soarscore-api",
        "SigningKey": "<base64, >= 32 bytes>",
        "Personas": [
          { "Name": "Pete", "Email": "pete@example.org", "Roles": ["Organiser"] },
          { "Name": "Tama", "Email": "tama@example.org", "Roles": ["Competitor"] },
          { "Name": "FieldRig", "Email": "rig@example.org", "Roles": [] }
        ]
      }
    }
  }
  ```

- **Persona seeding** — a hosted service registered only under `mock` (before
  the web host, like `ClassCorpusSeederHost`) creates each persona
  idempotently: `Person.Register` + `IdentityLinked(provider "mock", subject
  = persona slug)` + `RoleGranted` per configured role — real store events,
  exactly what an organiser would have appended. Re-running is a no-op for
  existing personas.
- **Token distribution** — the seeder prints each persona's bearer token to
  the console at startup; paste into Swagger UI (the bearer scheme from
  WI-9's OpenAPI transformer) or a curl header. No mock-token endpoint and no
  UI — NFR-3 headless holds; picking a persona for a run = choosing the
  config set before start (owner's "select a config item before run"), then
  picking the printed token.
- **Roles are never claims, not even in mock** (D2): a claim-based role
  override is explicitly rejected — it would test a lookup that never happens
  in production. Role differences come from the seeded store data, so
  switching personas locally exercises the real grant → resolve → enforce
  chain end to end.
- Persona tokens carry `sub = "mock|<slug>"` so the standard `provider|subject`
  parsing rule resolves the seeded identity link unchanged.

**D12 — Third-party integrations are machine actors (client credentials, no
delegation).** Question raised by the owner (2026-09-14): client-credentials
flow — delegate authorisation? Better way? Must integrators be OIDC?

- **No delegation.** A client-credentials token makes the *client application*
  the actor (`sub` = client id). Soarscore treats it exactly like any other
  principal: authority is expressed with the existing vocabulary — roles for
  administrative reach, and above all the capture-policy `AllowList` (a
  scoring-rig client is allow-listed on the competition it serves; a
  results-display client needs nothing beyond `Authenticated` reads, D4).
- **Identity** — M2M tokens carry `sub` **without** the `|` separator. The
  sub-parsing rule extends: `sub` containing `|` → interactive user
  (`provider|subject`); `sub` without `|` → provider = `client-credentials`,
  subject = the client id. One identity row per client; the existing
  resolution path is unchanged.
- **Provisioning** — the organiser binds the identity to a Person with the
  new `BindIdentity(PersonId PersonRef, string Provider, string Subject)`
  command (organiser policy, WI-7). The machine is just a person — "Field
  clock rig" — whose name/contact the club records. The same command also
  pre-provisions humans (organiser creates the account before the person ever
  signs in) and later serves account recovery.
- **Rejected: scope-in-token authorisation** (Auth0 `permissions`,
  `read:competitions`-style branching). It duplicates roles + capture policy
  as a second, token-resident authority, contradicts D2, and drifts from the
  event-store truth. Auth0 RBAC may still be used tenant-side to limit what
  tokens Auth0 will issue per client; the API keeps ignoring every claim
  beyond `sub`/`email`/`name`.
- **No new role.** Machine actors are persons; the v1 role set stays
  Competitor + Organiser (glossary as approved in WI-1).
- **Do integrators need to be OIDC?** No. They need to be **registered
  OAuth2 clients on our Auth0 tenant** (an M2M application): client-credentials
  grant against our domain, audience = our API identifier, HTTPS only. Any
  HTTP stack can do that — no IdP of their own is required. If an integrator
  later wants to federate their own IdP's users, that is token exchange
  (RFC 8693), which Auth0 supports — stubbed as future work (WI-11), not
  designed here.
- Client-secret vs private-key-jwt is Auth0-tenant configuration; the core is
  agnostic. Automating client registration via the Auth0 Management API is
  also stubbed (WI-11).

## Per-command policy table

The complete mapping — `AuthorizationPipeline`'s table is exactly this, and
the architecture totality test (WI-10) fails the build if a mapped command or
query is missing from it. Policy kinds: **A** = authenticated, **O** =
organiser role, **S** = self or organiser, **C** = capture policy (D10).

| Message | Policy | Notes |
|---|---|---|
| `RegisterPerson` | O | Manual pre-registration by an organiser. Self-service registration is `LinkSignIn`. |
| `RenamePerson`, `ChangePersonContactDetails`, `ChangePersonClubAffiliation` | S | Self-service for one's own record; organiser for anyone's. Commands already carry `PersonId Id`. |
| `PublishClassDefinition` | O | Class library is shared master data. |
| `CreateCompetition` … `RecordTieBreakOutcome` (all 19 competition commands: CreateCompetition, RegisterCompetitor, WithdrawCompetitor, DrawPhase, PrescribeDraw, AcceptDraw, RejectDraw, BindParameter, DeclareInstruments, CorrectInstrumentDeclaration, CompleteTaskRound, ReopenTaskRound, AnnulTaskRound, FinaliseCompetition, RecordCompetitionPenalty, AppendReflightGroup, AssignGroupSpots, RecordReflightRuling, RecordTieBreakOutcome) | O | Contest structure, rulings and CD authority — Organiser for v1 (Contest Director folded in). |
| All 7 team commands (`DefineScoringTeam` … `ConfigureTeamClassification`) | O | Competition configuration. |
| `OpenEntry`, `OpenFlight`, `CaptureMeasurement`, `AmendMeasurement` | C | The capture path. `OpenEntry` is competition-scoped; the other three are entry-scoped (resolve EntryRef → CompetitionRef via `IEntryQuery`). |
| `AnnulEntry`, `RecordEntryPenalty` | O | Rulings, not capture. |
| `LinkSignIn` | A | Any validated token; the handler does the get-or-create (D5). |
| `GrantRole`, `RevokeRole`, `ConfigureCapturePolicy`, `BindIdentity` | O | New commands (WI-7). |
| All 15 existing queries | A | D4. |
| `WhoAmI` (new) | A | D9. |

Marker interfaces carry the coordinates the policies need (implemented
explicitly by the existing records — no property renames):

```csharp
public interface ISelfPersonCommand        { PersonId PersonRef { get; } }      // PersonRef => Id
public interface ICompetitionScopedCommand { CompetitionId CompetitionRef { get; } }
public interface IEntryScopedCommand       { EntryId EntryRef { get; } }
```

## Work items

WI numbers are scoped to this plan. Each WI lists files, shapes, behaviour,
and its tests; "done when" is the acceptance bar. Dependencies: WI-1 → WI-2 →
(WI-3, WI-4) → WI-5 → (WI-6, WI-7) → WI-8 → WI-9 → WI-10 → WI-11.

### WI-1 — Owner sign-off of D1–D12 and the §Glossary-and-diagram-amendments text

No code. Walk the owner through the decision list and the proposed glossary,
class-diagram and users.md text. Record ticked decisions in this file (dated).
**Done when:** every D1–D12 is ticked (or amended with the owner's variant)
and the glossary text is approved or reworked.

### WI-2 — LADR-0004, glossary, class diagram, users.md

`docs/ladr/ladr-0004-authentication.md` — Status Accepted, follows LADR-0003.
Sections: Context (trust model amended 2026-09-14); Decision: Auth0 as IdP;
JwtBearer in the Api as the only token-validating layer; `ICurrentUser` port
in Application; enforcement in the Dispatcher pipeline via an explicit
per-command policy table; capture policy as competition configuration (D10);
identity resolution per request from the read model (D2); bootstrap (D3);
modes and the Production guard (D8); actor attribution deferred (D6).
Consequences + compliance mapping (layer rules, NFR-3/NFR-4 cross-refs).

`docs/soaring-domain-glossary.md` — append the three approved entries
verbatim from §Glossary-and-diagram-amendments.

`docs/soaring-domain-class-diagram.md` — Person gains `Roles` +
`Identities`; Competition gains `CapturePolicy`. Owner-approved in WI-1.

`docs/users.md` — apply the approved §Glossary-and-diagram-amendments §users
text (this discharges the flagged drift).

House-keeping rule 4: all four files change only with the WI-1 sign-off in
hand; the edits themselves are mechanical transcription of approved text.
**Done when:** LADR-0004 exists and the three docs match the approved text;
cross-references from CLAUDE.md's repository map need no change (LADR index
is the files themselves).

### WI-3 — Domain: Person roles and identity link

Files: `src/Soarscore.Domain/People/PersonEvents.cs`, `Person.cs`.

Events (change events carry no `Id` — stream-scoped, matching
`PersonRenamed`'s shape):

```csharp
public sealed record RoleGranted(PersonRole Role, DateTimeOffset At) : PersonEvent;
public sealed record RoleRevoked(PersonRole Role, DateTimeOffset At) : PersonEvent;
public sealed record IdentityLinked(string Provider, string Subject, DateTimeOffset At) : PersonEvent;
public enum PersonRole { Competitor, Organiser }   // plain enum, MinEnforcement precedent
```

State: `Person` gains `ImmutableHashSet<PersonRole> Roles` (default empty) and
`ImmutableArray<IdentityLink> Identities` (default empty),
`record IdentityLink(string Provider, string Subject)`; `Create` initialises
both empty — existing streams replay unchanged, no migration.

Folds: one `Apply` overload per new event + arms in the generic
`Apply(Person?, PersonEvent)` switch; `RoleGranted`/`RoleRevoked` add/remove
from the set; `IdentityLinked` appends unless an identical link already exists
(fold tolerance for a duplicated event).

Decides (WI-4-style single event or failure):

- `GrantRole(role, at)` — failure `person.roleAlreadyHeld` if held (the
  *handler* pre-checks for the idempotent bootstrap path; see WI-7).
- `RevokeRole(role, at)` — failure `person.roleNotHeld` if absent.
- `LinkIdentity(provider, subject, at)` — failure `person.identity.blank` if
  provider/subject blank; `person.identityAlreadyLinked` if an identical
  link already exists on this person.

Tests (`tests/Soarscore.Domain.Tests`): decide truth tables (grant → held →
refuse; revoke → absent → refuse; link → duplicate → refuse); fold tests
including replay of a stream with all three new events; the generic switch
dispatches them. **Done when:** green fast loop; `PersonEvent` union covers
the new events everywhere it is exhaustive (switch arms — the compiler's
exhaustiveness is the checklist).

### WI-4 — Domain: Competition capture policy

Files: `src/Soarscore.Domain/Competitions/Competition.cs`,
`CompetitionEvents.cs` (event union), new
`src/Soarscore.Domain/Competitions/CapturePolicy.cs`.

```csharp
public enum CapturePolicyMode { OrganisersOnly, AnyRegisteredPerson, AllowList }
public sealed record CapturePolicy(CapturePolicyMode Mode, IReadOnlyList<PersonId> Capturers)
```

Event `CapturePolicyConfigured(CapturePolicy Policy, DateTimeOffset At)` in
the Competition event union; state field
`CapturePolicy? CapturePolicy { get; init; }` beside
`TeamClassificationConfiguration` (`Competition.cs:557` — same precedent);
`Apply` sets it; generic switch arm added.

Decide `ConfigureCapturePolicy(policy, at)`: `AllowList` with an empty
`Capturers` list → `competition.capturePolicy.emptyAllowList`; non-`AllowList`
mode with a non-empty list → `competition.capturePolicy.capturersIgnored`
(strict — the client says what it means); reconfiguration always allowed
(latest wins in the fold; the log keeps the history). Person existence is NOT
checked here — `Competition` cannot read people; that is the handler's
cross-aggregate read (WI-7), the `CreateCompetition` precedent.

Tests: decide validation truth table; fold latest-wins; event JSON round-trip
in `CompetitionEventJsonTests`. **Done when:** green fast loop.

### WI-5 — Application: current-user port and the authorization pipeline

New folder `src/Soarscore.Application/Auth/`:

```csharp
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? Provider { get; }      // "google-oauth2", "auth0", …
    string? Subject { get; }       // IdP user id
    string? Email { get; }
    PersonId? PersonId { get; }    // null until linked
    IReadOnlyList<PersonRole> Roles { get; }
    bool HasRole(PersonRole role);
}

public sealed record SystemCurrentUser : ICurrentUser  // IsAuthenticated=true, seeded roles=[Organiser]; seeder host only (WI-9)
public sealed record AnonymousCurrentUser : ICurrentUser // registered under none-mode

public readonly record struct AuthzOutcome(bool Allowed, string? Code, string? Message)
{
    public static AuthzOutcome Allow();
    public static AuthzOutcome Deny(string code, string message);
}

public interface IAuthorizationPipeline
{
    Task<AuthzOutcome> AuthorizeAsync(object message, CancellationToken cancellationToken);
}

public interface ICommandPolicy   // per-kind implementations below
{
    Task<AuthzOutcome> AuthorizeAsync(object command, ICurrentUser user, IServiceProvider services, CancellationToken ct);
}
```

Policies (each ~15 lines, in `Auth/Policies/`):
`AuthenticatedPolicy`, `OrganiserPolicy` (deny `auth.forbidden`),
`SelfOrOrganiserPolicy` (command via `ISelfPersonCommand`; self = user's
`PersonId` equals `PersonRef`), `CapturePolicyPolicy` (D10 semantics; reads
the competition's policy via `ICompetitionsQuery`; resolves entry-scoped
commands via `IEntryQuery.FindAsync` — entry not found ⇒ allow through so the
handler produces the authoritative `*.notFound`, never a policy-invented 404).

The table — `Auth/CommandPolicyTable.cs` — is the literal, hand-written
`Dictionary<Type, ICommandPolicy>` from the §Per-command policy table (house
style: one explicit registration each, no assembly scanning). Totality is
enforced by test (WI-10), and the pipeline **fails closed**: a message type
absent from the table denies with `auth.policyMissing`.

`Dispatcher.cs` gains exactly the D1 step (resolve pipeline via `GetService`;
await `AuthorizeAsync`; on denial return `Result<TResult>.Failure(outcome.Code!, outcome.Message!)`
before handler resolution) and the header comment is amended per D1.

Ports added: `IPeopleQuery.FindIdentityAsync(provider, subject)` returning
`IdentityMatch?(PersonId, IReadOnlyList<PersonRole>)`;
`IPeopleQuery.CountByRoleAsync(PersonRole)`; `ICompetitionsQuery.FindCapturePolicyAsync(CompetitionId)`
returning `CapturePolicy?`; `IEntryQuery.FindAsync` gains an optional
`EntryId? entryRef = null` filter (fits the "every filter, all optional"
shape, `IEntryQuery.cs:6-8`). `CompetitionSummary` gains
`CapturePolicy? CapturePolicy` (positional-parameter append — update
`CompetitionProjection` construction and any test fixtures).

Tests (`tests/Soarscore.Application.Tests`): per-policy truth tables with
fakes; dispatcher denial blocks handler resolution (FakeEventStore stays
empty); registry absent ⇒ behaviour unchanged (the existing
`DispatcherTests` keep passing untouched). **Done when:** green fast loop;
`LayerRuleTests` untouched and green (Auth folder references Application +
Domain types only).

### WI-6 — Application: identity and role read models

Files: `src/Soarscore.Application/Queries/People/`.

`PersonSummary` gains `IReadOnlyList<PersonRole> Roles` (positional append;
update `PeopleProjection.Apply` — roles fold from `RoleGranted`/`RoleRevoked`
— plus `EventLogSummariser`/`FindByIdsAsync` consumers and test fixtures that
construct summaries positionally).

New `PersonIdentityRow(string Provider, string Subject, PersonId PersonId)` —
the projected row for one identity link, part of the **people** read model
(the ADR's four read models are not a fifth: LADR-0004 records the
clarification). Projection function `PersonIdentityProjection.Apply`:
`IdentityLinked` → row; a change event with no current row is the same
Require-error as `PeopleProjection`. Fed only by Person streams.

`PersonIdentityMatch(PersonId PersonId, IReadOnlyList<PersonRole> Roles)` —
the join the adapter returns from `FindIdentityAsync` (identity row + the
person's summary; two document reads, documented in the adapter).

Tests: projection folds (grant/revoke/link → summary roles + identity row);
`Require` semantics. **Done when:** green fast loop; `PersonSummary`
construction sites all compile (the compiler lists them).

### WI-7 — Application: the four new commands and one new query

New files under `src/Soarscore.Application/Commands/People/` and
`.../Commands/Competitions/`, `.../Queries/People/`:

- `LinkSignIn : ICommand<LinkSignInResult>` — **no body fields**; identity
  comes from `ICurrentUser`, never from request JSON (a caller can spoof a
  body, never the validated token). Result
  `LinkSignInResult(PersonId PersonId, bool PersonCreated)`. Handler flow per
  D5: resolve identity → hit: (bootstrap check: `AuthBootstrap` emails list
  contains `user.Email` ordinal-ignore-case and folded person lacks Organiser
  ⇒ decide `GrantRole` + append `Exact` — the handler has the folded state,
  so the decide's duplicate refusal cannot fire) return; miss: email lookup →
  hit: load that person's stream (fold + version), decide `LinkIdentity`,
  append `Exact`, bootstrap-check, return; miss: `PersonId.New()`, decide
  `Person.Register` (name from token `name` claim, defaulting to the email
  local-part; email required — no email claim ⇒ `auth.signIn.emailRequired`),
  decide `LinkIdentity`, bootstrap-check, append `NoStream` with all events
  in one call. On `eventStore.uniqueConstraintViolation`: re-run resolution
  **once** (D5 race), then propagate the failure. Constructor deps:
  `IEventStore, IPeopleQuery, ICurrentUser, IClock, AuthBootstrap` —
  `record AuthBootstrap(IReadOnlyList<string> OrganiserEmails)` in
  `Application/Auth/`, bound in Composition (WI-9).

- `GrantRole(PersonId PersonRef, PersonRole Role) : ICommand<PersonId>` —
  load, decide, append `Exact`. `RevokeRole(PersonId PersonRef, PersonRole Role)`
  — same, plus the last-organiser guard: when revoking `Organiser` from a
  holder, `IPeopleQuery.CountByRoleAsync(Organiser) == 1` ⇒ failure
  `person.lastOrganiser`. This is a cross-stream read guarding a UX deadlock,
  not an aggregate invariant (the `BindParameter` `roundHasEntries`
  precedent) — the comment says so, and the race (two concurrent revokes) is
  tolerated and documented.

- `ConfigureCapturePolicy(CompetitionId CompetitionRef, CapturePolicy Policy)
  : ICommand<CompetitionId>` — `AllowList` mode: every capturer `PersonId`
  must exist (`IPeopleQuery.FindByIdsAsync`; any missing ⇒
  `competition.capturePolicy.unknownPerson`) — a cross-aggregate read, the
  `CreateCompetition` precedent; then load, decide, append `Exact`.

- `WhoAmI : IQuery<CurrentUserView>` — `Authenticated`; returns
  `CurrentUserView(bool IsAuthenticated, PersonId? PersonId,
  IReadOnlyList<PersonRole> Roles, string? Name)` via the identity lookup.

- `BindIdentity(PersonId PersonRef, string Provider, string Subject)
  : ICommand<PersonId>` (D12) — organiser binds any external identity
  (interactive provider, or a client-credentials client id) to an existing
  person; also the pre-provisioning path for humans. Decide `LinkIdentity`,
  load, append `Exact`; the pipeline table entry is `Organiser`.

Tests (fakes per the house pattern): LinkSignIn all four paths + retry-once;
bootstrap grant idempotence; GrantRole/RevokeRole decides; last-organiser
guard; ConfigureCapturePolicy validation; BindIdentity decides; WhoAmI
unauthenticated shape. **Done when:** green fast loop; handlers follow the
read-fold-decide-append template with no auth logic inside (policies already
ran in the pipeline).

### WI-8 — Infrastructure: projections, indexes, adapters — both backends

Files: `src/Soarscore.Infrastructure/SoarscoreEventTypes.cs` (four new
`(typeof(Event), "camelCaseName")` entries: `roleGranted`, `roleRevoked`,
`identityLinked`, `capturePolicyConfigured`),
`People/PersonSummaryProjection.cs` (roles),
new `People/PersonIdentityProjection.cs` (+ shim),
`People/DocumentPeopleQuery.cs` (`FindIdentityAsync` two-lookup join,
`CountByRoleAsync`),
`Competitions/CompetitionSummaryProjection.cs` + `DocumentCompetitionsQuery.cs`
(capture policy),
`Entries/DocumentEntryQuery.cs` (entryRef filter),
`MartenConfig.cs` / `FisherConfig.cs` (register the identity projection;
**unique compound index on `(Provider, Subject)`** on `PersonIdentityRow` —
the link arbiter; mirror the people email unique index), and the store-level
event-type maps both backends share.

Store-backed tests (`tests/Soarscore.Infrastructure.Tests`, written once
against `IStoreFixture`, run against every backend): identity row round-trip;
duplicate `IdentityLinked` append → projection-time unique violation →
`eventStore.uniqueConstraintViolation` Result failure (the existing code at
`EndpointRouteBuilderExtensions.cs:72` maps it to 409); roles on the summary;
capture policy on the summary. **Done when:** the whole suite passes under
both `postgres` and `sqlite` (the SQLite run is the fast loop; postgres is
`Storage`-tagged).

### WI-9 — Api: bearer validation, current-user middleware, composition

Files: `src/Soarscore.Api/Composition.cs`, `appsettings.json`, new `Auth/`
folder, `wwwroot/integrators-guide.html`.

1. **Config** — `Soarscore:Auth:Mode` (`none` default; `none | mock | oidc`),
   `Soarscore:Auth:Domain`, `Soarscore:Auth:Audience`,
   `Soarscore:Auth:BootstrapOrganisers: []`, and the `Soarscore:Auth:Mock`
   block (D11: `Audience`, `SigningKey` base64 ≥ 32 bytes, `Personas`) — the
   static signing key is honoured under `oidc` too (test/acceptance pinning
   of `TokenValidationParameters` instead of Authority metadata retrieval),
   which is what makes `mock` and the acceptance suite the same mechanism.
2. **Startup validation (D8)** — Production + mode `none`/unset/`mock` ⇒
   throw with a message naming the config key (only `oidc` boots in
   Production). `mock` with a missing `SigningKey` ⇒ throw. `oidc` without
   Domain/Audience ⇒ throw. Unknown mode values ⇒ throw.
3. **Bearer wiring** — `AddAuthentication().AddJwtBearer(o => { o.Authority =
   $"https://{Domain}/"; o.Audience = Audience; o.MapInboundClaims = false;
   NameClaimType = "sub"; })` under `oidc`, or the static-key
   `TokenValidationParameters` (issuer + audience + symmetric key from the
   Mock block) under `mock`; `app.UseAuthentication()` in both.
4. **Current-user middleware** — after `UseAuthentication`, resolve scoped
   `HttpCurrentUser` and `Bind(context.User)`: parse `sub` on the first `|`
   (`provider|subject`; **no `|` ⇒ provider `client-credentials`, subject =
   the whole sub** — D12's M2M rule; malformed otherwise ⇒ principal stays
   unauthenticated — the pipeline then 401s), normalise provider to
   lower-case; `PersonId`/roles resolve lazily per request through
   `IPeopleQuery.FindIdentityAsync` and cache (one indexed lookup per
   request, D2). `UseAuthorization` is NOT added — there are no
   endpoint-level policies; the pipeline is the single enforcement point.
5. **Mock persona seeder (D11)** — hosted service registered only under
   `mock`, starting before the web host: seeds each configured persona
   (Person + identity link + role grants, idempotent, real store events) and
   prints each persona's bearer token to the console.
6. **Registrations** — `none`: `AnonymousCurrentUser` singleton, no pipeline
   enforcement (the pipeline bean may still be registered; the table lookup
   denies nothing because `AuthenticatedPolicy` is only reached under `oidc`
   — simplest: register `IAuthorizationPipeline` only under `oidc`/`mock`, so
   none-mode truly registers nothing). `oidc`/`mock`: `HttpCurrentUser`
   scoped, `IAuthorizationPipeline` scoped, `AuthBootstrap` from config,
   `SystemCurrentUser` **into the seeder host's scope** —
   `Seeding/ClassCorpusSeederHost.cs:55` resolves `IDispatcher` and dispatches
   `PublishClassDefinition`, which must keep working at startup in Production;
   the host registers `SystemCurrentUser` in the scope it creates (comment:
   system seeding is not a user request; D1/D3).
7. **Routes** — `MapCommand<LinkSignIn, LinkSignInResult>("/link-sign-in")`,
   `MapCommand<GrantRole, PersonId>("/grant-role")`,
   `MapCommand<RevokeRole, PersonId>("/revoke-role")`,
   `MapCommand<ConfigureCapturePolicy, CompetitionId>("/configure-capture-policy")`,
   `MapCommand<BindIdentity, PersonId>("/bind-identity")`,
   `MapQuery<WhoAmI, CurrentUserView>("/who-am-i")` — plus the five new
   handler/query registrations beside the existing blocks in `Composition.cs`.
8. **Status codes** — `StatusCodeFor` gains: `auth.notAuthenticated` → 401,
   `auth.forbidden` → 403, `auth.capturePolicy.denied` → 403,
   `auth.policyMissing` → 500 (fail-closed bug alarm — the totality test
   should make this unreachable),
   `person.lastOrganiser` → 409; `person.roleAlreadyHeld` → 409.
9. **OpenAPI** — a small document transformer adds the bearer security scheme
   so Swagger UI accepts a token; the Composition comment claiming "the trust
   model is the club-level no-auth one" is rewritten (it is stale the moment
   this story lands).
10. **Integrators guide** (`wwwroot/integrators-guide.html`, the single
    artifact, no markdown source) — gains a new section **"3. Authentication
    &amp; authorisation"** immediately after "2. Endpoint map": every call an
    integrator makes passes auth, so the first 401 must be explained before
    the use-cases. Sections 3–8 renumber to 4–9 (ids updated; the guide has
    zero internal `href="#…"` links — verified — so renumbering is headings +
    ids + the heading list only). Section content, in the guide's existing
    voice (tables, code blocks, `docs/rules/` untouched):
    - **Who is acting, in one screen** — a bearer JWT says *which* identity
      is calling (`sub`, `email`); who they are *to Soarscore* (PersonId,
      roles) is resolved from the event-store read model per request. Roles
      never live in tokens. 401 = no/invalid token; 403 = valid identity,
      refused policy.
    - **Error codes table** — `auth.notAuthenticated` → 401,
      `auth.forbidden` → 403, `auth.capturePolicy.denied` → 403,
      `auth.policyMissing` → 500, `person.lastOrganiser` → 409,
      `person.roleAlreadyHeld` → 409 (the codes WI-9 step 8 ships, so the
      guide and `StatusCodeFor` never disagree).
    - **Getting a token** — three recipes: (a) SPA/BFF redirect flow against
      Auth0 (audience = the API identifier; the API never sees a password);
      (b) Swagger UI's Authorize button (bearer scheme from step 9) with a
      pasted token; (c) machine clients — client-credentials against the
      tenant domain, `sub` without `|` = the client itself is the actor (D12).
    - **Actor vocabulary** — Roles (Competitor/Organiser, organiser-granted);
      capture policy (`OrganisersOnly` default | `AnyRegisteredPerson` |
      `AllowList`; organiser always passes; reconfigurable mid-contest);
      `/link-sign-in` get-or-create (identity link → email match → create);
      `/who-am-i` to resolve PersonId + roles.
    - **Integrator recipe (M2M)** — register the client on the Auth0 tenant →
      client-credentials token → organiser binds it with `/bind-identity` →
      competition allow-lists it via `/configure-capture-policy` → capture.
      Plus the note: *no scopes; authority is competition configuration, not
      token claims.*
    - §2's command/query tables gain the five new endpoints with one-line
      descriptions; §9 (renumbered Quick reference) gains the same plus a
      leading "authenticate first" line.

Tests: Architecture.Tests `RouteShapeTests`/`HandlerRegistrationTests` gain
`--environment=Development` (D8) and the mapped-message count bump; new
composition guard tests (Production + `none` throws; Production + `mock`
throws; `oidc` + missing Domain/Audience throws; `mock` + missing
SigningKey throws). **Done when:** the app boots in all three modes under
Development and only `oidc` under Production; `dotnet run` with no auth
config behaves byte-identically to today; `dotnet run` with `Mode=mock`
prints persona tokens and enforces every policy; the guide's new §3 matches
the shipped codes and endpoints (renumbered sections intact, zero broken
anchors).

### WI-10 — Tests: totality property, acceptance BDD

**Architecture totality test** (`tests/Soarscore.Architecture.Tests`): reuse
`HandlerRegistrationTests`' route enumeration — every mapped `ICommand`/`IQuery`
message type must be a key in `CommandPolicyTable.Table`. Fail-closed has a
backstop in the pipeline; this test makes the backstop unreachable.

**Named property invariants** (CsCheck, per CLAUDE.md's planning rule):

- *Enforcement totality:* for every message type in the table and every
  generated principal state (anonymous / authenticated-unlinked / competitor /
  organiser / allow-listed / system), `AuthorizeAsync` is total — never
  throws, never returns an outcome that is neither Allow nor Deny — and
  **anonymous ⇒ deny for every message type**. Expressed as a CsCheck
  property over the generated principal × table cross-product; the
  anonymous-arm is the auditability-critical half.
- *Capture-policy temporal blindness (NFR-4):* the capture decision for a
  (principal, competition) pair is invariant under any insertion, removal or
  reordering of score-capture events in the competition's other streams — the
  policy reads configuration, never captured state. Property: generate
  arbitrary captured-data states, assert identical outcomes.

**Acceptance BDD** (`tests/Soarscore.Acceptance.Tests`): the auth-enabled
fixture instance starts the factory in **`mock` mode** (D11 — the same
mechanism local development uses, which is the point: the suite proves the
shipped enforcement path). Fixture support — a `TestJwt` helper mints HS256
JWTs (system-identity-model tokens, package already transitively present via
JwtBearer; pin explicitly in `Directory.Packages.props` per CPM) with claims
`sub` (`mock|<persona>` for people, bare client ids for machine actors),
`email`, `name`; the factory runs with `Soarscore:Auth:Mode=mock`, the
fixture's `SigningKey`, matching `Audience`, and a persona set that covers
organiser / competitor / unlinked / machine actor. Existing scenarios stay on
the none-mode factory instance — zero churn.

New features (Reqnroll, driven through the real HTTP surface):

- `SigningIn.feature` — anonymous POST is 401 (ProblemDetails
  `auth.notAuthenticated`); first sign-in creates the person and links
  (201-shape 200 + `personCreated: true`); repeat sign-in is idempotent; a
  second provider with the same email links to the existing person;
  bootstrap-listed email lands with the Organiser role.
- `Roles.feature` — organiser grants and revokes Competitor; competitor
  attempting `GrantRole` gets 403 `auth.forbidden`; revoking the last
  organiser fails `person.lastOrganiser`; a revoked competitor loses
  self-service edits (`SelfOrOrganiser` self-half still works, organiser-half
  denied).
- `CapturePolicy.feature` — default policy denies a competitor's
  `CaptureMeasurement` (403) but allows the organiser's; organiser configures
  `AllowList` naming the competitor; the competitor now captures (200); a
  non-listed person still denied; late/out-of-order capture after
  reconfiguration behaves exactly as capture always did (NFR-4 — a capture
  arriving for an earlier round is recorded, never gated on round currency).
- `Integrations.feature` (D12) — a machine token (sub without `|`) is
  unlinked until an organiser calls `/bind-identity`; after binding and
  allow-listing, the machine client captures measurements (200); the same
  machine token without allow-listing is denied (403); the machine can read
  results (`Authenticated` queries, D4).

Run both stores (`SOARSCORE_TEST_STORE=postgres` and `=sqlite`).
**Done when:** all features green on both stores; existing suites untouched
and green.

### WI-11 — Housekeeping and completion

- Backlog stubs (house-keeping rule 6): `event-actor-attribution.md` (D6, both
  options sketched), `self-service-competitor-actions.md` (D7: self-entry,
  self-withdrawal), `public-read-surface.md` (D4 opt-outs), `invite-email-delivery.md`
  (organiser invite → email), `unlink-identity-and-account-recovery.md`,
  `scoped-read-policies-for-integrations.md` (D12: per-client read scoping —
  a results client currently sees everything an authenticated principal
  sees), `token-exchange-federation.md` (RFC 8693 — integrators federating
  their own IdP), `auth0-client-registration-automation.md` (Management API —
  registering M2M clients without dashboard clicks).
- `tech-debt.md`: the last-organiser guard's read-model count is
  race-tolerant (documented in WI-7); the `PersonSummary` positional append
  ripples into fixtures (compile-time, but noted).
- `deferred-decisions.md`: CORS for a direct-SPA (non-BFF) caller — deferred
  with the front-end story; Swagger UI stays anonymous under `oidc` in v1.
- Move this story to `completed/`, set the status header, reconcile the two
  inventories. **Done when:** board and inventories agree with the tree.

## Glossary-and-diagram-amendments (proposed text for WI-1 approval)

**Glossary entries:**

> **Identity link** — the binding between one external identity-provider
> account (a provider and its subject id) and exactly one Person. A Person
> may hold several identity links (several social accounts, or a social
> account and a magic-link email); a link belongs to exactly one Person.
> Established at first sign-in or by an organiser.

> **Role** — a system-level authority held by a Person, independent of any
> competition. For v1: *Competitor* and *Organiser*; the Contest Director's
> authority folds into Organiser. Roles are granted and revoked by organisers.

> **Capture policy** — per-competition configuration naming who may enter
> which scores for that competition: organisers only, any registered person,
> or an explicit allow-list of People (organisers always pass). The core
> interprets the policy generically; it never branches on who is acting
> beyond applying it.

**Class diagram:** `Person` gains `Roles: PersonRole set` and
`Identities: IdentityLink set`; `Competition` gains
`CapturePolicy?: CapturePolicy`. No new aggregates; `IdentityLink` and
`CapturePolicy` are value objects owned by their aggregate.

**users.md:** replace the §5 paragraph "That is a statement about the roles at
a contest … neither enables nor forbids" with: *"Whether a given person may
enter scores is **per-competition capture policy** — competition configuration
decided by the organiser (owner decision 2026-09-14). The default policy keeps
score entry with the organisers; a competition may widen it to named scorers
or to every registered person. The 'Multiple Hats' rule stands unchanged: the
software's Organiser role covers the Contest Director's officiating authority
for v1, and who may *enter* scores is governed by the competition's capture
policy, not by the role a person played at the contest."*

## Cross-checks (house-keeping rule 2)

- **NFR-3** — auth adds a transport concern (JwtBearer, Api only), one
  Application port (`ICurrentUser`), and policy interpretation in the
  Application layer. No UI, no UX assumptions; the SPA/BFF remains a separate
  consuming system. `/who-am-i` exists because the front-end flow (owner
  decision, 2026-09-14) requires the identity bridge.
- **NFR-4** — the capture policy gates *who*, never *when*: D10's evaluation
  reads configuration and principal only; the WI-10 temporal-blindness
  property pins it. No capture command gains a precondition on round state.
- **NFR-1/NFR-2, architectural law** — capture policy is competition-level
  configuration, not class data; no class-specific branch appears anywhere in
  this plan; `ClassAgnosticismTests` untouched.
- **LADR-0001** — no new read model *count* (identity rows belong to the
  `people` read model, recorded in LADR-0004); ports gain methods, not new
  abstractions; the unique index remains the arbiter — no read-check-write on
  aggregate invariants (D5's single bounded retry is resolution, not
  enforcement).
- **LADR-0003** — new packages: `Microsoft.AspNetCore.Authentication.JwtBearer`
  (in-box ASP.NET family) + its `Microsoft.IdentityModel.*` token dependencies,
  pinned via CPM and licence-checked by the existing CI step. No other new
  library.
- **Rules** — none of the rule corpus touches authentication or capture
  authority; no rule cross-reference is owed.
