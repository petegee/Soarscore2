# LADR-0003 — Library choices

**Status:** Accepted · **Date:** 2026-08-03 · **Follows:** LADR-0002

Settled elsewhere, not re-argued here: event store = Marten/PostgreSQL (LADR-0001);
class definitions = canonical JSON, C# records at build time, no code execution (LADR-0002).

## Choices

| Concern | Choice | Why | Rejected |
|---|---|---|---|
| Framework / language | .NET 10 LTS, C# 14 | LTS survives a fork left alone for two years; records/`required`/collection expressions do the functional-like work | — |
| Project layout | Domain / Application / Infrastructure / Api; **Domain has zero `PackageReference`** | Cheapest enforcement of "Domain has NO dependencies" — checkable by eye | clean-architecture templates (ship `BaseEntity` + generic repo + AutoMapper) |
| Layer enforcement | NetArchTest.Rules, one test file (~20 lines) | Catches `using Soarscore.Infrastructure` in Domain at build | ArchUnitNET (see Open) |
| Web / API host | ASP.NET Core Minimal APIs; `MapCommand`/`MapQuery` helpers only + a reflection test asserting every route ∈ {GET, POST} | Controllers pull toward noun-resources; helpers turn the intent-based rule into a failing build | MVC controllers, FastEndpoints (opinionated) |
| Query binding | `[AsParameters]` record structs | Query-string binding is explicit, not conventional | request bodies on GET |
| Errors | `ProblemDetails` (RFC 9457) via `IProblemDetailsService` | In-box | — |
| Command/query dispatch | **Hand-rolled** `ICommandHandler`/`IQueryHandler` + `IDispatcher` over `IServiceProvider`, ~60 LOC | Zero licence risk, more inspectable than a behaviour pipeline | **MediatR — commercial licence v12+**; Wolverine (see Open) |
| Event store + persistence | Marten 9.22.x / PostgreSQL 15+ — **MIT** (verified 9.22.2, 31 Jul 2026) | LADR-0001 | EventStoreDB/Kurrent (relicensed off OSI terms); hand-rolled SQLite (LADR-0001 §1) |
| Serialisation | `System.Text.Json` + source generation (`GenerationMode = Metadata`); hand-written `[JsonPolymorphic]` discriminators under **`$kind`**, `AllowOutOfOrderMetadataProperties` on ingestion | A namespace rename must never orphan historical events; the two options are the spike's, below | `Newtonsoft.Json`, `TypeNameHandling`, CLR-type-derived discriminators, custom converters for the term hierarchies |
| Validation | Hand-written `Validate()` → `IReadOnlyList<Defect>`, total, non-throwing, one method per numbered check | The 16 checks are graph-wide and cross-referential; per-property fluent validators are the wrong shape and invite class-specific rules into a validator | **FluentValidation — licence shift v12** and wrong shape; JSON Schema as the adoption gate (shape only) |
| Authoring aid | JSON Schema generated from the model | Editor completion for class authors; not a validation gate | — |
| DI | `Microsoft.Extensions.DependencyInjection` | Autofac/Lamar buy nothing at this scale | Autofac, Lamar |
| Configuration | `IConfiguration` + environment variables | Two containers | Consul, feature-flag services, .NET Aspire |
| Logging | `ILogger` + Serilog (Apache 2.0) console/file sinks | Structured output without a platform | — |
| Observability / metrics | **None initially.** OpenTelemetry deferred | Single-digit writes/minute; pure ceremony at this rate | — |
| API documentation | `Microsoft.AspNetCore.OpenApi` (in-box, .NET 9+); spec emitted as a build artefact | Integrators need the spec, not a UI (NFR-3) | Swashbuckle, NSwag, Scalar, any hosted UI |
| Test runner | xUnit v3 | Parallel by default — matches independent/isolated | — |
| Assertions | Shouldly *or* AwesomeAssertions (MIT fork of the FluentAssertions v7 API) | **FluentAssertions v8 is paid for commercial use** | FluentAssertions v8+ |
| Test entry point | `IDispatcher` with real handlers | "Driven without HTTP tools" is satisfied by making the Application layer the seam | `WebApplicationFactory`, Alba |
| Containers / test data | Testcontainers for Marten-backed tests, disposed per class; bulk of tests on the lighter store | Real Postgres where it matters, honestly | shared dev database |
| Snapshot / approval | Verify | A round's score sheet as an approved file — NFR-2's "existing results unaffected" made executable | — |
| Property-based | Not evaluated (see Open) | — | — |
| Doubles | Hand-written fakes for clock and ID generator (three lines each); NSubstitute only at genuine external boundaries | Mocks double-encode structure the tests must not know | Moq (SponsorLink incident) |
| Coverage | `dotnet test --coverage` (Microsoft.Testing.Platform) | In-box | Coverlet + ReportGenerator chain |
| Build discipline | `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true` | C# has no non-nullable guarantee; warnings-as-errors is the substitute | — |
| Package management | Central Package Management (`Directory.Packages.props`), all versions pinned | Given how many .NET staples relicensed in 2024–25 | floating versions |
| Licence hygiene | CI step: `dotnet list package --include-transitive` through a licence checker | Automate the constraint rather than trust it | — |
| Vocabulary discipline | `PublicAPI.Shipped.txt` baseline on the authoring types | Makes "a term type is admitted only when a rule requires it" a build failure on an unreviewed diff | prose-only enforcement |
| Domain primitives | `readonly record struct EntryId(Guid)` etc.; `Guid.CreateVersion7()`; `decimal` never `double`; `System.Collections.Immutable` | Kills argument-order bugs; time-ordered IDs; binary FP eventually publishes 999.9999999 | — |
| Result type | Hand-rolled `Result<T>`, ~80 lines | Lean and un-opinionated | LanguageExt (a second language in the codebase); CSharpFunctionalExtensions (defensible, unnecessary) |
| Closed vocabulary | `abstract record ScoreTerm { private ScoreTerm() {} }` + nested subtypes | Makes `switch` missing-arm warnings meaningful — NFR-2's closure in the type system | open class hierarchies |

## Deliberately not used

- **AutoMapper** — commercial licence, and layer-crossing mapping is explicit by hand here.
- **EF Core / any ORM** — events are the state; nothing to map.
- **Rules engine** (NRules, RulesEngine, Roslyn/Lua scripting) — an open expression language destroys static validation (NFR-2).
- **Marten async projection daemon** — never started; Inline is required by the Person-email uniqueness invariant (LADR-0001 §2).
- **Marten document store for aggregates** — would reintroduce state storage.
- **IoC-heavy / opinionated frameworks** (.NET Aspire, FastEndpoints, clean-architecture scaffolds) — conventions leak into the domain.
- **Snapshots** — streams are 20–60 events; folding is sub-millisecond (LADR-0001 §2).

## Open

- Shouldly vs AwesomeAssertions — pick one after verifying the licence terms in force.
- NetArchTest.Rules vs ArchUnitNET — tie unbroken.
- **Wolverine** (MIT, Critter Stack) — a coherent pairing now that Marten is chosen (mediator + outbox + handlers returning events); never formally closed against the hand-rolled dispatcher.
- Property-based testing (FsCheck/CsCheck) — never evaluated for the scoring pipeline.
- Serilog vs in-box `ILogger` only — one fewer dependency is the counter-argument.
- OpenTelemetry — deferred, not rejected; trigger is the shared cloud instance needing it.
- The licence-checker tool itself is unnamed.

## Closed — `System.Text.Json` and the class-definition hierarchy

Spiked in `spike/ClassJsonSpike/` (F3K and F5K transcribed and round-tripped;
`FINDINGS.md` has the detail). **Attributes, not a custom converter.**
`[JsonPolymorphic]` + `[JsonDerivedType]` carries `ScoreTerm`, `Predicate` and
`FlightSelection` with no hand-written serialisation code, round-tripping
byte-identically, source generation agreeing with reflection in both directions.

The question was posed as converter vs hand-written `$type` throughout the seed
JSON, and half of it had already dissolved: LADR-0002 §1–2 make the seed JSON
*machine-emitted from C# records*, so nobody hand-writes a discriminator and the
verbosity was never a cost anyone pays. Its stated fallback — a small parser for
`competition-class-notation.md` — is separately closed by LADR-0002 §3.

Three amendments the spike forced, two of them silent failures:

- **`$kind`, not `kind`.** A discriminator that shadows a real property emits
  *both* keys (`{"kind":"fixed","kind":"Fixed"}`) with no error, no warning and
  no build failure; it is written, hashed and stored, and fails only on read
  back. Six records already carry a `Kind`; the diagram records that splitting
  `TaskTiming` (§3) and `PromotionRule` (§2) into subtypes was *considered and
  declined*, so either decision reversed and this fires.
- **`AllowOutOfOrderMetadataProperties = true` on ingestion.** A document
  containing the discriminator but not as the object's first property is
  rejected, with a message naming a property the document already has. Any
  key-sorting formatter produces one, as does a class author writing
  `{"metricRef": …, "$kind": "rate"}`. Canonical order is provably recoverable
  once the option is set.
- **Round-trip tests compare bytes, not records.** `ImmutableArray<T>.Equals` is
  reference-based, so LADR-0002 §6's test fails on every definition for a reason
  unrelated to serialisation. Consistent with §5 making the content hash the
  identity.

Two further things asserted here without evidence, now checked: source
generation composes with the polymorphic hierarchies **only** under
`GenerationMode = Metadata` and with every derived type given its own
`[JsonSerializable]` (the generator does not walk `[JsonDerivedType]`, and the
omission fails at run time, not at build); and deserialisation does enforce the
closed vocabulary on untrusted input as LADR-0002 §4 claims — unknown and missing
discriminators, missing `required` members and over-deep payloads are all
rejected. It does not catch a `ParameterRef` in a non-admitted slot, which is
type-correct and belongs to `Validate()`.

Nesting is shallower than the question assumed: max depth **9** (F3K) and **11**
(F5K) against `MaxDepth` 64, so §4's depth limit can sit near 24. `ScoreTerm`'s
closure does not depend on nesting the subtypes inside the base — a
`private protected` constructor with top-level subtypes closes the hierarchy to
other assemblies and serialises identically.

Retained without being required: the ~60 lines of converter for
`NumberOrParam`/`FlagOrParam` in `Json.cs`. The tagged form round-trips
identically, so this is a presentation choice about the reviewable seed corpus —
it collapses thirteen slots of `{"kind":"literal","value":599}` to `599`, worth
9–19 % of the artefact. Reversible.
