# LADR-0003 — Library choices

**Status:** Accepted · **Date:** 2026-08-03 · **Follows:** ADR-0002

Settled elsewhere, not re-argued here: event store = Marten/PostgreSQL (ADR-0001);
class definitions = canonical JSON, C# records at build time, no code execution (ADR-0002).

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
| Event store + persistence | Marten 9.22.x / PostgreSQL 15+ — **MIT** (verified 9.22.2, 31 Jul 2026) | ADR-0001 | EventStoreDB/Kurrent (relicensed off OSI terms); hand-rolled SQLite (ADR-0001 §1) |
| Serialisation | `System.Text.Json` + source generation; hand-written `[JsonPolymorphic]` discriminators | A namespace rename must never orphan historical events | `Newtonsoft.Json`, `TypeNameHandling`, CLR-type-derived discriminators |
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
- **Marten async projection daemon** — never started; Inline is required by the Person-email uniqueness invariant (ADR-0001 §2).
- **Marten document store for aggregates** — would reintroduce state storage.
- **IoC-heavy / opinionated frameworks** (.NET Aspire, FastEndpoints, clean-architecture scaffolds) — conventions leak into the domain.
- **Snapshots** — streams are 20–60 events; folding is sub-millisecond (ADR-0001 §2).

## Open

- Shouldly vs AwesomeAssertions — pick one after verifying the licence terms in force.
- NetArchTest.Rules vs ArchUnitNET — tie unbroken.
- **Wolverine** (MIT, Critter Stack) — a coherent pairing now that Marten is chosen (mediator + outbox + handlers returning events); never formally closed against the hand-rolled dispatcher.
- Property-based testing (FsCheck/CsCheck) — never evaluated for the scoring pipeline.
- Serilog vs in-box `ILogger` only — one fewer dependency is the counter-argument.
- OpenTelemetry — deferred, not rejected; trigger is the shared cloud instance needing it.
- **`System.Text.Json` polymorphic deserialisation of the deep `ScoreTerm`/`Predicate` hierarchy** — spike F3K or F5J before committing; custom converter vs explicit `$type` throughout the seed JSON is unresolved.
- The licence-checker tool itself is unnamed.
