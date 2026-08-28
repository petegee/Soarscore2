# Graph Report - SoarScore2  (2026-08-26)

## Corpus Check
- 461 files · ~702,558 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 6055 nodes · 15981 edges · 296 communities (289 shown, 7 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 1790 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ddb8f5bb`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- .ScoreCompetition
- Soarscore.Domain.PublishedClassDefinition
- .BuildCompetitionWithPenalties
- FakeEntryQuery
- SeedF5J
- .HandleAsync
- IServiceProvider
- Soarscore.Domain.Competitions
- CatalogueDrawPropertyTests
- .CheckLimits
- ResolvedTask
- .Of
- PrescribeDrawDecideTests
- .Aggregate
- .New
- CompetitionId
- PostgresFixture
- EntryId
- ICommandHandler
- PhaseDrawn
- NumberOrParam
- CompetitionSummary
- ClassDefinition
- MeasuredValue
- Soarscore.Application.Commands.CompetitionClasses
- ClassAgnosticismTests
- EntryCapturePropertyTests
- DrawAcceptanceEventStoreTests
- Then
- .New
- FixtureModels.cs
- .Exact
- Entry
- 3.16 CLASS Q: NZ F5K (Hand Launch Electric Glider)
- .CompareRawGrainAsync
- FlightOpened
- ReflightingAGroupSteps
- AcceptingTheDrawSteps
- ScoringACompetitionSteps
- AmendMeasurementDecideTests
- SystemClock
- Work items
- Plan — Capturing a score: the Entry write path and `entry_index`
- Plan — Scoring: de-orphaning the scoring engine
- .BuildDispatcher
- The Competition Class notation — draft spec
- TaskRoundRecordingPropertyTests
- CaptureMeasurement
- Competition
- Comparison
- RecordingAReflightRulingSteps
- 2 SOARING (All Classes)
- B.4 DEFINITIONS OF EXPRESSIONS
- .SeedCompetition
- .ScoreGroup
- FinaliseDecideTests
- ClassDefinitionSummary
- Scoring Service Build Plan
- .SeedOpenEntry
- Enumerations.cs
- Work items
- .AddSoarscoreInfrastructure
- .HandleAsync
- GliderscoreFixture
- Plan — Catalogue-choice draws: the CD picks each round's task
- TaskRound
- Soarscore.Application.Queries.Competitions
- PenaltyDefinition
- .Normalise
- .Rank
- Work items
- .MapQueries
- .CompetitionAdopting
- 3.4 CLASS D : THERMAL FORMULA 500
- Plan — The CD's choices: `BindParameter`
- Context
- EndpointRouteBuilderExtensions
- CompetitorId
- PersonRegistered
- .BuildDrawnCompetition
- TaskRoundCompleted
- .BuildDrawnCompetition
- Work items
- .ComputeEntries
- Soarscore.Application
- .New
- .Validate
- Core Principles
- 00-general-rules.md
- RC Soaring Competitions — Key Concepts
- Plan — Command-side steel thread: Person end-to-end
- HarnessSelfCheckSteps
- .DrawnCompetitionAsync
- ExpectedVersion
- .Select
- Work items
- DrawAcceptanceDecideTests
- 5.5.10 F5K – RC THERMAL DURATION GLIDERS FOR MULTIPLE TASK COMPETITION WITH
- Work items
- Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor`
- .EvaluateTerm
- MetricDefinition
- SeedF5K
- F3F.1 GENERAL RULES
- CompetitionReplaceTaskRoundPropertyTests
- Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition`
- JasperFxEventStore
- PublishClassDefinition
- SeeingWhatIsRecordedSteps
- .SeedCompetition
- 6 F3L – RADIO CONTROLLED THERMAL GLIDERS RES
- 5.5.11 CLASS F5J – RC ELECTRIC POWERED THERMAL DURATION GLIDERS
- 5.5.12 CLASS F5L – RADIO CONTROLLED THERMAL GLIDERS RES WITH ELECTRIC MOTOR AND
- MartenEventStore
- FisherEventStore
- Work items
- LADR-0001 — Event store: PostgreSQL + Marten
- SECTION C - CIAM GENERAL RULES FOR INTERNATIONAL EVENTS
- C.15 ORGANISATION OF WORLD AND CONTINENTAL CHAMPIONSHIPS
- DrawAcceptancePropertyTests
- Plan — Create-competition steel thread: `CreateCompetition`
- PhaseDefinition
- .HandleAsync
- 2. Findings
- ReflightRule
- RecordEntryPenaltyDecideTests
- Rule map — topic × class
- SECTION A - CIAM INTERNAL REGULATIONS
- .LoadCurrentAsync
- PublishedClassDefinition
- F3K.11 DEFINITIONS OF TASKS
- .LoadCurrentAsync
- Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server
- WithdrawCompetitorHandler
- .LoadCurrentAsync
- ClassDefinitionPublished
- 1 GENERAL DEFINITIONS
- Defect
- FixtureLoader
- DrawingACatalogueChoicePhaseSteps
- CaptureMeasurementDecideTests
- validate.py
- F3G.1 GENERAL RULES
- Gliderscore Jet DB — Schema Analysis and Indicative Domain Mapping
- Story — Gliderscore golden-fixture pipeline
- FakeEventStore
- GroupId
- ReplaySteps
- IStoreFixture
- EntryModelBasedFoldTests
- CLAUDE.md — Soarscore
- fai-rule.sh
- 00-nz-general-rules.md
- F3B.2 RULES FOR MULTI-TASK CONTESTS
- F3B.1 GENERAL RULES
- F3J.10 SCORING
- PART 5 – TECHNICAL REGULATIONS FOR RADIO
- Plan
- Plan
- Story — Move the store adapters onto the JasperFx shared contracts
- Raw score
- Result
- GliderScore fixture extraction
- AnnulEntryDecideTests
- Story — GliderScore webmine tool (read-only online comp acquisition)
- OpenFlightDecideTests
- .SetUpAsync
- NZ Class M — ALES 200 (Altitude Limited Electric Soaring)
- NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)
- 3.17 CLASS R: E-RES 2M (Electric Rudder Elevator Spoiler 2M Glider)
- ReflightSelection
- F3G.2 RULES FOR MULTI-TASK CONTESTS
- Soarscore — Users
- Drop-worst
- FakeEventStore
- When
- PersonSummary
- LADR-0002 — Competition Class definition: representation, ingestion and identity
- DateTimeOffset
- F5K — RC Electric Thermal Duration, Multiple-Task
- NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)
- .BuildDrawnCompetition
- C.16.2 Requirements for radio control
- C.2.1 First category events
- 5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS
- Plan
- Scoring Service — Open Design Issues
- Soarscore.Acceptance.Tests.csproj
- Soarscore.sln
- IProjection
- F3 Soaring — Generally Applicable Rules
- Soarscore.Infrastructure.Tests.csproj
- non-functional-requirements.md
- Competition rules for RC soaring
- Competition Rules — Generally Applicable (all contest types)
- DocumentClassLibraryQuery
- ReflightRole
- C.13 REQUIREMENTS FOR ORGANISATION OF INTERNATIONAL EVENTS
- C.21 CIAM TROPHIES
- C.5.1 Competitor
- 4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS
- 5.5.3 CLASS F5A – RC ELECTRIC POWERED GPS MOTOR GLIDERS (PROVISIONAL RULE)
- 5.5.4 CLASS F5B – RC ELECTRIC POWERED MULTI TASK GLIDERS
- ClassDefinitionValidationPropertyTests
- Soarscore.Infrastructure.Tests
- .Apply
- Entry-completeness indicator
- Plan
- Ranking & tie-breaks
- Deferred decisions
- ComparisonReport
- DecideFlightModel
- ScoringCorpusPropertyTests
- Compliance check
- Model
- F5J — RC Electric Powered Thermal Duration Gliders
- RC Soaring Competitions — Domain Class Diagram
- DocumentPeopleQuery
- F5L — RC Electric Thermal Gliders, RES
- NZ Soaring — Generally Applicable Rules
- 3.10 CLASS K: Thermal R.E.S. (Rudder, Elevator, Spoiler)
- 3.12 CLASS M: ALES 200 (Altitude Limited Electric Soaring)
- C.18 SAFETY
- EntryEventJsonTests
- 5.5.1 GENERAL RULES
- Work items
- Plan — Per-round parameter bindings
- Remove `Flight.LaunchAt`
- Precision & storage
- Plan
- Soarscore.Architecture.Tests.csproj
- .ApplyRounding
- Soarscore.Application.Tests.csproj
- ResultTests
- .New
- Soarscore.Domain.Tests.csproj
- RC Soaring — Aggregate Boundaries
- 2.5 CONTESTS
- 2.4 LANDING
- 3.3 CLASS C: PREMIER THERMAL DURATION.
- F3K — RC Hand-Launch Gliders
- C.20 COMPLAINTS AND PROTESTS
- F3K.2 DEFINITION OF MODEL GLIDER
- F3K.9 DEFINITION OF A ROUND
- 5.5.2 CONTEST RULES
- Normalisation
- Soarscore.Application.csproj
- Soarscore.Infrastructure.csproj
- TaskDefinition
- BindParameterPropertyTests
- .Apply
- F5 Electric Soaring — Generally Applicable Rules
- .BuildDispatcher
- .Decide
- IDomainEvent
- F3B — RC Multi-Task Gliders
- LayerRuleTests
- LADR-0003 — Library choices
- 3.1 CLASS A: 6 MINUTE THERMAL DURATION
- 3.7 CLASS H : NEW ZEALAND THERMAL 2 METRE RULES
- C.19.1 Penalties imposed by the Contest Director
- C.7 CONTEST OFFICIALS
- CreateCompetitionPropertyTests
- Story — Coverage: normalisation is per group, not per round
- Stop storing `Entry.WorkingTime`
- Competitor
- IClassFixture
- .HandleAsync
- F3J.8 LAUNCHING
- SoarscoreEventTypes.cs
- IClock
- 3.2 CLASS B: 10 MINUTE THERMAL DURATION
- 3.9 CLASS J: THERMAL 2,4,6,8,10
- F3K.10 SCORING
- F3K.4 SAFETY
- Corpus.cs
- Story — Resolve GliderScore scoring arithmetic from source
- Findings
- F3J.11 FINAL CLASSIFICATION
- F3J.1 GENERAL RULES
- F3J.2 THE FLYING SITE
- F3J.6 ORGANISATION OF THE FLYING
- F3J.13 ADVISORY INFORMATION
- extract.py
- GliderScore fixture corpus index
- F3J.9 LANDING
- Seed classes — the authoring source
- C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY
- C.15.6 Classification
- F3K.1 GENERAL
- 5.5.11.1 General Rules
- opencode.json
- Story — Smaller items
- .mcp.json
- graphify.js
- tech-debt.md

## God Nodes (most connected - your core abstractions)
1. `CompetitionId` - 183 edges
2. `CompetitorId` - 172 edges
3. `ClassDefinition` - 168 edges
4. `Soarscore.Domain.PublishedClassDefinition` - 164 edges
5. `Soarscore.Domain.Competitions` - 133 edges
6. `Result` - 119 edges
7. `EntryId` - 94 edges
8. `Soarscore.Domain.People` - 91 edges
9. `Soarscore.SeedData` - 90 edges
10. `Soarscore.Domain` - 89 edges

## Surprising Connections (you probably didn't know these)
- `AcceptanceFixture` --references--> `Program`  [EXTRACTED]
  tests/Soarscore.Acceptance.Tests/Support/AcceptanceFixture.cs → src/Soarscore.Api/Program.cs
- `PhaseDrawPropertyTests` --references--> `Rounds`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/PhaseDrawPropertyTests.cs → src/Soarscore.Domain/Competitions/Competition.cs
- `EntryCapturePropertyTests` --references--> `Kind`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/EntryCapturePropertyTests.cs → src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs
- `CatalogueDrawPropertyTests` --references--> `MinPerGroup`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/CatalogueDrawPropertyTests.cs → src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs
- `CatalogueDrawPropertyTests` --references--> `Selection`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/CatalogueDrawPropertyTests.cs → src/Soarscore.Domain/Shared.cs

## Import Cycles
- None detected.

## Communities (296 total, 7 thin omitted)

### Community 0 - ".ScoreCompetition"
Cohesion: 0.25
Nodes (10): Result, TaskRoundCoordinate, Competitors, DateTimeOffset, Dictionary, Fact, Group, ImmutableArray (+2 more)

### Community 1 - "Soarscore.Domain.PublishedClassDefinition"
Cohesion: 0.06
Nodes (9): Soarscore.Domain.Scoring, Soarscore.Domain.Tests, Soarscore.Application.Tests, Soarscore.Domain.People, Soarscore.SeedData, Soarscore.Domain.PublishedClassDefinition, JsonSerializerContext, Intrinsic (+1 more)

### Community 2 - ".BuildCompetitionWithPenalties"
Cohesion: 0.15
Nodes (12): InfractionType, SubjectIndex, Competitors, DateTimeOffset, Dictionary, Entries, Fact, Gen (+4 more)

### Community 3 - "FakeEntryQuery"
Cohesion: 0.06
Nodes (62): AfterTestRun, BeforeTestRun, CancellationToken, IClock, IEventStore, Task, BindParameterHandler, CancellationToken (+54 more)

### Community 4 - "SeedF5J"
Cohesion: 0.09
Nodes (19): Builder, Rows, ConstantTerm, Value, LookupRow, LookupTerm, MetricRef, Rows (+11 more)

### Community 5 - ".HandleAsync"
Cohesion: 0.09
Nodes (29): IsBogus, CancellationToken, IClock, IEventStore, Task, RegisterCompetitorHandler, DateTimeOffset, Fact (+21 more)

### Community 6 - "IServiceProvider"
Cohesion: 0.15
Nodes (13): IServiceProvider, Type, Dictionary, Type, FakeServiceProvider, Dictionary, Type, FakeServiceProvider (+5 more)

### Community 7 - "Soarscore.Domain.Competitions"
Cohesion: 0.10
Nodes (11): Soarscore.Domain.Competitions, Soarscore.Application.Shared.Entries, Soarscore.Application.Commands.Competitions, Soarscore.Application.Tests.Shared.Competitions, Soarscore.Application.Tests.Queries.Entries, Soarscore.Domain.Entries, Soarscore.Application.Queries.Entries, Soarscore.Domain (+3 more)

### Community 8 - "CatalogueDrawPropertyTests"
Cohesion: 0.07
Nodes (25): MinPerGroupByRound, Remaining, Sizes, Dictionary, IEnumerable, ImmutableArray, PhaseDraw, TaskCount (+17 more)

### Community 9 - ".CheckLimits"
Cohesion: 0.15
Nodes (9): IReadOnlyList, JsonSerializerOptions, List, ClassDefinitionIngestion, ClassDefinitionIngestionFixtures, Fact, ClassDefinitionIngestionPropertyTests, Fact (+1 more)

### Community 10 - "ResolvedTask"
Cohesion: 0.08
Nodes (30): Bindings, ClassDef, ExpectedRawScore, AllFlights, ScoreTerm, ImmutableArray, IReadOnlyDictionary, FlightSelector (+22 more)

### Community 11 - ".Of"
Cohesion: 0.12
Nodes (10): AllOf, Children, Predicate, Fact, InlineData, Theory, BindParameterDecideTests, ArgumentException (+2 more)

### Community 12 - "PrescribeDrawDecideTests"
Cohesion: 0.07
Nodes (34): Mutation, IReadOnlyList, PrescribedGroup, PrescribedRound, Given, HttpClient, HttpResponseMessage, IEnumerable (+26 more)

### Community 13 - ".Aggregate"
Cohesion: 0.12
Nodes (26): aggregate, dropped, DropPolicy, ApplyWhenResultsAtLeast, ApplyWhenRoundsCompletedAtLeast, Dimension, DropCount, ImmutableArray (+18 more)

### Community 14 - ".New"
Cohesion: 0.09
Nodes (17): AdoptedRules, AdoptedAt, Definition, SourceClassId, SourceVersion, Store, Store, ArgumentException (+9 more)

### Community 15 - "CompetitionId"
Cohesion: 0.05
Nodes (72): ICommand, IHttpMaxRequestBodySizeFeature, WebApplication, IReadOnlyList, WebApplication, CancellationToken, IClock, IEventStore (+64 more)

### Community 16 - "PostgresFixture"
Cohesion: 0.05
Nodes (38): IAsyncLifetime, PostgresBindParameterEventStoreTests, SqliteBindParameterEventStoreTests, PostgresCompetitorEventStoreTests, SqliteCompetitorEventStoreTests, PostgresDrawPhaseEventStoreTests, SqliteDrawPhaseEventStoreTests, DateTimeOffset (+30 more)

### Community 17 - "EntryId"
Cohesion: 0.09
Nodes (16): Guid, EntryId, Dictionary, Given, Group, HttpClient, HttpResponseMessage, List (+8 more)

### Community 18 - "ICommandHandler"
Cohesion: 0.09
Nodes (33): ICommandHandler, CancellationToken, IClock, IEventStore, ImmutableArray, Task, FinaliseCompetition, FinaliseCompetitionHandler (+25 more)

### Community 19 - "PhaseDrawn"
Cohesion: 0.12
Nodes (23): DateTimeOffset, Group, ImmutableArray, Penalty, ReflightRuling, Round, CompetitionEvent, CompetitorRegistered (+15 more)

### Community 20 - "NumberOrParam"
Cohesion: 0.07
Nodes (34): Bands, JsonConverter, JsonSerializerOptions, ClassDefinitionHashing, JsonSerializerOptions, Utf8JsonReader, Utf8JsonWriter, DecimalAsStringConverter (+26 more)

### Community 21 - "CompetitionSummary"
Cohesion: 0.30
Nodes (12): DateOnly, CompetitionSummary, CancellationToken, DateOnly, IReadOnlyList, Task, FindCompetitions, FindCompetitionsHandler (+4 more)

### Community 22 - "ClassDefinition"
Cohesion: 0.11
Nodes (21): Path, HashSet, IEnumerable, ImmutableArray, IReadOnlyDictionary, List, Phase, Task (+13 more)

### Community 23 - "MeasuredValue"
Cohesion: 0.11
Nodes (23): Exception, Parameter, AllowedValues, BoundAt, DefaultValue, Kind, Name, Unit (+15 more)

### Community 24 - "Soarscore.Application.Commands.CompetitionClasses"
Cohesion: 0.09
Nodes (12): Soarscore.Application.Tests.Commands.CompetitionClasses, Soarscore.Application.Queries.CompetitionClasses, Soarscore.Application.Tests.Shared.CompetitionClasses, Soarscore.Application.Shared.CompetitionClasses, Soarscore.Application.Commands.CompetitionClasses, Soarscore.Application.Tests.Queries.CompetitionClasses, Fact, ClassDefinitionHashingTests (+4 more)

### Community 25 - "ClassAgnosticismTests"
Cohesion: 0.09
Nodes (17): Soarscore.Api, Soarscore.ArchitectureTests, GeneratedRegex, HttpMethodMetadata, MethodInfo, Regex, Program, Fact (+9 more)

### Community 26 - "EntryCapturePropertyTests"
Cohesion: 0.07
Nodes (27): DecideActual, DecideModel, FlagValue, FlightPlan, MetricIndex, NumericValue, Pick, PlannedCapture (+19 more)

### Community 27 - "DrawAcceptanceEventStoreTests"
Cohesion: 0.09
Nodes (34): CountLetters, Echo, CancellationToken, IServiceProvider, Task, Type, Dispatcher, ICommand (+26 more)

### Community 28 - "Then"
Cohesion: 0.12
Nodes (15): ImmutableArray, CompetitionScoreView, CompetitorFinalScoreView, Then, DateTimeOffset, Dictionary, Given, Group (+7 more)

### Community 29 - ".New"
Cohesion: 0.14
Nodes (15): TaskRoundState, Annulled, Complete, Drawn, InProgress, Competitors, DateTimeOffset, Fact (+7 more)

### Community 30 - "FixtureModels.cs"
Cohesion: 0.15
Nodes (23): JsonElement, IReadOnlyList, List, Dictionary, CompetitionFile, CompetitionIdentity, CompetitionScoring, CompPilotRow (+15 more)

### Community 31 - ".Exact"
Cohesion: 0.13
Nodes (24): CancellationToken, IClock, IEventStore, Task, DrawPhaseHandler, AdoptedRules, Store, AdoptedRules (+16 more)

### Community 32 - "Entry"
Cohesion: 0.08
Nodes (33): DateTimeOffset, Func, ImmutableArray, Penalty, PenaltyRecorded, Result, Amendment, At (+25 more)

### Community 33 - "3.16 CLASS Q: NZ F5K (Hand Launch Electric Glider)"
Cohesion: 0.05
Nodes (37): 3.16.10 Landing rules:, 3.16.11 Retrieving of model glider, 3.16.12 Safety, 3.16.13 Mid-air collision, 3.16.14 Forbidden airspace, 3.16.15 Weather conditions / Interruptions, 3.16.16 Definition of landing, 3.16.17 Flight time (+29 more)

### Community 34 - ".CompareRawGrainAsync"
Cohesion: 0.17
Nodes (17): GroupNo, KeyCollection, PilotNo, RoundNo, Competition, Dictionary, Entry, HashSet (+9 more)

### Community 35 - "FlightOpened"
Cohesion: 0.17
Nodes (13): Penalty, EntryAnnulled, EntryEvent, FlightOpened, MeasurementAmended, MeasurementCaptured, PenaltyRecorded, ArgumentException (+5 more)

### Community 36 - "ReflightingAGroupSteps"
Cohesion: 0.18
Nodes (10): Dictionary, Given, Group, HttpClient, HttpResponseMessage, List, ProblemDetails, Task (+2 more)

### Community 37 - "AcceptingTheDrawSteps"
Cohesion: 0.16
Nodes (8): Given, HttpClient, HttpResponseMessage, IReadOnlyList, List, Task, AcceptingTheDrawSteps, Client

### Community 38 - "ScoringACompetitionSteps"
Cohesion: 0.15
Nodes (11): DateTimeOffset, Dictionary, Given, Group, HttpClient, HttpResponseMessage, IReadOnlyList, List (+3 more)

### Community 39 - "AmendMeasurementDecideTests"
Cohesion: 0.14
Nodes (13): AmendmentFact, MeasurementDigest, DateTimeOffset, Fact, Gen, ImmutableArray, IReadOnlyList, AmendMeasurementDecideTests (+5 more)

### Community 40 - "SystemClock"
Cohesion: 0.11
Nodes (31): CancellationToken, Competition, IEventStore, ImmutableArray, Task, CompetitionView, GetCompetition, GetCompetitionHandler (+23 more)

### Community 41 - "Work items"
Cohesion: 0.09
Nodes (21): Before starting — all discharged, Decisions settled during planning (2026-08-26), Execution plan, Known traps (pre-answered by planning — verified against the tree), Out of scope (deferrals restated), Pipeline shape (one feature per fixture, shared machinery), Plan, Story — GliderScore replay-and-compare harness (+13 more)

### Community 42 - "Plan — Capturing a score: the Entry write path and `entry_index`"
Cohesion: 0.06
Nodes (32): Acceptance, Context, Dependency order, Finding 1 — `entry_index` cannot be built from the Entry events as they stand · **fixed here**, Finding 2 — `TimeWindow.End` cannot be stated under `UntilAllFlightsComplete` · **fixed here**, Finding 3 — `OpenFlight` must not gate on the working-time window · **scope removal**, Finding 4 — capture-time rounding · **decided: apply it**, Four findings that shape the scope (+24 more)

### Community 43 - "Plan — Scoring: de-orphaning the scoring engine"
Cohesion: 0.06
Nodes (32): Acceptance, Context, Dependency order, Finding 1 — `ScoreCompetition` is a shell, not a mis-typed method, Finding 2 — amendment resolution exists nowhere in the tree, Finding 3 — the engine speaks `string`, the domain speaks typed ids, Finding 4 — `RecordedPenalty` and `Penalty` do not have the same shape, Finding 5 — nothing ever marks a task-round `Complete`, so the leaderboard must derive its own field (+24 more)

### Community 44 - ".BuildDispatcher"
Cohesion: 0.29
Nodes (11): CancellationToken, IEventStore, Task, GetPerson, GetPersonHandler, Fact, FakeClock, FakeEventStore (+3 more)

### Community 45 - "The Competition Class notation — draft spec"
Cohesion: 0.06
Nodes (30): 10. Findings F1–F15, 11. Findings F16–F21, 12. Findings F22–F23 — the F3F probe, 13. Findings F24–F27 — the NZ probe, 14. Finding F28 — the F3F re-check, 1. Three rules the notation obeys, 2. Shape, 3. Class level (+22 more)

### Community 46 - "TaskRoundRecordingPropertyTests"
Cohesion: 0.11
Nodes (27): GenEntry, GenFlight, Noise, PlacedEntry, Shape, Competition, DateTimeOffset, Entry (+19 more)

### Community 47 - "CaptureMeasurement"
Cohesion: 0.28
Nodes (11): CancellationToken, IClock, IEventStore, Task, CaptureMeasurementHandler, DateTimeOffset, Fact, FakeEventStore (+3 more)

### Community 48 - "Competition"
Cohesion: 0.10
Nodes (18): Defect, Penalty, PenaltyRecorded, ReflightRuling, TaskRoundCoordinate, Competition, AdoptedRules, EndDate (+10 more)

### Community 49 - "Comparison"
Cohesion: 0.15
Nodes (13): Comparator, EqualTo, GreaterOrEqual, GreaterThan, LessOrEqual, LessThan, Comparison, LeftMetricRef (+5 more)

### Community 50 - "RecordingAReflightRulingSteps"
Cohesion: 0.16
Nodes (9): Given, Group, HttpClient, HttpResponseMessage, List, ProblemDetails, Task, RecordingAReflightRulingSteps (+1 more)

### Community 51 - "2 SOARING (All Classes)"
Cohesion: 0.07
Nodes (30): 2.1 THERMAL SOARING, 2.2.1 General., 2.2.2 Launch apparatus shall conform to the following specifications:, 2.2 LAUNCHING, 2.3.1 Timing of the flight commences when the parachute/pennant is seen to drop from the, 2.3.2 Timing of the flight shall finish when the sailplane first touches the ground or a ground based, 2.3.3 Models already in the air and being timed at the completion of the round, may complete that, 2.3.4 If the sailplane comes into contact with a person during the flight and before the model (+22 more)

### Community 52 - "B.4 DEFINITIONS OF EXPRESSIONS"
Cohesion: 0.04
Nodes (47): B.1.1 General definition, B.1.2.1 Category F1 - Free Flight, B.1.2.2 Category F2 - Control Line Flight, B.1.2.3 Category F3 - Radio Controlled Flight, B.1.2.4 Category F4 - Scale Model Aircraft, B.1.2.5 Category F5 - Radio Control Electric Powered Aircraft, B.1.2.6 Category F7 - Radio Controlled Aerostats, B.1.2.7 Category F9 - Drone Sports (+39 more)

### Community 53 - ".SeedCompetition"
Cohesion: 0.19
Nodes (16): CancellationToken, IEventStore, Task, RecordCompetitionPenalty, RecordCompetitionPenaltyHandler, CancellationToken, DateTimeOffset, Fact (+8 more)

### Community 54 - ".ScoreGroup"
Cohesion: 0.27
Nodes (6): GroupResult, ImmutableArray, ImmutableDictionary, IReadOnlyDictionary, Penalty, ScoringService

### Community 55 - "FinaliseDecideTests"
Cohesion: 0.16
Nodes (11): DeclaredResult, Aggregate, CompetitorRef, Placing, Promoted, DateTimeOffset, Fact, ImmutableArray (+3 more)

### Community 56 - "ClassDefinitionSummary"
Cohesion: 0.15
Nodes (18): IQuery, DateTimeOffset, Guid, ClassDefinitionSummary, CancellationToken, IReadOnlyList, Task, FindClassDefinitions (+10 more)

### Community 57 - "Scoring Service Build Plan"
Cohesion: 0.07
Nodes (26): Dependency Graph, Design Rules Every Agent Must Uphold, File Layout, Issue Tracking, Open Issues, Overview, Parallelism Summary, Scoring Service Build Plan (+18 more)

### Community 58 - ".SeedOpenEntry"
Cohesion: 0.18
Nodes (16): CancellationToken, IClock, IEventStore, Task, OpenFlightHandler, CancellationToken, DateTimeOffset, Fact (+8 more)

### Community 59 - "Enumerations.cs"
Cohesion: 0.05
Nodes (43): CompositionKind, ChooseFromCatalogue, FixedSequence, DropDimension, ByRound, ByTask, ParameterBindingPoint, BeforeFlying (+35 more)

### Community 60 - "Work items"
Cohesion: 0.08
Nodes (25): 1. `TaskRoundState.InProgress` stays unreachable — but `TaskRoundReopened` is added, 2. Finalisation is competition-scope only this thread, Before starting — done, Out of scope — deliberately, Plan — Task-round lifecycle: `TaskRoundCompleted` / `TaskRoundReopened` / `TaskRoundAnnulled` / `Finalised`, Risks, The governing principle: the system does not order score capture, Three decisions taken up front (+17 more)

### Community 61 - ".AddSoarscoreInfrastructure"
Cohesion: 0.09
Nodes (19): IConfiguration, IServiceCollection, CancellationToken, DateOnly, IReadOnlyList, Task, ICompetitionsQuery, CancellationToken (+11 more)

### Community 62 - ".HandleAsync"
Cohesion: 0.18
Nodes (15): CancellationToken, IClock, IEventStore, Task, AnnulEntryHandler, CancellationToken, DateTimeOffset, Fact (+7 more)

### Community 63 - "GliderscoreFixture"
Cohesion: 0.20
Nodes (13): SlotCapture, IReadOnlyList, GliderscoreFixture, ScoresRawFile, ScoresRow, HttpClient, IReadOnlyDictionary, IReadOnlyList (+5 more)

### Community 64 - "Plan — Catalogue-choice draws: the CD picks each round's task"
Cohesion: 0.08
Nodes (24): Acceptance, Appendix A — the deferred follow-on: per-round parameter bindings, Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Domain, Phase B — Application (+16 more)

### Community 65 - "TaskRound"
Cohesion: 0.09
Nodes (26): ResolvedSchedule, Round, Group, Func, ImmutableArray, Group, CompetitorRefs, Id (+18 more)

### Community 66 - "Soarscore.Application.Queries.Competitions"
Cohesion: 0.09
Nodes (13): Soarscore.Application.Shared.People, Soarscore.Application.Commands.People, Soarscore.Application.Tests.Queries.Competitions, Soarscore.Acceptance.Tests.Support, Soarscore.Api.Queries, Soarscore.Application.Queries.Competitions, Soarscore.Application.Queries.Scoring, Soarscore.Acceptance.Tests.Support.Gliderscore (+5 more)

### Community 67 - "PenaltyDefinition"
Cohesion: 0.11
Nodes (28): AccruedInfo, PenaltyDefinition, Accrual, Effects, ExclusionGroups, PenaltyEffectSpec, Dictionary, HashSet (+20 more)

### Community 68 - ".Normalise"
Cohesion: 0.10
Nodes (27): NormalisationDirection, HigherIsBetter, LowerIsBetter, RoundingMode, Ceiling, HalfUp, Truncate, Normalisation (+19 more)

### Community 69 - ".Rank"
Cohesion: 0.14
Nodes (16): Disqualified, FinalRankingKind, LastPhaseReplaces, SinglePhase, SplitByPromotion, ImmutableArray, RankingEngine, ImmutableDictionary (+8 more)

### Community 70 - "Work items"
Cohesion: 0.08
Nodes (23): Before starting — done, Decisions settled before planning (user, 2026-08-21), Findings from reading the tree, Out of scope — deliberately, Plan, Planner's calls — flag for veto when this plan is reviewed, Risks, Story — Reflights: `ReflightGroupAppended` (+15 more)

### Community 71 - ".MapQueries"
Cohesion: 0.16
Nodes (17): IReadOnlyList, WebApplication, Queries, CancellationToken, Competition, Entry, IEventStore, ImmutableArray (+9 more)

### Community 72 - ".CompetitionAdopting"
Cohesion: 0.21
Nodes (4): Fact, InlineData, Theory, PhaseDrawnDecideTests

### Community 73 - "3.4 CLASS D : THERMAL FORMULA 500"
Cohesion: 0.09
Nodes (23): 3.4.1 Launching: The launch of the model may be by one of the following means:, 3.4.3 Duration Task, 3.4.4 Precision Task, 3.4.5 Contest Format, 3.4.6 NDC Competition, 3.4 CLASS D : THERMAL FORMULA 500, 3.5.1 There are no restrictions on motor, plane, motor control or cell size. No more than 7 x nicad, 3.5.2 The battery SHALL NOT BE RE-CHARGED between flights and the same battery must be (+15 more)

### Community 74 - "Plan — The CD's choices: `BindParameter`"
Cohesion: 0.09
Nodes (22): Acceptance, Context, Dependency order, Finding 1 — `ParameterBindingPoint.PerRound` is unrepresentable · **deferred**, Finding 2 — `Parameter.DefaultValue` is inert · **fixed here, see WI-2**, Governing documents, Out of scope (deliberately), Phase A — Domain (+14 more)

### Community 75 - "Context"
Cohesion: 0.09
Nodes (22): A model gap this plan closes before WI-1, not silently, Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Domain, Phase B — Application, Phase C — Api and verification (+14 more)

### Community 76 - "EndpointRouteBuilderExtensions"
Cohesion: 0.28
Nodes (4): IEndpointRouteBuilder, IResult, EndpointRouteBuilderExtensions, Func

### Community 77 - "CompetitorId"
Cohesion: 0.14
Nodes (17): IParsable, CancellationToken, IClock, IEventStore, Task, RegisterPerson, RegisterPersonHandler, Guid (+9 more)

### Community 78 - "PersonRegistered"
Cohesion: 0.06
Nodes (34): PeopleProjection, DateTimeOffset, Defect, Result, ClubAffiliation, ClubName, MembershipNumber, Person (+26 more)

### Community 79 - ".BuildDrawnCompetition"
Cohesion: 0.19
Nodes (7): Competitors, DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, AppendReflightGroupDecideTests

### Community 80 - "TaskRoundCompleted"
Cohesion: 0.10
Nodes (22): minRounds, minTasks, outcomes, RoundOutcome, TaskRoundAnnulled, TaskRoundCompleted, taskRefs, DateTimeOffset (+14 more)

### Community 81 - ".BuildDrawnCompetition"
Cohesion: 0.24
Nodes (8): Competition, Competitors, DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, RecordReflightRulingDecideTests

### Community 82 - "Work items"
Cohesion: 0.09
Nodes (21): Before starting — done, Decisions settled before planning (user, 2026-08-24), Findings from reading the tree, Out of scope — deliberately, Plan, Planner's calls — flag for veto when this plan is reviewed, Property-based invariants (named now, per CLAUDE.md), Reflight-scoring rulings (+13 more)

### Community 83 - ".ComputeEntries"
Cohesion: 0.18
Nodes (9): ImmutableArray, ImmutableDictionary, PairwiseCoOccurrence, PairwiseCoOccurrenceEntry, Dictionary, Fact, Group, Round (+1 more)

### Community 84 - "Soarscore.Application"
Cohesion: 0.09
Nodes (19): Soarscore.Application.Tests.Commands.Entries, Soarscore.Application.Commands.Entries, Soarscore.Api.Commands, Soarscore.Infrastructure, Soarscore.Application.Tests.Shared.Entries, Soarscore.Api.Routing, Soarscore.Application, Commands (+11 more)

### Community 85 - ".New"
Cohesion: 0.17
Nodes (7): Fact, InlineData, Theory, CompetitionDecideTests, DateTimeOffset, Fact, EntryTests

### Community 86 - ".Validate"
Cohesion: 0.28
Nodes (4): IReadOnlyList, Fact, ClassDefinitionValidationTests, ClassDefinitionFixtures

### Community 87 - "Core Principles"
Cohesion: 0.10
Nodes (20): Access is strictly via an REST based API, Append only immutable log as state storage (Event Sourced), Commands and Queries only, Core-owned invariants, Core Principles, CQRS pattern to cleanly seperate Reads from Writes, Domain Driven Design, Functional-Like as a Core Princple (+12 more)

### Community 88 - "00-general-rules.md"
Cohesion: 0.12
Nodes (15): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`F3J.10.10–10.11`), 4. Round score, 5. Final classification (`F3J.3.1`, `F3J.11`), 6. Re-flights (`F3J.4`, `F3J.5.2`), F3J — RC Thermal Duration Gliders, Penalty schedule (+7 more)

### Community 89 - "RC Soaring Competitions — Key Concepts"
Cohesion: 0.10
Nodes (20): Competition, Competition Class, Competitor, Draw, Entry, Finalisation, Flight, Group (+12 more)

### Community 90 - "Plan — Command-side steel thread: Person end-to-end"
Cohesion: 0.10
Nodes (20): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Foundations, Phase B — The Application kernel, Phase C — Adapters, Plan — Command-side steel thread: Person end-to-end (+12 more)

### Community 91 - "HarnessSelfCheckSteps"
Cohesion: 0.19
Nodes (5): Given, Task, Then, When, HarnessSelfCheckSteps

### Community 92 - ".DrawnCompetitionAsync"
Cohesion: 0.22
Nodes (12): CancellationToken, IClock, IEventStore, Task, RecordReflightRulingHandler, CancellationToken, Competition, Fact (+4 more)

### Community 93 - "ExpectedVersion"
Cohesion: 0.17
Nodes (12): ExpectedVersion, Any, IsAny, IsExact, IsNoStream, NoStream, Version, Kind (+4 more)

### Community 95 - "Work items"
Cohesion: 0.11
Nodes (18): Before starting, Decisions settled during planning (2026-08-25), Execution plan — how an agent (or agents) runs this, Findings from reading the tree (verified 2026-08-25), Out of scope (deferrals restated, untouched), Plan, Story — Prescribed-draw import capability, What (+10 more)

### Community 96 - "DrawAcceptanceDecideTests"
Cohesion: 0.20
Nodes (7): CompetitorRef, GroupRef, DateTimeOffset, Fact, InlineData, Theory, DrawAcceptanceDecideTests

### Community 97 - "5.5.10 F5K – RC THERMAL DURATION GLIDERS FOR MULTIPLE TASK COMPETITION WITH"
Cohesion: 0.10
Nodes (20): 5.5.10.10 Number of Model Aircraft, 5.5.10.11 Launch and Landing area (Pilots Area), 5.5.10.12 Penalty overview, 5.5.10.13 Reflight, 5.5.10.14 Preparation time, 5.5.10.15 Scoring, 5.5.10.16 Final score, 5.5.10.17 Resolution of a tie (+12 more)

### Community 98 - "Work items"
Cohesion: 0.10
Nodes (19): Before starting, Decisions settled during planning (2026-08-24), Execution plan — how an agent (or agents) runs this, Findings from reading the tree (re-verified 2026-08-24), Out of scope (deferrals restated, untouched), Plan, Story — Accepting or rejecting the draw, and redrawing, What (+11 more)

### Community 99 - "Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor`"
Cohesion: 0.10
Nodes (19): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Domain, Phase B — Application, Phase C — Api and verification, Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor` (+11 more)

### Community 100 - ".EvaluateTerm"
Cohesion: 0.51
Nodes (4): IReadOnlyDictionary, FlightInterpreter, Intrinsic, TermContribution

### Community 101 - "MetricDefinition"
Cohesion: 0.05
Nodes (33): MetricDefinition, DeclaredBeforeLaunch, Kind, Name, Precision, Unit, M, ImmutableArray (+25 more)

### Community 102 - "SeedF5K"
Cohesion: 0.13
Nodes (15): ImmutableArray, SeedF5K, Catalogue, Definition, FlightMetrics, LaunchAltitude, LaunchBands, LaunchPenaltyOnlyBands (+7 more)

### Community 103 - "F3F.1 GENERAL RULES"
Cohesion: 0.11
Nodes (19): F3F.1.10 Safety, F3F.1.11 Judging, F3F.1.12 Scoring, F3F.1.13 Classification, F3F.1.14 Team Classification, F3F.1.15 Organisation of the Contest, F3F.1.16 Changes, F3F.1.17 Weather Conditions and interruptions (+11 more)

### Community 104 - "CompetitionReplaceTaskRoundPropertyTests"
Cohesion: 0.12
Nodes (16): EventKind, phaseCount, roundsPerPhase, targetPhase, targetTaskRound, taskRoundsPerRound, Gen, kind (+8 more)

### Community 105 - "Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition`"
Cohesion: 0.11
Nodes (18): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — `Validate()` and ingestion limits, Phase B — `class_library` read model and the write path, Phase C — Api and end-to-end verification, Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition` (+10 more)

### Community 106 - "JasperFxEventStore"
Cohesion: 0.18
Nodes (11): CancellationToken, Exception, Guid, IDocumentReadOperations, IDocumentSessionFactory, IDocumentSessionOperations, IEventStoreOperations, IQueryEventStore (+3 more)

### Community 107 - "PublishClassDefinition"
Cohesion: 0.09
Nodes (35): Hash, CancellationToken, IClock, IEventStore, Task, PublishClassDefinition, PublishClassDefinitionHandler, CancellationToken (+27 more)

### Community 108 - "SeeingWhatIsRecordedSteps"
Cohesion: 0.22
Nodes (6): Given, HttpClient, List, Task, SeeingWhatIsRecordedSteps, Client

### Community 109 - ".SeedCompetition"
Cohesion: 0.29
Nodes (5): DateTimeOffset, Fact, Gen, Penalty, RecordCompetitionPenaltyDecideTests

### Community 110 - "6 F3L – RADIO CONTROLLED THERMAL GLIDERS RES"
Cohesion: 0.11
Nodes (18): 6 F3L – RADIO CONTROLLED THERMAL GLIDERS RES, F3L.10 Landing, F3L.11.1 Scoring of the Flight Time, F3L.11.2 Scoring of the Landing, F3L.11.3 Normalised Score, F3L.11 Scoring, F3L.12 Final Classification, F3L.1 General Rules (+10 more)

### Community 111 - "5.5.11 CLASS F5J – RC ELECTRIC POWERED THERMAL DURATION GLIDERS"
Cohesion: 0.11
Nodes (18): 5.5.11.10 Launching, 5.5.11.11 Landing, 5.5.11.12 Scoring, 5.5.11.13 Final Classification, 5.5.11.14.1 Organisational Requirements, 5.5.11.14.2 Timekeeper Responsibilities, 5.5.11.14 Advisory Information, 5.5.11.2 Competitors and Helpers (+10 more)

### Community 112 - "5.5.12 CLASS F5L – RADIO CONTROLLED THERMAL GLIDERS RES WITH ELECTRIC MOTOR AND"
Cohesion: 0.11
Nodes (18): 5.5.12.10 Landing, 5.5.12.11.1 Scoring of the Flight Time, 5.5.12.11.2 Scoring of the Landing, 5.5.12.11 Scoring, 5.5.12.12 Final Classification, 5.5.12.13 Additional Information, 5.5.12.1 General Rules, 5.5.12.2 Definition of a Radio-Controlled Glider (+10 more)

### Community 113 - "MartenEventStore"
Cohesion: 0.12
Nodes (12): PostgresException, CancellationToken, Exception, Guid, IDocumentReadOperations, IDocumentSessionOperations, IDocumentStore, IEventStoreOperations (+4 more)

### Community 114 - "FisherEventStore"
Cohesion: 0.12
Nodes (12): SqliteException, CancellationToken, Exception, Guid, IDocumentReadOperations, IDocumentSessionOperations, IDocumentStore, IEventStoreOperations (+4 more)

### Community 115 - "Work items"
Cohesion: 0.12
Nodes (15): Before starting, Decision — evidence-based triage refinement (2026-08-26, Pete approved), Story — Grow the Gliderscore fixture corpus, Survey of the export (2026-08-26), What, Why it matters, WI-1 — Refine validation rule 5 + index standing-skip wording, WI-2 — Commit the shared source export (+7 more)

### Community 116 - "LADR-0001 — Event store: PostgreSQL + Marten"
Cohesion: 0.12
Nodes (16): 1. Why Marten, 2. What we use, and what we deliberately do not, 3. Read models — the complete inventory, 4. Constraints that keep a SQLite adapter possible, 5. What a swap would actually cost, 6. When to revisit, 7. Not decided here, 8. Amendment, 2026-08-16 — the swap cost in §5 is wrong (+8 more)

### Community 117 - "SECTION C - CIAM GENERAL RULES FOR INTERNATIONAL EVENTS"
Cohesion: 0.12
Nodes (17): C.11.1 Class F - Model Aircraft, C.11.2 Class S - Space models, C.11 IDENTIFICATION MARKS, C.12 MODEL PROCESSING, C.14.1 Eligibility for World and Continental Championship, C.14.2 Maintaining championship status, C.14 CHAMPIONSHIP STATUS, C.17.1 Duration (+9 more)

### Community 118 - "C.15 ORGANISATION OF WORLD AND CONTINENTAL CHAMPIONSHIPS"
Cohesion: 0.12
Nodes (17): C.15.10 Multiple Classes (combined Championships – Cancellation of a class, C.15.1 CIAM championships naming policy, C.15.2.1 Class F (Model Aircraft), C.15.2.2 Class S (Space Models), C.15.2 Current World Championships, C.15.3 Offers to host a World or Continental Championship, C.15.4.1 Bulletin 0, C.15.4.2 Bulletin 1 (+9 more)

### Community 119 - "DrawAcceptancePropertyTests"
Cohesion: 0.12
Nodes (17): DrawOp, Op, DateTimeOffset, Gen, List, DrawAcceptancePropertyTests, DrawOp, Accept (+9 more)

### Community 120 - "Plan — Create-competition steel thread: `CreateCompetition`"
Cohesion: 0.12
Nodes (16): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — `competitions` read model, Phase B — `CreateCompetition` write path, Phase C — Api and end-to-end verification, Plan — Create-competition steel thread: `CreateCompetition` (+8 more)

### Community 121 - "PhaseDefinition"
Cohesion: 0.04
Nodes (52): closure, ClosureKind, fieldSize, ImmutableArray, PhaseDefinition, Drops, Ordinal, Promotion (+44 more)

### Community 122 - ".HandleAsync"
Cohesion: 0.11
Nodes (26): CancellationToken, IEventStore, Task, RecordEntryPenalty, RecordEntryPenaltyHandler, Penalty, By, CompetitorRef (+18 more)

### Community 123 - "2. Findings"
Cohesion: 0.15
Nodes (12): 1. Why, 2.1 Competition catalogue (public, easy), 2.2 What `eScoringInterface.exe` actually is, 2.3 Server API (recovered by decompiling GliderScore.exe 6.79 U5), 2.4 Download zip contents, 2.5 Caveats learned the hard way, 2. Findings, 3. Fit with the existing fixture pipeline (+4 more)

### Community 124 - "ReflightRule"
Cohesion: 0.20
Nodes (8): ReflightRule, EntitledScores, MinNewGroupSize, OthersScore, DateTimeOffset, Fact, ImmutableArray, ReflightSelectionPropertyTests

### Community 125 - "RecordEntryPenaltyDecideTests"
Cohesion: 0.33
Nodes (5): Fact, Gen, ImmutableArray, Penalty, RecordEntryPenaltyDecideTests

### Community 126 - "Rule map — topic × class"
Cohesion: 0.12
Nodes (15): Contest shape, Contest shape, Cross-class NZ rules, Drop-worst, Flight points and landing bonus, Launch-height scoring (F5 only), Normalisation and rounding, NZ national classes (NZMAA Section 5: Soaring, March 2024) (+7 more)

### Community 127 - "SECTION A - CIAM INTERNAL REGULATIONS"
Cohesion: 0.05
Nodes (42): A.10.1 Requirements for proposals, A.10.2 Effective date of rule changes, A.10.3 Submission procedure, A.10 SUBMISSION OF PROPOSALS TO THE CIAM, A.11.1 Emergency safety rules, A.11.2 Emergency safety notices, A.11 EMERGENCY SAFETY RULES & NOTICES, A.12 AEROMODELLING FUND (+34 more)

### Community 128 - ".LoadCurrentAsync"
Cohesion: 0.26
Nodes (10): CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task, CompetitionSummaryProjection (+2 more)

### Community 129 - "PublishedClassDefinition"
Cohesion: 0.14
Nodes (12): CancellationToken, IEventStore, Task, ClassDefinitionLoader, DateTimeOffset, Guid, PublishedClassDefinition, ContentHash (+4 more)

### Community 130 - "F3K.11 DEFINITIONS OF TASKS"
Cohesion: 0.13
Nodes (15): F3K.11.10 Task J (Three last flights), F3K.11.11 Task K (Increasing time by 30 seconds, “Big Ladder”), F3K.11.12 Task L (One flight), F3K.11.13 Fly-off Task M (Increasing time by 2 minutes “Huge Ladder”), F3K.11.14 Task N (Best flight), F3K.11.1 Task A (Last flight), F3K.11.2 Task B (Next to last and last flight), F3K.11.3 Task C (All up, last down) (+7 more)

### Community 131 - ".LoadCurrentAsync"
Cohesion: 0.26
Nodes (10): CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task, EntryIndexProjection (+2 more)

### Community 132 - "Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server"
Cohesion: 0.13
Nodes (14): Also done, not in the original plan, Before starting, Deliberately not done, One thing deliberately left short of the story's title, Outcome — as built, 2026-08-16, Plan, Property-based testing, Scope of this pass — Fisher/SQLite only (+6 more)

### Community 133 - "WithdrawCompetitorHandler"
Cohesion: 0.22
Nodes (11): CancellationToken, IClock, IEventStore, Task, WithdrawCompetitorHandler, DateTimeOffset, Fact, FakeEventStore (+3 more)

### Community 134 - ".LoadCurrentAsync"
Cohesion: 0.24
Nodes (11): IJasperFxProjection, CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task (+3 more)

### Community 135 - "ClassDefinitionPublished"
Cohesion: 0.12
Nodes (17): IDomainEvent, ClassDefinitionProjection, DateTimeOffset, ClassDefinitionEvent, ClassDefinitionPublished, ClassDefinitionRetired, Fact, ClassDefinitionEventJsonTests (+9 more)

### Community 136 - "1 GENERAL DEFINITIONS"
Cohesion: 0.10
Nodes (21): 0.0 NDC Rules for FAI Events, 0.1 F3F - RC SLOPE SOARING GLIDERS, 0.2.1 FAI F3K NDC Tasks:, 0.2 F3K - RC HAND LAUNCH GLIDERS, 0.3 F5J - RC ELECTRIC POWERED THERMAL DURATION GLIDERS, 0.4 F5K - RC ELECTRIC POWERED HAND LAUNCH GLIDERS, 1.1 DEFINITIONS, 1.2 CHARACTERISTICS (+13 more)

### Community 137 - "Defect"
Cohesion: 0.36
Nodes (3): Result, Phase, Defect

### Community 138 - "FixtureLoader"
Cohesion: 0.29
Nodes (4): Given, IReadOnlyList, JsonSerializerOptions, FixtureLoader

### Community 139 - "DrawingACatalogueChoicePhaseSteps"
Cohesion: 0.21
Nodes (10): HttpClient, HttpResponseMessage, List, ProblemDetails, Table, Task, Then, When (+2 more)

### Community 140 - "CaptureMeasurementDecideTests"
Cohesion: 0.32
Nodes (3): Fact, ImmutableArray, CaptureMeasurementDecideTests

### Community 141 - "validate.py"
Cohesion: 0.27
Nodes (17): base_competition(), check_integrity(), check_rule_1(), check_rule_2(), check_rule_3(), check_rule_4(), check_rule_5(), composite_key() (+9 more)

### Community 142 - "F3G.1 GENERAL RULES"
Cohesion: 0.15
Nodes (13): F3G.1.10 Organisation of Contests, F3G.1.11 Safety Rules, F3G.1.12 Weather Conditions/Interruptions, F3G.1.1 Definition of a Radio-Controlled Glider with Electric Motor, F3G.1.2 Characteristics data of Radio-Controlled Gliders F3G, F3G.1.3 Technical equipment, F3G.1.4 General requirements, F3G.1.5 Competitors and Helpers (+5 more)

### Community 143 - "Gliderscore Jet DB — Schema Analysis and Indicative Domain Mapping"
Cohesion: 0.15
Nodes (12): 1. Table inventory, 2. Relationships, 3. Indicative mapping to the Soarscore domain, 4. Observations on `Scores`, 5. The structural lesson, 6. Concept gaps surfaced (require glossary approval — not silently added), Competition setup, Event-time records (+4 more)

### Community 144 - "Story — Gliderscore golden-fixture pipeline"
Cohesion: 0.15
Nodes (12): Before starting, Export format — resolved from source 2026-08-25, Ranking oracle — decided 2026-08-25 (hybrid), Sequencing, Story — Gliderscore golden-fixture pipeline, What, Why it matters, WI-1 — Extraction tool (+4 more)

### Community 145 - "FakeEventStore"
Cohesion: 0.27
Nodes (9): IEventStore, CancellationToken, ExpectedVersion, Guid, IReadOnlyList, List, RecordedEvent, Task (+1 more)

### Community 146 - "GroupId"
Cohesion: 0.16
Nodes (14): CancellationToken, Entry, IEventStore, ImmutableArray, IReadOnlyDictionary, IReadOnlyList, Task, CompetitorTaskResultView (+6 more)

### Community 147 - "ReplaySteps"
Cohesion: 0.25
Nodes (5): Task, Then, When, ReplaySteps, Fixture

### Community 148 - "IStoreFixture"
Cohesion: 0.22
Nodes (11): Fact, Task, EntryCaptureEventStoreTests, CancellationToken, Task, IStoreFixture, ClassLibraryQuery, CompetitionsQuery (+3 more)

### Community 149 - "EntryModelBasedFoldTests"
Cohesion: 0.06
Nodes (37): actual, Actual, Model, FlightModel, MeasurementModel, model, Competition, Fact (+29 more)

### Community 150 - "CLAUDE.md — Soarscore"
Cohesion: 0.17
Nodes (12): CLAUDE.md — Soarscore, Core architectural law: Competition Class model vs. core system, Domain in one screen, graphify, House-keeping rules, Key constraints, Pointers, Project status (+4 more)

### Community 151 - "fai-rule.sh"
Cohesion: 0.39
Nodes (9): cmd_check_links(), cmd_find(), cmd_show(), cmd_toc(), die(), norm_ref(), fai-rule.sh script, volume_file() (+1 more)

### Community 152 - "00-nz-general-rules.md"
Cohesion: 0.29
Nodes (6): 1. The shared shape, 2. Data the timer / helper collects, 3. What is *not* scoring data, 4. Re-flights, NZ ALES — Generally Applicable Rules, Source references

### Community 153 - "F3B.2 RULES FOR MULTI-TASK CONTESTS"
Cohesion: 0.18
Nodes (11): F3B.2.10 Site, F3B.2.1 Definition, F3B.2.2 Launching, F3B.2.3 Task A - Duration, F3B.2.4 Task B - Distance, F3B.2.5 Task C - Speed, F3B.2.6 Partial Scores, F3B.2.7 Total Score (+3 more)

### Community 154 - "F3B.1 GENERAL RULES"
Cohesion: 0.17
Nodes (12): F3B.1.10 Safety Rules, F3B.1.11 Weather Conditions / Interruptions, F3B.1.1 Definition of a Radio-Controlled Glider, F3B.1.2 Prefabrication of F3B Model Aircraft, F3B.1.3 Characteristics of Radio-Controlled Gliders F3B, F3B.1.4 Competitors and Helpers, F3B.1.5 Definition of an Attempt, F3B.1.6 Definition of the Official Flight (+4 more)

### Community 155 - "F3J.10 SCORING"
Cohesion: 0.17
Nodes (12): F3J.10.10 Group Winner, F3J.10.11 Corrected Score, F3J.10.1 Flight Timing, F3J.10.2 Flight Time Recording, F3J.10.3 Overflying of the Working Time, F3J.10.4 Long Overflying, F3J.10.5 Landing Evaluation, F3J.10.6 Landing distance Measuring (+4 more)

### Community 156 - "PART 5 – TECHNICAL REGULATIONS FOR RADIO"
Cohesion: 0.17
Nodes (12): 5.5.7.1 Definition, 5.5.7.2 Course Layout and Organisation, 5.5.7.3 Scoring, 5.5.7 F5E – RC SOLAR POWERED MOTOR GLIDERS (PROVISIONAL), 5.5.8.1 Model Aircraft Specifications:, 5.5.8 F5F – RC 6 CELL ELECTRIC POWERED MOTOR GLIDERS (PROVISIONAL), 5.5.9.1 Definition, 5.5.9.2 Model Aircraft Specifications: (+4 more)

### Community 157 - "Plan"
Cohesion: 0.17
Nodes (11): Amend a captured measurement, Before starting, Decisions settled before planning (user, 2026-08-18), Findings from reading the tree, Out of scope, Plan, Property-based invariants (CsCheck), Separation of duty — the open design question (+3 more)

### Community 158 - "Plan"
Cohesion: 0.17
Nodes (11): Before starting, Decisions settled before planning (user, 2026-08-21), Findings from reading the tree, Implementation notes (deviations from the plan as written), Out of scope, Plan, Property-based invariants (CsCheck), Story — The second Entry thread (annul and penalise) (+3 more)

### Community 159 - "Story — Move the store adapters onto the JasperFx shared contracts"
Cohesion: 0.17
Nodes (11): Also done, not in the original plan, Before starting, Decision — `ReadAllAsync` stays, as a per-store method, Outcome — as built, 2026-08-16, Plan, Property-based testing, Story — Move the store adapters onto the JasperFx shared contracts, Two collisions worth knowing about (+3 more)

### Community 160 - "Raw score"
Cohesion: 0.17
Nodes (12): Duration time→points curve (`GetTimeScore` Case 1, `Scoring_MOD.vb:645–673`), F3K (`CalcRawScoreF3K`, `Scoring_MOD.vb:1467–1887`), F5K four-flights-in-four-columns packing and height bonus, Landing distance → points (`GetLandingBonus`, `Scoring_MOD.vb:726–803`), Per-family raw-score formulas (`Update_RawScore`, `Scoring_MOD.vb:137–244`; branch on `drv("TaskNo")` at :162), Raw score, Score pipeline and persistence, Unresolved (+4 more)

### Community 161 - "Result"
Cohesion: 0.08
Nodes (32): Competition, TaskResolver, CancellationToken, Guid, IReadOnlyList, Task, IEventStore, RecordedEvent (+24 more)

### Community 162 - "GliderScore fixture extraction"
Cohesion: 0.17
Nodes (11): Differential gate result, GliderScore fixture extraction, Index contract (rule 5), Offline-only rule, Output shape contract, Pinned access_parser version, Purpose, Schema type names (+3 more)

### Community 163 - "AnnulEntryDecideTests"
Cohesion: 0.35
Nodes (5): DateTimeOffset, Fact, Gen, ImmutableArray, AnnulEntryDecideTests

### Community 164 - "Story — GliderScore webmine tool (read-only online comp acquisition)"
Cohesion: 0.22
Nodes (8): Before starting, Confidentiality, Open questions carried forward, Plan, Story — GliderScore webmine tool (read-only online comp acquisition), Validation of the mining approach (source cross-reference, 2026-08-26), What, Why it matters

### Community 166 - ".SetUpAsync"
Cohesion: 0.49
Nodes (5): Fact, Group, List, Task, TaskRoundRecordingEventStoreTests

### Community 167 - "NZ Class M — ALES 200 (Altitude Limited Electric Soaring)"
Cohesion: 0.18
Nodes (11): 1. Pilot assignment to groups (the draw), 2. Launch (`NZ.3.12.1`), 3. Data the timer / helper collects, 4. The task (`NZ.3.12.1 f, g, m, n`), 5. Group score (`NZ.3.12.3`), 6. Round and final score, 7. Re-flights (`NZ.3.12.5 l`), 8. NDC format (`NZ.3.12.7`) — a different scoring pipeline (+3 more)

### Community 168 - "NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)"
Cohesion: 0.18
Nodes (11): 1. Pilot assignment to groups (the draw) — **and the open problem**, 2. Launch (`NZ.3.15.1 d, f, g`), 3. Data the timer / helper collects, 4. The task (`NZ.3.15.1 c`), 5. Score (`NZ.3.15.1 i`), 6. Rounds, 7. Re-flights (`NZ.3.15.1 h`), 8. A defect in the rule text — `NZ.3.15.1 j` (+3 more)

### Community 169 - "3.17 CLASS R: E-RES 2M (Electric Rudder Elevator Spoiler 2M Glider)"
Cohesion: 0.18
Nodes (11): 3.17.0 Contents:, 3.17.1 Introduction, 3.17.2 Model Specifications, 3.17.3 Competition Terrain, 3.17.4 Cancellation, 3.17.5 Competition Flights, 3.17.6 Launching, 3.17.7 Landing (+3 more)

### Community 170 - "ReflightSelection"
Cohesion: 0.15
Nodes (13): ReflightSelection, BetterOf, NotPermitted, Replacement, UndefinedRequiresRuling, DateTimeOffset, ReflightRuling, At (+5 more)

### Community 171 - "F3G.2 RULES FOR MULTI-TASK CONTESTS"
Cohesion: 0.20
Nodes (10): F3G.2.1 Definition, F3G.2.2 Launching / Relaunching, F3G.2.3 Task A – Duration, F3G.2.4 Task B Distance, F3G.2.5 Task C – Speed, F3G.2.6 Partial Scores, F3G.2.7 Total Score, F3G.2.8 Classification (+2 more)

### Community 172 - "Soarscore — Users"
Cohesion: 0.18
Nodes (10): 1. Organiser, 2. Contest Director, 3. Scorer, 5. Pilot / Competitor, Direct users, Indirect users, Multiple "Hats" Rule, Purpose (+2 more)

### Community 173 - "Drop-worst"
Cohesion: 0.18
Nodes (11): 1. Configuration source (Comps table), 2. `DropScoreOption` decode and gating, 3. Staged activation — how many drops at R rounds flown, 4. Selection basis (task-driven vs round-driven), 5. Tie-breaking among equal drop candidates — deterministic, 6. Marking and exclusion, 7. Re-flights, 8. F3K-gated variant (`f3kRecord`) (+3 more)

### Community 174 - "FakeEventStore"
Cohesion: 0.31
Nodes (8): CancellationToken, Guid, IReadOnlyDictionary, IReadOnlyList, List, Task, FakeEventStore, Streams

### Community 175 - "When"
Cohesion: 0.18
Nodes (9): ConditionalTerm, Else, When, HttpClient, HttpResponseMessage, JsonSerializerOptions, Task, ApiClient (+1 more)

### Community 176 - "PersonSummary"
Cohesion: 0.20
Nodes (15): CancellationToken, IReadOnlyList, Task, FindPeople, FindPeopleHandler, CancellationToken, IReadOnlyList, Task (+7 more)

### Community 177 - "LADR-0002 — Competition Class definition: representation, ingestion and identity"
Cohesion: 0.20
Nodes (9): 1. Users POST definitions, 2. Authoring: C# records, not a fluent DSL, 3. No notation parser in the core, 4. Ingestion — one path, 5. Identity: content hash, not versions, 6. Transcribing `seed-data/*.class`, 7. Citations are not in the model — decided, rejected, Decision (+1 more)

### Community 178 - "DateTimeOffset"
Cohesion: 0.09
Nodes (23): DateTimeOffset, Draw, CreatedAt, Status, Finalisation, At, By, DeclaredResults (+15 more)

### Community 179 - "F5K — RC Electric Thermal Duration, Multiple-Task"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`5.5.10.15`), 4. Round score, 5. Final classification (`5.5.10.16–10.18`), 6. Re-flights (`5.5.10.13`), F5K — RC Electric Thermal Duration, Multiple-Task, Nominal Launch Height (NLH) and launch points (`5.5.10.3–10.4`) (+2 more)

### Community 180 - "NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Launch (`NZ.3.13.1 d, f, g`), 3. Data the timer / helper collects, 4. The task (`NZ.3.13.1 c`), 5. Score (`NZ.3.13.1 i`), 6. Rounds, 7. Re-flights (`NZ.3.13.1 h`), 8. What is not stated (+2 more)

### Community 181 - ".BuildDrawnCompetition"
Cohesion: 0.27
Nodes (8): DateTimeOffset, Fact, Func, Group, ImmutableArray, NormalisationGroupIsolationPropertyTests, World, World

### Community 182 - "C.16.2 Requirements for radio control"
Cohesion: 0.20
Nodes (10): C.16.1 General requirements, C.16.2.1 Flight area, C.16.2.2 Transmitter pound, C.16.2.3 Spread spectrum transmitters, C.16.2.4 AM/FM transmitters, C.16.2.5 Detection of radio interference, C.16.2.6 Starting order, C.16.2.7 Other requirements (+2 more)

### Community 183 - "C.2.1 First category events"
Cohesion: 0.20
Nodes (10): C.2.1.1 World Championships, C.2.1.2 Continental Championships, C.2.1.3 World Air Games and World Games, C.2.1 First category events, C.2.2.1 Open International, C.2.2.2 International Series, C.2.2.3 World Cup, C.2.2 Second category events (+2 more)

### Community 184 - "5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS"
Cohesion: 0.20
Nodes (10): 5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS, F3K.3.1 Flying field, F3K.3.2 Start and landing field, F3K.3 DEFINITION OF THE FLYING FIELD, F3K.5 WEATHER CONDITIONS / INTERRUPTIONS, F3K.6.1 Landing, F3K.6.2 Valid landing, F3K.6 DEFINITION OF LANDING (+2 more)

### Community 185 - "Plan"
Cohesion: 0.20
Nodes (9): As built (2026-08-24), Decisions settled during planning (2026-08-24), Findings from reading the tree, Flights within an Entry can be recorded out of order, Out of scope, Plan, What, Why it matters (+1 more)

### Community 186 - "Scoring Service — Open Design Issues"
Cohesion: 0.20
Nodes (9): Issue #1: `CapScope.PerTask` — flight interpreter / flight selector interaction, Issue #2: `validWhen` evaluation semantics, Issue #3: `BestNFlights` AnyOrder target pairing algorithm, Issue #4: Measurement amendment resolution — where does it live?, Issue #5: `minValidResults` and group annulment — whose job?, Issue #6: `validWhen` and flight selection — what ordering?, Issue #7: `ResolvedTask` type placement, Issue #8: `ByTask` drop dimension — exact algorithm (+1 more)

### Community 187 - "Soarscore.Acceptance.Tests.csproj"
Cohesion: 0.20
Nodes (9): Microsoft.AspNetCore.Mvc.Testing, Reqnroll.xunit.v3, $(SoarscoreTargetFramework), AwesomeAssertions, Microsoft.NET.Test.Sdk, Testcontainers.PostgreSql, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 188 - "Soarscore.sln"
Cohesion: 0.22
Nodes (4): $(SoarscoreTargetFramework), Microsoft.NET.Sdk, $(SoarscoreTargetFramework), Microsoft.NET.Sdk

### Community 189 - "IProjection"
Cohesion: 0.13
Nodes (14): IProjection, CancellationToken, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task, ClassDefinitionSummaryProjection (+6 more)

### Community 190 - "F3 Soaring — Generally Applicable Rules"
Cohesion: 0.29
Nodes (7): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score, 4. Round & final score, 5. Re-flights, F3 Soaring — Generally Applicable Rules, Source references

### Community 191 - "Soarscore.Infrastructure.Tests.csproj"
Cohesion: 0.20
Nodes (9): $(SoarscoreTargetFramework), AwesomeAssertions, Fisher, Marten, Microsoft.NET.Test.Sdk, Testcontainers.PostgreSql, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 192 - "non-functional-requirements.md"
Cohesion: 0.22
Nodes (5): NFR-1 — One centralised, flexible competition class model, NFR-2 — Additive-only extensibility for new competition types, NFR-3 — Core System Only, NFR-4 — No imposed ordering on score capture, Soarscore — Non-Functional Requirements

### Community 193 - "Competition rules for RC soaring"
Cohesion: 0.22
Nodes (8): Auditing a change for compliance, Competition rules for RC soaring, Invariants — **FAI classes only**, Never `Read` a file in `source-docs/`, Retrieval ladder — stop at the first rung that answers the question, Rules → architecture, Rules for working with this corpus, The corpus

### Community 194 - "Competition Rules — Generally Applicable (all contest types)"
Cohesion: 0.22
Nodes (9): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (normalisation), 4. Round score, 5. Final classification (common), 6. Penalties (common), 7. Re-flights (common pattern), Competition Rules — Generally Applicable (all contest types) (+1 more)

### Community 195 - "DocumentClassLibraryQuery"
Cohesion: 0.32
Nodes (5): CancellationToken, IDocumentSessionFactory, IReadOnlyList, Task, DocumentClassLibraryQuery

### Community 196 - "ReflightRole"
Cohesion: 0.18
Nodes (9): ReflightRole, Entitled, Filler, Original, Entry, IReadOnlyList, ReflightSelector, InlineData (+1 more)

### Community 197 - "C.13 REQUIREMENTS FOR ORGANISATION OF INTERNATIONAL EVENTS"
Cohesion: 0.22
Nodes (9): C.13.1 Organisation, C.13.2 Local rules, C.13.3 Number of entries, C.13.4 Entry forms, C.13.5 Junior classification in an Open International, C.13.6 Female classification in an Open International, C.13.7 Results of international events, C.13.8 Fuel (+1 more)

### Community 198 - "C.21 CIAM TROPHIES"
Cohesion: 0.22
Nodes (9): C.21.1 Registration of CIAM trophies, C.21.2 Acceptance of CIAM trophies, C.21.3 Award of CIAM trophies, C.21.4 CIAM trophies report forms, C.21.5 Championship trophies, C.21.6 World Cup trophies, C.21.7 Responsibilities of the holder of a CIAM trophy, C.21.8 Loss of a CIAM trophy (+1 more)

### Community 199 - "C.5.1 Competitor"
Cohesion: 0.22
Nodes (9): C.5.1.1 Age of participants for Junior World or Continental Championships, C.5.1.2 Builder of the model, C.5.1.3 Competitor's proxy and substitution of team members, C.5.1.4 Anti-Doping Policy for Competitors, C.5.1 Competitor, C.5.2 Team manager, C.5.3 National team for World and Continental Championships, C.5.4 Competitor Invitation Procedure Phases (+1 more)

### Community 200 - "4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS"
Cohesion: 0.22
Nodes (9): 4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS, F3J.12 WEATHER CONDITIONS AND INTERRUPTIONS, F3J.3.1 Rounds and Attempts, F3J.3 CONTEST FLIGHTS, F3J.4 RE-FLIGHTS, F3J.5.1 Judging, F3J.5.2 Neutralisation of a flight group, F3J.5 CANCELLATION OF A FLIGHT AND/OR DISQUALIFICATION (+1 more)

### Community 201 - "5.5.3 CLASS F5A – RC ELECTRIC POWERED GPS MOTOR GLIDERS (PROVISIONAL RULE)"
Cohesion: 0.22
Nodes (9): 5.5.3.1 Definition, 5.5.3.2 Energy Management, 5.5.3.3 Course Layout, 5.5.3.4 Launching, 5.5.3.5 Distance Task, 5.5.3.6 Landing Task, 5.5.3.7 Contest organisation, 5.5.3.8 Scoring (+1 more)

### Community 202 - "5.5.4 CLASS F5B – RC ELECTRIC POWERED MULTI TASK GLIDERS"
Cohesion: 0.22
Nodes (9): 5.5.4.1 Definition, 5.5.4.2 Course Layout and Organisation, 5.5.4.3 F5B Contest Site Layout, 5.5.4.4 Scoring, 5.5.4.5 Launching, 5.5.4.6 Distance Task, 5.5.4.7 Duration and Landing Task, 5.5.4.8 Site (+1 more)

### Community 203 - "ClassDefinitionValidationPropertyTests"
Cohesion: 0.28
Nodes (6): ExpectedCode, Definition, Fact, Func, Gen, ClassDefinitionValidationPropertyTests

### Community 204 - "Soarscore.Infrastructure.Tests"
Cohesion: 0.08
Nodes (23): Soarscore.Infrastructure.Competitions, Soarscore.Infrastructure.Tests, Soarscore.Infrastructure.CompetitionClasses, Soarscore.Infrastructure.People, Soarscore.Application.Tests.Shared.People, Soarscore.Application.Queries.People, Soarscore.Application.Tests.Queries.People, Soarscore.Infrastructure.Entries (+15 more)

### Community 205 - ".Apply"
Cohesion: 0.31
Nodes (4): CompetitionProjection, Fact, Fact, CompetitionProjectionTests

### Community 206 - "Entry-completeness indicator"
Cohesion: 0.22
Nodes (9): As built, Before starting, Before starting — done, Design constraints, Entry-completeness indicator, Not blocked by, and does not block, What, Why it cannot simply be derived — the reason this is an indicator, not a state (+1 more)

### Community 207 - "Plan"
Cohesion: 0.22
Nodes (9): Plan, Sub-agent split, WI-1 — Domain: `EntryOpened` and the `Entry` aggregate, WI-2 — Domain: `Competition.OpenEntry`, WI-3 — Tests: mechanical removal (Domain, Application, Infrastructure), WI-4 — Tests: re-express binding/default resolution at the scoring seam, WI-5 — Tests: re-express the acceptance scenario, WI-6 — Docs — approved 2026-08-21, apply with the rest of the work (+1 more)

### Community 208 - "Ranking & tie-breaks"
Cohesion: 0.22
Nodes (9): Fly-off / preliminary-final override (note), `HiddenRanking` vs displayed `Rank`, Percent column, Ranking & tie-breaks, Sanity check vs sample comp (ALES, `/tmp/opencode/gs_data.json`), Sort-key spec (primary ladder), Team / Comp-Series / By-Task (note), THE LADDER — ordered comparisons (+1 more)

### Community 209 - "Deferred decisions"
Cohesion: 0.22
Nodes (8): Annulments and penalties, Competition class model, Decisions that have since been taken up, Deferred decisions, Draw, Event store, Score capture and corrections, Task-round lifecycle and finalisation

### Community 210 - "ComparisonReport"
Cohesion: 0.24
Nodes (7): IEnumerable, IReadOnlyList, ComparisonReport, AllGrainsExact, Conserves, ConservationBreak, Detail

### Community 211 - "DecideFlightModel"
Cohesion: 0.32
Nodes (7): DecideFlightModel, List, DecideFlightModel, Measurements, Sequence, DecideModel, Flights

### Community 212 - "ScoringCorpusPropertyTests"
Cohesion: 0.25
Nodes (7): DateTimeOffset, Fact, ImmutableArray, ImmutableDictionary, Name, Value, ScoringCorpusPropertyTests

### Community 213 - "Compliance check"
Cohesion: 0.25
Nodes (7): 1. Scope the check, 2. Pull the rules, 3. Verify numbers against source, 4. Check it against the architectural law, 5. Check it against the rest of the corpus, 6. Report, Compliance check

### Community 214 - "Model"
Cohesion: 0.25
Nodes (8): CompetitorModel, List, Model, Competitors, FinalisationCount, ParameterBindingCount, PenaltyCount, RulesAmendmentCount

### Community 215 - "F5J — RC Electric Powered Thermal Duration Gliders"
Cohesion: 0.25
Nodes (8): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`5.5.11.12`), 4. Round score, 5. Final classification (`5.5.11.13`), 6. Re-flights (`5.5.11.6`), F5J — RC Electric Powered Thermal Duration Gliders, Source references

### Community 216 - "RC Soaring Competitions — Domain Class Diagram"
Cohesion: 0.25
Nodes (6): 1. The competition spine, 2. Competition Class — structure, 3. Competition Class — the scoring vocabulary, 4. Scoring, Modelling notes, RC Soaring Competitions — Domain Class Diagram

### Community 217 - "DocumentPeopleQuery"
Cohesion: 0.38
Nodes (5): CancellationToken, IDocumentSessionFactory, IReadOnlyList, Task, DocumentPeopleQuery

### Community 218 - "F5L — RC Electric Thermal Gliders, RES"
Cohesion: 0.25
Nodes (8): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`5.5.12.11`), 4. Round score, 5. Final classification (`5.5.12.12`), 6. Re-flights (`5.5.12.9`), F5L — RC Electric Thermal Gliders, RES, Source references

### Community 219 - "NZ Soaring — Generally Applicable Rules"
Cohesion: 0.25
Nodes (8): 1. Scope and the FAI classes, 2. Official flight and repeat attempts (`NZ.1.6`, `NZ.1.7`), 3. Landing (`NZ.2.4`), 4. Contests (`NZ.2.5`), 5. Altitude limiters (`NZ.2.8`), 6. What this rulebook does not state, NZ Soaring — Generally Applicable Rules, Source references

### Community 220 - "3.10 CLASS K: Thermal R.E.S. (Rudder, Elevator, Spoiler)"
Cohesion: 0.25
Nodes (8): 3.10.1 Flown to Class A Thermal Flying Rules, 3.10.2 The model may be any size within the general rules, 3.10.3 There are no restrictions on building materials, 3.10.4 Basic flight control is by rudder and elevator or moving tail only, 3.10.5 Spoiler control must not utilise Trailing edge flaps, except in the case of a flying wing,, 3.10.6 There is no restriction on the number of servos, 3.10.7 It is not necessary to have a spoiler., 3.10 CLASS K: Thermal R.E.S. (Rudder, Elevator, Spoiler)

### Community 221 - "3.12 CLASS M: ALES 200 (Altitude Limited Electric Soaring)"
Cohesion: 0.25
Nodes (8): 3.12.1 Event Rules, 3.12.2 Landing, 3.12.3 Scoring, 3.12.4 General Requirements, 3.12.5 Definition of Electric Powered Model Glider:, 3.12.6 Approved Timer/Altimeters, 3.12.7 National Decentralized Contest Format (NDC), 3.12 CLASS M: ALES 200 (Altitude Limited Electric Soaring)

### Community 222 - "C.18 SAFETY"
Cohesion: 0.25
Nodes (8): C.18.1 Premise, C.18.2 Competence, C.18.3 Prohibited, C.18.4 Other requirements, C.18.5 Pre-flight checks, C.18.6 After launch of the model, C.18.7 Flying sites, C.18 SAFETY

### Community 223 - "EntryEventJsonTests"
Cohesion: 0.47
Nodes (3): DateTimeOffset, Fact, EntryEventJsonTests

### Community 224 - "5.5.1 GENERAL RULES"
Cohesion: 0.25
Nodes (8): 5.5.1.1 Definition of Electric Powered Motor Gliders, 5.5.1.2 Builder of the Model Aircraft, 5.5.1.3 General Characteristics of RC Electric Powered Motor Gliders F5, 5.5.1.4 Energy Limiter/Logger, 5.5.1.5 Procedure for Limiter and Logger Checking, 5.5.1.6 Number of Model Aircraft, 5.5.1.7 Competitor and Helper, 5.5.1 GENERAL RULES

### Community 225 - "Work items"
Cohesion: 0.25
Nodes (8): WI-1 — The query: views, pure compute core, handler (Application), WI-2 — Route and composition (Api), WI-3 — Property tests (CsCheck), invariants named up front, WI-4 — Store-backed tests (both backends), WI-5 — Acceptance feature, WI-6 — Verification loop, WI-7 — Board reconciliation, Work items

### Community 226 - "Plan — Per-round parameter bindings"
Cohesion: 0.25
Nodes (7): Before starting, Plan — Per-round parameter bindings, Shape, as far as the prior thread's design settled it, The freeze rule — decided, What, Why it matters, Work items — as built

### Community 227 - "Remove `Flight.LaunchAt`"
Cohesion: 0.25
Nodes (7): Before starting, Blast radius, Remove `Flight.LaunchAt`, What, What was done, What was left alone, Why it matters

### Community 228 - "Precision & storage"
Cohesion: 0.25
Nodes (8): 1. `RoundNumber(Nbr As Double, Decs As Integer) As Double` — `GlobalFunctions_MOD.vb:3116-3134`, 2. `TruncateNumber(Nbr As Double, Decs As Integer) As Double` — `GlobalFunctions_MOD.vb:3155-3176`, 3. `Decs` range, 4. Stage-by-stage storage map (acceptance gate), 5. Config plumbing, 6. Comparator recommendation, Precision & storage, Unresolved

### Community 229 - "Plan"
Cohesion: 0.25
Nodes (8): Execution waves for sub-agents, Plan, WI-1 — RawScore composition, per task family, WI-2 — NormalisedScore: the group-score matrix, WI-3 — Precision, rounding, and storage map, WI-4 — Drop-worst: activation schedule and algorithm, WI-5 — Final ranking, tie-breaks, percent, fly-offs, WI-6 — Reconciliation gate and consolidation

### Community 230 - "Soarscore.Architecture.Tests.csproj"
Cohesion: 0.25
Nodes (7): TngTech.ArchUnitNET.xUnitV3, $(SoarscoreTargetFramework), AwesomeAssertions, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 232 - "Soarscore.Application.Tests.csproj"
Cohesion: 0.25
Nodes (7): $(SoarscoreTargetFramework), AwesomeAssertions, CsCheck, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 233 - "ResultTests"
Cohesion: 0.39
Nodes (3): Fact, InvalidOperationException, ResultTests

### Community 234 - ".New"
Cohesion: 0.08
Nodes (31): FieldOp, Id, Competition, DateOnly, CompetitionCreated, DrawAccepted, Role, Competition (+23 more)

### Community 235 - "Soarscore.Domain.Tests.csproj"
Cohesion: 0.25
Nodes (7): $(SoarscoreTargetFramework), AwesomeAssertions, CsCheck, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 236 - "RC Soaring — Aggregate Boundaries"
Cohesion: 0.29
Nodes (7): 1. CompetitionClass — the rulebook library, 2. Person — a registered person, 3. Competition — the event structure, field and schedule, 4. Entry — the live flying record, RC Soaring — Aggregate Boundaries, Scoring is cross-aggregate (not a root), Why there are four roots, not three

### Community 237 - "2.5 CONTESTS"
Cohesion: 0.67
Nodes (3): 2.5.1 Contestants Meeting., 2.5.2 Round Identification., 2.5 CONTESTS

### Community 239 - "2.4 LANDING"
Cohesion: 0.29
Nodes (7): 2.4.1 An in-flight sailplane has right of way over a launching sailplane., 2.4.2 In contests requiring precision (spot) landings, the pilot and timekeeper must stand upwind, 2.4.3 Models are to be scored and retrieved by the pilot / timekeeper with haste and caution so, 2.4.4 Precision Landings for Gliding events, 2.4.5 Precision Landings for Electric Events, 2.4.6 The Flight is cancelled and recorded as a zero score if during landing, the nose of the model, 2.4 LANDING

### Community 240 - "3.3 CLASS C: PREMIER THERMAL DURATION."
Cohesion: 0.29
Nodes (7): 3.3.1 Launching: The launch of the model may be by one of the following means:, 3.3.2 Organisation of Starts, 3.3.3 Scoring, 3.3.4 Definition of an Attempt and Official Flight., 3.3.5 Number of Rounds., 3.3.6 Partial Scores, 3.3 CLASS C: PREMIER THERMAL DURATION.

### Community 241 - "F3K — RC Hand-Launch Gliders"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`F3K.9.1`), 4. Round score, 5. Final classification (`F3K.10`), 6. Re-flights (`F3K.9.6`, `F3K.4.2`, `F3K.2.4`), F3K — RC Hand-Launch Gliders, Penalty schedule (+2 more)

### Community 242 - "C.20 COMPLAINTS AND PROTESTS"
Cohesion: 0.29
Nodes (7): C.20.1.1 Complaints prior to an event, C.20.1.2 Complaints during an event, C.20.1 Complaints, C.20.2 Protests, C.20.3 Time limit for lodging protests, C.20.4 Appeals, C.20 COMPLAINTS AND PROTESTS

### Community 243 - "F3K.2 DEFINITION OF MODEL GLIDER"
Cohesion: 0.29
Nodes (7): F3K.2.1 Specifications, F3K.2.2 Losing a part of the model glider, F3K.2.3 Change of model glider, F3K.2.4 Retrieving of model glider, F3K.2.5 Radio frequencies, F3K.2.6 Ballast, F3K.2 DEFINITION OF MODEL GLIDER

### Community 244 - "F3K.9 DEFINITION OF A ROUND"
Cohesion: 0.29
Nodes (7): F3K.9.1 Groups and round scores, F3K.9.2 Working time, F3K.9.3 Landing window, F3K.9.4 Preparation time, F3K.9.5 Flight testing time, F3K.9.6 Re-Flights, F3K.9 DEFINITION OF A ROUND

### Community 245 - "5.5.2 CONTEST RULES"
Cohesion: 0.29
Nodes (7): 5.5.2.1 Definition of an Official Flight, 5.5.2.2 Cancelling of a Flight and Disqualification, 5.5.2.3 Organisation of the Contest, 5.5.2.4 Organisation of Starts, 5.5.2.5 Processing of Energy Limiters, 5.5.2.6 Judging, 5.5.2 CONTEST RULES

### Community 247 - "Normalisation"
Cohesion: 0.29
Nodes (7): Decision matrix — `GroupScoreOption` × task family × `varFltDednIdx`, Names and configuration plumbing, Normalisation, Re-run paths (who recomputes normalised scores), Shape of `Update_GroupScores` (`Scoring_MOD.vb:247-486`), Unresolved, Validation gate — sample comp reproduced from the matrix

### Community 248 - "Soarscore.Application.csproj"
Cohesion: 0.29
Nodes (5): Microsoft.AspNetCore.OpenApi, Microsoft.NET.Sdk.Web, $(SoarscoreTargetFramework), $(SoarscoreTargetFramework), Microsoft.NET.Sdk

### Community 249 - "Soarscore.Infrastructure.csproj"
Cohesion: 0.29
Nodes (6): Microsoft.Data.Sqlite, Npgsql, $(SoarscoreTargetFramework), Fisher, Marten, Microsoft.NET.Sdk

### Community 251 - "TaskDefinition"
Cohesion: 0.06
Nodes (34): TaskDefinition, Code, Flights, FlightValidWhen, Group, Metrics, Name, Normalise (+26 more)

### Community 252 - "BindParameterPropertyTests"
Cohesion: 0.28
Nodes (4): Ref, DateTimeOffset, Fact, BindParameterPropertyTests

### Community 253 - ".Apply"
Cohesion: 0.40
Nodes (4): EntryProjection, DateTimeOffset, Fact, EntryProjectionTests

### Community 254 - "F5 Electric Soaring — Generally Applicable Rules"
Cohesion: 0.29
Nodes (7): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score, 4. Round & final score, 5. Re-flights, F5 Electric Soaring — Generally Applicable Rules, Source references

### Community 255 - ".BuildDispatcher"
Cohesion: 0.25
Nodes (12): IQueryHandler, CancellationToken, IEventStore, Task, GetClassDefinition, GetClassDefinitionHandler, Fact, FakeClock (+4 more)

### Community 256 - ".Decide"
Cohesion: 0.24
Nodes (5): DateOnly, DateOnly, Fact, Gen, CompetitionDecidePropertyTests

### Community 259 - "IDomainEvent"
Cohesion: 0.12
Nodes (21): CancellationToken, Entry, IEventStore, Task, Version, EntryLoader, IDomainEvent, DateTimeOffset (+13 more)

### Community 260 - "F3B — RC Multi-Task Gliders"
Cohesion: 0.22
Nodes (9): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score — three partial scores (`F3B.2.6`), 4. Round score (`F3B.2.7`), 5. Final classification (`F3B.2.8`), 6. Re-flights (`F3B.1.5`, `F3B.1.11`), F3B — RC Multi-Task Gliders, Penalty schedule (+1 more)

### Community 261 - "LayerRuleTests"
Cohesion: 0.47
Nodes (3): Architecture, Fact, LayerRuleTests

### Community 262 - "LADR-0003 — Library choices"
Cohesion: 0.33
Nodes (5): Choices, Closed — `System.Text.Json` and the class-definition hierarchy, Deliberately not used, LADR-0003 — Library choices, Open

### Community 263 - "3.1 CLASS A: 6 MINUTE THERMAL DURATION"
Cohesion: 0.33
Nodes (6): 3.1.1 Launching, 3.1.2 Scoring, 3.1.3 Number of Flights, 3.1.4 Flights at end of round., 3.1.5 NDC Competition, 3.1 CLASS A: 6 MINUTE THERMAL DURATION

### Community 264 - "3.7 CLASS H : NEW ZEALAND THERMAL 2 METRE RULES"
Cohesion: 0.33
Nodes (6): 3.7.1 The model, 3.7.2 Launching, 3.7.3 Flying, 3.7.4 Landing, 3.7.5 Scoring, 3.7 CLASS H : NEW ZEALAND THERMAL 2 METRE RULES

### Community 266 - "C.19.1 Penalties imposed by the Contest Director"
Cohesion: 0.33
Nodes (6): C.19.1.1 Range of penalties imposed by the Contest Director with the consent of the FAI Jury, C.19.1.2 Information and publication, C.19.1 Penalties imposed by the Contest Director, C.19.2.1 Types of penalties imposed by CIAM Bureau, C.19.2 Penalties imposed by CIAM Bureau, C.19 PENALTIES

### Community 267 - "C.7 CONTEST OFFICIALS"
Cohesion: 0.33
Nodes (6): C.7.1 FAI Jury, C.7.2 FAI Jury at World and Continental Championships & WAG, C.7.3 FAI Jury at Open International, C.7.4 World Cup Board, C.7.5 Contest officials, C.7 CONTEST OFFICIALS

### Community 268 - "CreateCompetitionPropertyTests"
Cohesion: 0.33
Nodes (6): DurationDays, OffsetDays, Location, Gen, Name, CreateCompetitionPropertyTests

### Community 270 - "Story — Coverage: normalisation is per group, not per round"
Cohesion: 0.33
Nodes (5): As built — notes, Deferred, Story — Coverage: normalisation is per group, not per round, What, Why it mattered

### Community 271 - "Stop storing `Entry.WorkingTime`"
Cohesion: 0.33
Nodes (6): Before starting, Decisions — user-confirmed 2026-08-21, Stop storing `Entry.WorkingTime`, What, What a removal must not break, Why it matters

### Community 272 - "Competitor"
Cohesion: 0.25
Nodes (6): Competitor, CompetitorNumber, Id, PersonRef, RegisteredAt, WithdrawnAt

### Community 273 - "IClassFixture"
Cohesion: 0.17
Nodes (15): IClassFixture, CancellationToken, IClock, IEventStore, Task, AppendReflightGroupHandler, Competitors, CancellationToken (+7 more)

### Community 274 - ".HandleAsync"
Cohesion: 0.18
Nodes (15): CancellationToken, IClock, IEventStore, Task, AmendMeasurementHandler, CancellationToken, DateTimeOffset, Fact (+7 more)

### Community 275 - "F3J.8 LAUNCHING"
Cohesion: 0.25
Nodes (8): F3J.8.1 Start Direction, F3J.8.2 Launching, F3J.8.3 Launching Procedure, F3J.8.4 Launching Area, F3J.8.5 Launching Device, F3J.8.6 Early Start, F3J.8.7 Towlines, F3J.8 LAUNCHING

### Community 276 - "SoarscoreEventTypes.cs"
Cohesion: 0.40
Nodes (4): Alias, IReadOnlyList, Type, SoarscoreEventTypes

### Community 277 - "IClock"
Cohesion: 0.29
Nodes (6): DateTimeOffset, IClock, UtcNow, DateTimeOffset, FakeClock, UtcNow

### Community 278 - "3.2 CLASS B: 10 MINUTE THERMAL DURATION"
Cohesion: 0.40
Nodes (5): 3.2.1 Launching: The launch of the model may be by one of the following means:, 3.2.2 Scoring, 3.2.3 Number of Flights, 3.2.4 National Decentralised Contest (NDC), 3.2 CLASS B: 10 MINUTE THERMAL DURATION

### Community 279 - "3.9 CLASS J: THERMAL 2,4,6,8,10"
Cohesion: 0.40
Nodes (5): 3.9.1 Launching, 3.9.2 Scoring, 3.9.3 Contest time, 3.9.4 NDC Competition, 3.9 CLASS J: THERMAL 2,4,6,8,10

### Community 284 - "F3K.10 SCORING"
Cohesion: 0.40
Nodes (5): F3K.10.1 Final score, F3K.10.2 Resolution of a tie, F3K.10.3 Fly-off, F3K.10.4 Team Classification, F3K.10 SCORING

### Community 285 - "F3K.4 SAFETY"
Cohesion: 0.40
Nodes (5): F3K.4.1 Contact with a person, F3K.4.2 Mid air collision, F3K.4.3 Safety area, F3K.4.4 Forbidden airspace, F3K.4 SAFETY

### Community 287 - "Corpus.cs"
Cohesion: 0.50
Nodes (4): ImmutableArray, Corpus, All, SeedClass

### Community 288 - "Story — Resolve GliderScore scoring arithmetic from source"
Cohesion: 0.40
Nodes (4): Before starting, Story — Resolve GliderScore scoring arithmetic from source, What, Why it matters

### Community 289 - "Findings"
Cohesion: 0.40
Nodes (5): Divergences from FAI/NZ rules, Findings, Formula narrative (consolidated), Handoff notes, Reconciliation result

### Community 290 - "F3J.11 FINAL CLASSIFICATION"
Cohesion: 0.40
Nodes (5): F3J.11.2 Fly-off Working Time, F3J.11.3 Fly-off Scoring, F3J.11.4 Final Placing, F3J.11.5 Ranking for International Team Classification, F3J.11 FINAL CLASSIFICATION

### Community 291 - "F3J.1 GENERAL RULES"
Cohesion: 0.40
Nodes (5): F3J.1.1 Definition of a Radio-Controlled Glider, F3J.1.2 Prefabrication of the Model aircraft, F3J.1.3 Characteristics of Radio-Controlled Gliders, F3J.1.4 Competitors and Helpers, F3J.1 GENERAL RULES

### Community 292 - "F3J.2 THE FLYING SITE"
Cohesion: 0.40
Nodes (5): F3J.2.1 Site Surface, F3J.2.2 Site Marking, F3J.2.3 Landing Spots, F3J.2.4 Safety Rules, F3J.2 THE FLYING SITE

### Community 293 - "F3J.6 ORGANISATION OF THE FLYING"
Cohesion: 0.50
Nodes (4): F3J.12.1 for details., F3J.6.1 Rounds and Groups, F3J.6.2 Flying in Groups, F3J.6 ORGANISATION OF THE FLYING

### Community 294 - "F3J.13 ADVISORY INFORMATION"
Cohesion: 0.50
Nodes (4): F3J.13.1 Organisational Requirements, F3J.13.2 Time-keeper Duties, F3J.13.3 Groups, F3J.13 ADVISORY INFORMATION

### Community 295 - "extract.py"
Cohesion: 0.70
Nodes (4): encode(), extract_table(), main(), type_name()

### Community 296 - "GliderScore fixture corpus index"
Cohesion: 0.40
Nodes (4): Competitions, Diversity wanted, GliderScore fixture corpus index, Standing skip reasons

### Community 297 - "F3J.9 LANDING"
Cohesion: 0.50
Nodes (4): F3J.9.1 Landing SCircle, F3J.9.2 Timekeeper Position, F3J.9.3 Model Retrrieving, F3J.9 LANDING

### Community 299 - "Seed classes — the authoring source"
Cohesion: 0.40
Nodes (4): How the notation maps, Running it, Seed classes — the authoring source, Status of the transcription

### Community 302 - "C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY"
Cohesion: 0.50
Nodes (4): C.10.1 Class F - Model aircraft, C.10.2 Class S - Space models, C.10.3 General requirements, C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY

### Community 303 - "C.15.6 Classification"
Cohesion: 0.50
Nodes (4): C.15.6.1 Individual classification, C.15.6.2 National team classification, C.15.6.3 Overall classification in multiple contest categories, C.15.6 Classification

### Community 307 - "F3K.1 GENERAL"
Cohesion: 0.50
Nodes (4): F3K.1.1 Timekeepers, F3K.1.2 Helper, F3K.1.3 Transmitter Pound, F3K.1 GENERAL

### Community 308 - "5.5.11.1 General Rules"
Cohesion: 0.50
Nodes (4): 5.5.11.1.1 Definition of a Radio Controlled Glider with Electric Motor, 5.5.11.1.2 Prefabrication of the Model Aircraft, 5.5.11.1.3 Characteristics of Radio Controlled Gliders with electric motor and altimeter/motor run, 5.5.11.1 General Rules

### Community 309 - "opencode.json"
Cohesion: 0.50
Nodes (3): plugin, $schema, .opencode/plugins/graphify.js

## Knowledge Gaps
- **1815 isolated node(s):** `context7`, `rider`, `$schema`, `.opencode/plugins/graphify.js`, `$(SoarscoreTargetFramework)` (+1810 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ClassDefinition` connect `ClassDefinition` to `.Decide`, `PublishedClassDefinition`, `.ScoreCompetition`, `FakeEntryQuery`, `.BuildCompetitionWithPenalties`, `.HandleAsync`, `WithdrawCompetitorHandler`, `ClassDefinitionPublished`, `CatalogueDrawPropertyTests`, `.CheckLimits`, `ResolvedTask`, `.Of`, `CreateCompetitionPropertyTests`, `PrescribeDrawDecideTests`, `.New`, `CompetitionId`, `EntryId`, `.HandleAsync`, `PhaseDrawn`, `IStoreFixture`, `EntryModelBasedFoldTests`, `IClassFixture`, `MeasuredValue`, `ICommandHandler`, `SeedF5J`, `DrawAcceptanceEventStoreTests`, `Then`, `.New`, `FixtureModels.cs`, `.Exact`, `Corpus.cs`, `FlightOpened`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `ScoringACompetitionSteps`, `.SetUpAsync`, `SystemClock`, `CaptureMeasurement`, `DateTimeOffset`, `RecordingAReflightRulingSteps`, `.SeedCompetition`, `.ScoreGroup`, `FinaliseDecideTests`, `.BuildDrawnCompetition`, `.SeedOpenEntry`, `GliderscoreFixture`, `PenaltyDefinition`, `.Rank`, `.MapQueries`, `.CompetitionAdopting`, `ClassDefinitionValidationPropertyTests`, `.Apply`, `.BuildDrawnCompetition`, `.BuildDrawnCompetition`, `.New`, `.Validate`, `.DrawnCompetitionAsync`, `DrawAcceptanceDecideTests`, `MetricDefinition`, `SeedF5K`, `CompetitionReplaceTaskRoundPropertyTests`, `.New`, `PublishClassDefinition`, `SeeingWhatIsRecordedSteps`, `.SeedCompetition`, `BindParameterPropertyTests`, `DrawAcceptancePropertyTests`, `PhaseDefinition`, `.HandleAsync`, `TaskDefinition`, `ReflightRule`, `.BuildDispatcher`?**
  _High betweenness centrality (0.083) - this node is a cross-community bridge._
- **Why does `CompetitorId` connect `CompetitorId` to `.ScoreCompetition`, `Soarscore.Domain.PublishedClassDefinition`, `.BuildCompetitionWithPenalties`, `FakeEntryQuery`, `IDomainEvent`, `.HandleAsync`, `WithdrawCompetitorHandler`, `CatalogueDrawPropertyTests`, `PrescribeDrawDecideTests`, `CaptureMeasurementDecideTests`, `CompetitionId`, `Competitor`, `EntryId`, `GroupId`, `ICommandHandler`, `PhaseDrawn`, `IStoreFixture`, `IClassFixture`, `EntryCapturePropertyTests`, `DrawAcceptanceEventStoreTests`, `Then`, `.New`, `.Exact`, `Entry`, `.CompareRawGrainAsync`, `AnnulEntryDecideTests`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `ScoringACompetitionSteps`, `AmendMeasurementDecideTests`, `FlightOpened`, `OpenFlightDecideTests`, `ReflightSelection`, `SystemClock`, `.SetUpAsync`, `TaskRoundRecordingPropertyTests`, `Competition`, `DateTimeOffset`, `RecordingAReflightRulingSteps`, `.SeedCompetition`, `.BuildDrawnCompetition`, `FinaliseDecideTests`, `TaskRound`, `.MapQueries`, `.BuildDrawnCompetition`, `.BuildDrawnCompetition`, `.ComputeEntries`, `.New`, `.DrawnCompetitionAsync`, `EntryEventJsonTests`, `DrawAcceptanceDecideTests`, `.New`, `SeeingWhatIsRecordedSteps`, `.SeedCompetition`, `.HandleAsync`, `ReflightRule`, `RecordEntryPenaltyDecideTests`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Why does `CompetitionId` connect `CompetitionId` to `.Decide`, `Soarscore.Domain.PublishedClassDefinition`, `.LoadCurrentAsync`, `FakeEntryQuery`, `IDomainEvent`, `.HandleAsync`, `WithdrawCompetitorHandler`, `DrawingACatalogueChoicePhaseSteps`, `PrescribeDrawDecideTests`, `CaptureMeasurementDecideTests`, `.New`, `EntryId`, `ICommandHandler`, `GroupId`, `.HandleAsync`, `CompetitionSummary`, `IStoreFixture`, `IClassFixture`, `EntryCapturePropertyTests`, `DrawAcceptanceEventStoreTests`, `Then`, `.Exact`, `Entry`, `.CompareRawGrainAsync`, `AnnulEntryDecideTests`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `ScoringACompetitionSteps`, `AmendMeasurementDecideTests`, `SystemClock`, `FlightOpened`, `OpenFlightDecideTests`, `.SetUpAsync`, `Competition`, `RecordingAReflightRulingSteps`, `.SeedCompetition`, `.SeedOpenEntry`, `.MapQueries`, `CompetitorId`, `.DrawnCompetitionAsync`, `EntryEventJsonTests`, `.New`, `PublishClassDefinition`, `SeeingWhatIsRecordedSteps`, `RecordEntryPenaltyDecideTests`?**
  _High betweenness centrality (0.044) - this node is a cross-community bridge._
- **What connects `context7`, `rider`, `$schema` to the rest of the system?**
  _1815 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Soarscore.Domain.PublishedClassDefinition` be split into smaller, more focused modules?**
  _Cohesion score 0.05788149164950812 - nodes in this community are weakly interconnected._
- **Should `FakeEntryQuery` be split into smaller, more focused modules?**
  _Cohesion score 0.05675877520537715 - nodes in this community are weakly interconnected._
- **Should `SeedF5J` be split into smaller, more focused modules?**
  _Cohesion score 0.09401709401709402 - nodes in this community are weakly interconnected._