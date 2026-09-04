# Graph Report - SoarScore2  (2026-09-04)

## Corpus Check
- 643 files · ~1,102,102 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 8039 nodes · 21791 edges · 399 communities (383 shown, 16 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 2339 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `02094d93`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Competition
- Soarscore.Domain.PublishedClassDefinition
- FakeEntryQuery
- ReflightingForAMissedRoundSteps
- SystemClock
- test_gsclient.py
- Work items
- Soarscore.Domain.Competitions
- CatalogueDrawPropertyTests
- FlagOrParam
- Teams: Findings and Implementation Options
- ScoringTeamCommandHandlerTests
- PrescribeDrawDecideTests
- TaskRoundScore
- .New
- .MapQueries
- ResolvedTask
- EntryId
- IStoreFixture
- .All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state
- .CheckLimits
- Corpus.cs
- ClassDefinition
- MeasuredValue
- Soarscore.Application.Queries.CompetitionClasses
- AcceptanceFixture
- EntryCapturePropertyTests
- Soarscore.Application.Commands.CompetitionClasses
- ClosingACompetitionSteps
- .New
- GliderscoreFixture
- IServiceProvider
- Entry
- 3.16 CLASS Q: NZ F5K (Hand Launch Electric Glider)
- Comparator
- FlightOpened
- ReflightingAGroupSteps
- AcceptingTheDrawSteps
- ScoringACompetitionSteps
- AmendMeasurementDecideTests
- ProtectionGroupId
- .Of
- Plan — Capturing a score: the Entry write path and `entry_index`
- Plan — Scoring: de-orphaning the scoring engine
- TaskResult
- The Competition Class notation — draft spec
- TaskRoundRecordingPropertyTests
- PhaseAggregatorTests
- Refined plan
- CompetitionEvent
- When
- 2 SOARING (All Classes)
- B.4 DEFINITIONS OF EXPRESSIONS
- ScoringTeamsSteps
- .LoadCurrentAsync
- FinaliseDecideTests
- ScoringTeamId
- Scoring Service Build Plan
- OpenFlight
- FakeEventStore
- Work items
- MetricDefinition
- .Exact
- JasperFxEventStore
- Plan — Catalogue-choice draws: the CD picks each round's task
- ReflightRole
- CompetitionReplaceTaskRoundPropertyTests
- Story — Normalisation lower clamp (floor NormalisedScore at 0)
- Design decisions — settled here, do not relitigate
- GroupSpotsPropertyTests
- Work items
- TeamsDecideTests
- .CompetitionAdopting
- 3.4 CLASS D : THERMAL FORMULA 500
- Plan — The CD's choices: `BindParameter`
- Context
- Work items
- CompetitionId
- Person
- .BuildDrawnCompetition
- TaskRoundCompleted
- .HandleAsync
- Work items
- .ComputeEntries
- ScoringServicePropertyTests
- Story — Model tie-break policy as class data
- .Validate
- Core Principles
- 00-general-rules.md
- RC Soaring Competitions — Key Concepts
- Plan — Command-side steel thread: Person end-to-end
- HarnessSelfCheckSteps
- .DrawnCompetitionAsync
- .New
- .AppendAsync
- Work items
- DrawAcceptanceDecideTests
- 5.5.10 F5K – RC THERMAL DURATION GLIDERS FOR MULTIPLE TASK COMPETITION WITH
- Work items
- Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor`
- test_triage.py
- ReplayDriver
- PersonRegistered
- F3F.1 GENERAL RULES
- IDispatcher
- Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition`
- Design decisions (settled here, cited from code)
- PublishClassDefinition
- Story — Entry-scoped point-deduction penalties are inert
- RecordCompetitionPenaltyDecideTests
- 6 F3L – RADIO CONTROLLED THERMAL GLIDERS RES
- 5.5.11 CLASS F5J – RC ELECTRIC POWERED THERMAL DURATION GLIDERS
- 5.5.12 CLASS F5L – RADIO CONTROLLED THERMAL GLIDERS RES WITH ELECTRIC MOTOR AND
- MartenEventStore
- FisherEventStore
- Work items
- LADR-0001 — Event store: PostgreSQL + Marten
- SECTION C - CIAM GENERAL RULES FOR INTERNATIONAL EVENTS
- C.15 ORGANISATION OF WORLD AND CONTINENTAL CHAMPIONSHIPS
- ScoringVocabulary.cs
- Plan — Create-competition steel thread: `CreateCompetition`
- EndpointRouteBuilderExtensions
- .Normalise
- 2. Findings
- .ScoreCompetition
- RegisterCompetitorHandler
- Rule map — topic × class
- SECTION A - CIAM INTERNAL REGULATIONS
- Design decisions — settled here, do not relitigate
- IDomainEvent
- F3K.11 DEFINITIONS OF TASKS
- IProjection
- Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server
- WithdrawCompetitorHandler
- .LoadCurrentAsync
- .BuildDispatcher
- 1 GENERAL DEFINITIONS
- .AddSoarscoreInfrastructure
- .Classify
- PhaseDefinition
- NzNdcSeedArithmeticTests
- SeedF3K
- F3G.1 GENERAL RULES
- Gliderscore Jet DB — Schema Analysis and Indicative Domain Mapping
- Story — Gliderscore golden-fixture pipeline
- .BuildDrawnCompetition
- Pre-requisites (sub-agent dispatchable — gate WI-1–4)
- ReplaySteps
- triage.py
- EntryModelBasedFoldTests
- CLAUDE.md — Soarscore
- fai-rule.sh
- RankingEnginePropertyTests
- validate.py
- TaskDefinition
- 4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS
- PART 5 – TECHNICAL REGULATIONS FOR RADIO
- Plan
- Plan
- Story — Move the store adapters onto the JasperFx shared contracts
- Raw score
- PrescribeDrawPropertyTests
- GliderScore fixture extraction
- ReflightRule
- test_csvparse.py
- OpenFlightDecideTests
- mine_catalogue.py
- NZ Class M — ALES 200 (Altitude Limited Electric Soaring)
- PenaltyEffect
- 3.17 CLASS R: E-RES 2M (Electric Rudder Elevator Spoiler 2M Glider)
- BindParameterPropertyTests
- DrawOp
- Soarscore — Users
- Drop-worst
- ClassDefinitionSummary
- DrawingACatalogueChoicePhaseSteps
- PersonSummary
- LADR-0002 — Competition Class definition: representation, ingestion and identity
- .Select
- F5K — RC Electric Thermal Duration, Multiple-Task
- RecordEntryPenaltyDecideTests
- F3J — RC Thermal Duration Gliders
- C.16.2 Requirements for radio control
- C.2.1 First category events
- 5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS
- Plan
- Scoring Service — Open Design Issues
- Soarscore.Acceptance.Tests.csproj
- Soarscore.sln
- test_fetch_comp.py
- FinaliseValidityPropertyTests
- Soarscore.Infrastructure.Tests.csproj
- non-functional-requirements.md
- Competition rules for RC soaring
- Competition Rules — Generally Applicable (all contest types)
- GsClient
- ScoreTerm
- C.13 REQUIREMENTS FOR ORGANISATION OF INTERNATIONAL EVENTS
- C.21 CIAM TROPHIES
- C.5.1 Competitor
- CompetitionSummary
- 5.5.3 CLASS F5A – RC ELECTRIC POWERED GPS MOTOR GLIDERS (PROVISIONAL RULE)
- 5.5.4 CLASS F5B – RC ELECTRIC POWERED MULTI TASK GLIDERS
- ClassDefinitionValidationPropertyTests
- csvparse.py
- .TwoRoundCompetitionAsync
- Entry-completeness indicator
- Plan
- Ranking & tie-breaks
- Deferred decisions
- .LoadAsync
- Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC
- ScoringCorpusPropertyTests
- Compliance check
- .ApplyAggregatePenalties
- test_mine_catalogue.py
- RC Soaring Competitions — Domain Class Diagram
- GroupId
- F5L — RC Electric Thermal Gliders, RES
- NZ Soaring — Generally Applicable Rules
- 3.10 CLASS K: Thermal R.E.S. (Rudder, Elevator, Spoiler)
- 3.12 CLASS M: ALES 200 (Altitude Limited Electric Soaring)
- C.18 SAFETY
- .Seed
- 5.5.1 GENERAL RULES
- Work items
- Plan — Per-round parameter bindings
- Remove `Flight.LaunchAt`
- Precision & storage
- Plan
- Soarscore.Architecture.Tests.csproj
- DrawProtectionPropertyTests
- Soarscore.Application.Tests.csproj
- ResultTests
- SeeingWhatIsRecordedSteps
- Soarscore.Domain.Tests.csproj
- RC Soaring — Aggregate Boundaries
- f3k-june-2020/ladder.py
- TeamClassificationEngineTests
- 2.4 LANDING
- 3.3 CLASS C: PREMIER THERMAL DURATION.
- f5j-christchurch-2019/ladder.py
- C.20 COMPLAINTS AND PROTESTS
- TeamClassificationPropertyTests
- .DrawnCompetitionAsync
- 5.5.2 CONTEST RULES
- Model
- Normalisation
- Soarscore.Application.csproj
- Soarscore.Infrastructure.csproj
- NZMAA Flying Rules, Section 5: Soaring — March 2024 (extracted source text)
- SeedF5kNdc
- ScoringServiceZeroRoutingTests
- Story — Permitted scopes on PenaltyDefinition (adoption-time scope hardening)
- Competitor
- AssigningSpotsSteps
- Story — Ranking's secondary key: RawScore tie-break
- TeamsEventJsonTests
- Story — GliderScore webmine tool (read-only online comp acquisition)
- .ScoreGroup
- PersonId
- LayerRuleTests
- LADR-0003 — Library choices
- 3.1 CLASS A: 6 MINUTE THERMAL DURATION
- 3.7 CLASS H : NEW ZEALAND THERMAL 2 METRE RULES
- fetch_comp.py
- C.19.1 Penalties imposed by the Contest Director
- C.7 CONTEST OFFICIALS
- .BuildDrawnCompetition
- People/TestDoubles.cs
- Story — Coverage: normalisation is per group, not per round
- Stop storing `Entry.WorkingTime`
- FakeEventStore
- .Apply
- .SetUpAsync
- OpKind
- AdoptedRules
- Work items
- 3.2 CLASS B: 10 MINUTE THERMAL DURATION
- 3.9 CLASS J: THERMAL 2,4,6,8,10
- F3B.1 GENERAL RULES
- reflight-aggregate-destination.md
- .BuildGroups
- webmine/ — GliderScore online competition acquisition (read-only)
- DrawPhase
- SeedNzF3kNdc
- .PostCommandRawAsync
- .Rank
- Story — Resolve GliderScore scoring arithmetic from source
- Findings
- RecordCompetitionPenalty
- .Apply
- Story — NZ F3K NDC seed class
- CompetitorId
- f5j-nz-south-island/ladder.py
- extract-mssql.py
- GliderScore fixture corpus index
- PenaltyDefinition
- Refined plan
- Seed classes — the authoring source
- NumberOrParam
- RecordEntryPenalty
- C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY
- C.15.6 Classification
- .Aggregate
- AnnulEntryDecideTests
- F3J.10 SCORING
- .SeedDrawnCompetitionAsync
- 5.5.11.1 General Rules
- opencode.json
- .SeedCompetitionWithProtectedPair
- PostgresFixture
- GliderScore golden comparison — state after http-grain-one-metric-bridge
- FixtureLoader
- Story — Smaller items
- .mcp.json
- graphify.js
- F3 Soaring — Generally Applicable Rules
- F3K — RC Hand-Launch Gliders
- IQuery
- CompetitionResult
- F5 Electric Soaring — Generally Applicable Rules
- tech-debt.md
- Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness)
- .RunPayoffCaptureScenarioAsync
- TeamContributionState
- gsclient.py
- TaskRound
- ParameterBinding
- RegisterCompetitorPropertyTests
- .HandleAsync
- Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus)
- .OpenAndCaptureFlightAsync
- A.5 PLENARY MEETING
- Story — webmine agent-skill wrapper
- F3J.8 LAUNCHING
- Story — OmitFromTeamScore=true witness fixture
- Penalty
- .DescribeContributors
- .ComputeGroupViews
- NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)
- ClassDefinitionPublished
- DocumentClassLibraryQuery
- F3B — RC Multi-Task Gliders
- SoarscoreEventTypes
- .HandleAsync
- _FormScanner
- F5J — RC Electric Powered Thermal Duration Gliders
- EntryEventJsonTests
- Soarscore.Application.Tests.Shared.Entries
- _documented_row
- F3J.11 FINAL CLASSIFICATION
- F3J.1 GENERAL RULES
- F3J.2 THE FLYING SITE
- Soarscore.Application
- Story — Operational tie-break resolution: record the outcome, re-rank
- NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)
- .ApplyRounding
- PenaltyEnginePropertyTests
- Story — Literal record replay scenarios (a whole fixture readable as Gherkin)
- Result
- Story — Seed-definition parallel run (corpus fixtures under the seed classes)
- .ApplyAsync
- DropPolicy
- FakeClock
- SeedF3B
- FakeTransport
- RoundComposition
- F3J.6 ORGANISATION OF THE FLYING
- F3J.13 ADVISORY INFORMATION
- ExpectedVersion
- F3J.9 LANDING
- .NormalisedDefinition
- Fact
- F3K.2 DEFINITION OF MODEL GLIDER
- Story — F5K fixture from the GliderScore server DB export
- RulesAmendment
- Mutation
- SeedNzNAles123
- SeedNzPRadian
- NZ ALES — Generally Applicable Rules
- CreateCompetitionPropertyTests
- Model
- .EvaluateTerm
- PersonTests
- ScoringServiceAnnulmentTests
- FlightModel
- CompetitorModel
- .Person_name_search_is_case_insensitive_and_matches_a_substring
- 2.5 CONTESTS
- .ComputeContentHash_is_deterministic_and_content_sensitive
- Actual
- Actual
- MeasurementModel
- .Random_event_sequences_fold_to_the_structurally_matching_reference_model
- .Random_event_sequences_fold_to_the_structurally_matching_reference_model
- .NextUnused

## God Nodes (most connected - your core abstractions)
1. `CompetitorId` - 268 edges
2. `CompetitionId` - 209 edges
3. `ClassDefinition` - 200 edges
4. `Soarscore.Domain.PublishedClassDefinition` - 196 edges
5. `Soarscore.Domain.Competitions` - 172 edges
6. `Result` - 132 edges
7. `Soarscore.Domain` - 115 edges
8. `Soarscore.Domain.People` - 111 edges
9. `EntryId` - 110 edges
10. `Soarscore.SeedData` - 109 edges

## Surprising Connections (you probably didn't know these)
- `Row` --references--> `CompetitorId`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/TeamClassificationPropertyTests.cs → src/Soarscore.Domain/Competitions/Competition.cs
- `PhaseDrawPropertyTests` --references--> `Rounds`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/PhaseDrawPropertyTests.cs → src/Soarscore.Domain/Competitions/Competition.cs
- `EntryCapturePropertyTests` --references--> `Kind`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/EntryCapturePropertyTests.cs → src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs
- `PenaltyEnginePropertyTests` --references--> `Count`  [EXTRACTED]
  tests/Soarscore.Domain.Tests/PenaltyEnginePropertyTests.cs → src/Soarscore.Domain/PublishedClassDefinition/ScoringVocabulary.cs
- `ScoringTeamsSteps` --references--> `Members`  [EXTRACTED]
  tests/Soarscore.Acceptance.Tests/Steps/ScoringTeamsSteps.cs → src/Soarscore.Domain/Scoring/TeamClassification.cs

## Import Cycles
- None detected.

## Communities (399 total, 16 thin omitted)

### Community 0 - "Competition"
Cohesion: 0.07
Nodes (27): DateOnly, DateTimeOffset, Defect, Penalty, ReflightRuling, Result, TaskRoundCoordinate, Competition (+19 more)

### Community 1 - "Soarscore.Domain.PublishedClassDefinition"
Cohesion: 0.05
Nodes (12): Soarscore.Domain.Scoring, Soarscore.Domain.Tests, Soarscore.Application.Tests, Soarscore.Domain.People, Soarscore.SeedData, Soarscore.Application.Tests.Queries.Entries, Soarscore.Domain.Entries, Soarscore.Domain.PublishedClassDefinition (+4 more)

### Community 2 - "FakeEntryQuery"
Cohesion: 0.15
Nodes (27): CancellationToken, IClock, IEventStore, Task, OpenEntryHandler, EntrySummary, CancellationToken, IReadOnlyList (+19 more)

### Community 3 - "ReflightingForAMissedRoundSteps"
Cohesion: 0.13
Nodes (12): CompetitionId, Dictionary, Given, Group, HttpClient, HttpResponseMessage, IReadOnlyList, List (+4 more)

### Community 4 - "SystemClock"
Cohesion: 0.17
Nodes (20): CancellationToken, IEventStore, Task, GetCompetition, GetCompetitionHandler, DateTimeOffset, SystemClock, UtcNow (+12 more)

### Community 5 - "test_gsclient.py"
Cohesion: 0.10
Nodes (36): _action_candidates(), _audit_plans(), check_common_audit_fields(), exact_sleep_factory(), execute_op(), FakeClock, FakeTransport, granular_sleep_factory() (+28 more)

### Community 6 - "Work items"
Cohesion: 0.09
Nodes (22): As built (2026-08-26), Before starting — all discharged, Decisions settled during planning (2026-08-26), Execution plan, Known traps (pre-answered by planning — verified against the tree), Out of scope (deferrals restated), Pipeline shape (one feature per fixture, shared machinery), Plan (+14 more)

### Community 7 - "Soarscore.Domain.Competitions"
Cohesion: 0.09
Nodes (9): Soarscore.Domain.Competitions, Soarscore.Application.Shared.Entries, Soarscore.Application.Commands.Competitions, Soarscore.Application.Shared.People, Soarscore.Application.Tests.Shared.Competitions, Soarscore.Application.Queries.Entries, Soarscore.Domain, Soarscore.Application.Tests.Commands.Competitions (+1 more)

### Community 8 - "CatalogueDrawPropertyTests"
Cohesion: 0.14
Nodes (12): MinPerGroupByRound, Sizes, TaskCount, DateTimeOffset, Dictionary, Fact, Field, Gen (+4 more)

### Community 9 - "FlagOrParam"
Cohesion: 0.10
Nodes (19): JsonConverter, JsonSerializerOptions, ClassDefinitionHashing, JsonSerializerOptions, Utf8JsonReader, Utf8JsonWriter, DecimalAsStringConverter, SoarscoreEventJson (+11 more)

### Community 10 - "Teams: Findings and Implementation Options"
Cohesion: 0.05
Nodes (43): Application, API, and infrastructure, Best fit, Best fit, Best fit, Changes required, Changes required, Changes required, Classification and finalisation (+35 more)

### Community 11 - "ScoringTeamCommandHandlerTests"
Cohesion: 0.13
Nodes (21): CancellationToken, IClock, IEventStore, Task, AssignScoringTeamMembershipHandler, CancellationToken, IClock, IEventStore (+13 more)

### Community 12 - "PrescribeDrawDecideTests"
Cohesion: 0.09
Nodes (21): IReadOnlyList, PrescribedGroup, PrescribedRound, CompetitionId, Given, HttpClient, HttpResponseMessage, IEnumerable (+13 more)

### Community 13 - "TaskRoundScore"
Cohesion: 0.26
Nodes (11): RoundData, TaskRoundData, TaskRoundState, Annulled, Complete, TaskRoundScore, Fact, Gen (+3 more)

### Community 14 - ".New"
Cohesion: 0.06
Nodes (39): FieldOp, RoundOrdinal, Competition, DateOnly, CompetitionCreated, TaskCode, EntryQuery, FakeEventStore (+31 more)

### Community 15 - ".MapQueries"
Cohesion: 0.16
Nodes (20): IReadOnlyList, WebApplication, Queries, CancellationToken, IEventStore, ImmutableArray, Task, GetTeamRosters (+12 more)

### Community 16 - "ResolvedTask"
Cohesion: 0.09
Nodes (17): Bindings, ClassDef, ExpectedRawScore, Competition, TaskResolver, AllFlights, ResolvedTask, ResolvedTiming (+9 more)

### Community 17 - "EntryId"
Cohesion: 0.10
Nodes (19): Guid, EntryId, Then, CompetitionId, Dictionary, Given, Group, HttpClient (+11 more)

### Community 18 - "IStoreFixture"
Cohesion: 0.07
Nodes (38): CancellationToken, IClock, IEventStore, ImmutableArray, Task, FinaliseCompetition, FinaliseCompetitionHandler, CancellationToken (+30 more)

### Community 19 - ".All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state"
Cohesion: 0.08
Nodes (33): DeclaredResult, Aggregate, CompetitorRef, Placing, Promoted, DeclaredTeamContributor, CompetitorRef, Placing (+25 more)

### Community 20 - ".CheckLimits"
Cohesion: 0.15
Nodes (9): IReadOnlyList, JsonSerializerOptions, List, ClassDefinitionIngestion, ClassDefinitionIngestionFixtures, Fact, ClassDefinitionIngestionPropertyTests, Fact (+1 more)

### Community 21 - "Corpus.cs"
Cohesion: 0.50
Nodes (4): ImmutableArray, Corpus, All, SeedClass

### Community 22 - "ClassDefinition"
Cohesion: 0.10
Nodes (23): Path, HashSet, IEnumerable, ImmutableArray, IReadOnlyDictionary, List, Phase, Task (+15 more)

### Community 23 - "MeasuredValue"
Cohesion: 0.11
Nodes (25): Exception, Parameter, AllowedValues, BoundAt, DefaultValue, Kind, Name, Unit (+17 more)

### Community 24 - "Soarscore.Application.Queries.CompetitionClasses"
Cohesion: 0.11
Nodes (5): Soarscore.Application.Tests.Commands.CompetitionClasses, Soarscore.Application.Queries.CompetitionClasses, Soarscore.Application.Tests.Shared.CompetitionClasses, Soarscore.Application.Shared.CompetitionClasses, Soarscore.Application.Tests.Queries.CompetitionClasses

### Community 25 - "AcceptanceFixture"
Cohesion: 0.06
Nodes (28): AfterTestRun, BeforeTestRun, Soarscore.Api, Soarscore.ArchitectureTests, GeneratedRegex, HttpMethodMetadata, MethodInfo, Regex (+20 more)

### Community 26 - "EntryCapturePropertyTests"
Cohesion: 0.07
Nodes (29): DecideActual, DecideFlightModel, DecideModel, FlagValue, MetricIndex, NumericValue, Pick, PlannedCapture (+21 more)

### Community 27 - "Soarscore.Application.Commands.CompetitionClasses"
Cohesion: 0.14
Nodes (12): Soarscore.Application.Commands.Entries, Soarscore.Application.Commands.People, Soarscore.Api.Commands, Soarscore.Acceptance.Tests.Support, Soarscore.Api.Queries, Soarscore.Application.Queries.Scoring, Soarscore.Acceptance.Tests.Support.Gliderscore, Soarscore.Application.Commands.CompetitionClasses (+4 more)

### Community 28 - "ClosingACompetitionSteps"
Cohesion: 0.11
Nodes (17): Competition, ImmutableArray, CompetitionView, ImmutableArray, CompetitionScoreView, CompetitionId, DateTimeOffset, Dictionary (+9 more)

### Community 29 - ".New"
Cohesion: 0.11
Nodes (19): TaskRoundState, Annulled, Complete, Drawn, InProgress, DateTimeOffset, Fact, EntryTests (+11 more)

### Community 30 - "GliderscoreFixture"
Cohesion: 0.13
Nodes (30): JsonElement, List, Dictionary, IReadOnlyList, CompetitionFile, CompetitionIdentity, CompetitionScoring, CompPilotRow (+22 more)

### Community 31 - "IServiceProvider"
Cohesion: 0.19
Nodes (10): IServiceProvider, Dictionary, Type, FakeServiceProvider, Dictionary, Type, FakeServiceProvider, Dictionary (+2 more)

### Community 32 - "Entry"
Cohesion: 0.07
Nodes (32): DateTimeOffset, Func, ImmutableArray, Penalty, PenaltyRecorded, Result, Annulment, At (+24 more)

### Community 33 - "3.16 CLASS Q: NZ F5K (Hand Launch Electric Glider)"
Cohesion: 0.05
Nodes (37): 3.16.10 Landing rules:, 3.16.11 Retrieving of model glider, 3.16.12 Safety, 3.16.13 Mid-air collision, 3.16.14 Forbidden airspace, 3.16.15 Weather conditions / Interruptions, 3.16.16 Definition of landing, 3.16.17 Flight time (+29 more)

### Community 34 - "Comparator"
Cohesion: 0.12
Nodes (26): GroupNo, KeyCollection, Mismatches, PilotNo, RoundNo, StandingsCompared, Competition, Dictionary (+18 more)

### Community 35 - "FlightOpened"
Cohesion: 0.13
Nodes (21): FlightPlan, Amendment, At, By, NewValue, Reason, DateTimeOffset, Penalty (+13 more)

### Community 36 - "ReflightingAGroupSteps"
Cohesion: 0.17
Nodes (11): CompetitionId, Dictionary, Given, Group, HttpClient, HttpResponseMessage, List, ProblemDetails (+3 more)

### Community 37 - "AcceptingTheDrawSteps"
Cohesion: 0.14
Nodes (9): CompetitionId, Given, HttpClient, HttpResponseMessage, IReadOnlyList, List, Task, AcceptingTheDrawSteps (+1 more)

### Community 38 - "ScoringACompetitionSteps"
Cohesion: 0.11
Nodes (12): CompetitionId, DateTimeOffset, Dictionary, Given, Group, HttpClient, HttpResponseMessage, IReadOnlyList (+4 more)

### Community 39 - "AmendMeasurementDecideTests"
Cohesion: 0.14
Nodes (13): AmendmentFact, MeasurementDigest, DateTimeOffset, Fact, Gen, ImmutableArray, IReadOnlyList, AmendMeasurementDecideTests (+5 more)

### Community 40 - "ProtectionGroupId"
Cohesion: 0.14
Nodes (25): CancellationToken, IClock, IEventStore, Task, AddProtectionGroupMemberHandler, CancellationToken, IClock, IEventStore (+17 more)

### Community 41 - ".Of"
Cohesion: 0.15
Nodes (7): Fact, InlineData, Theory, BindParameterDecideTests, Fact, ImmutableArray, CaptureMeasurementDecideTests

### Community 42 - "Plan — Capturing a score: the Entry write path and `entry_index`"
Cohesion: 0.06
Nodes (32): Acceptance, Context, Dependency order, Finding 1 — `entry_index` cannot be built from the Entry events as they stand · **fixed here**, Finding 2 — `TimeWindow.End` cannot be stated under `UntilAllFlightsComplete` · **fixed here**, Finding 3 — `OpenFlight` must not gate on the working-time window · **scope removal**, Finding 4 — capture-time rounding · **decided: apply it**, Four findings that shape the scope (+24 more)

### Community 43 - "Plan — Scoring: de-orphaning the scoring engine"
Cohesion: 0.06
Nodes (32): Acceptance, Context, Dependency order, Finding 1 — `ScoreCompetition` is a shell, not a mis-typed method, Finding 2 — amendment resolution exists nowhere in the tree, Finding 3 — the engine speaks `string`, the domain speaks typed ids, Finding 4 — `RecordedPenalty` and `Penalty` do not have the same shape, Finding 5 — nothing ever marks a task-round `Complete`, so the leaderboard must derive its own field (+24 more)

### Community 44 - "TaskResult"
Cohesion: 0.33
Nodes (7): PenaltyEffectSpec, RawPenaltyApplication, RecordedPenalty, SelectedFlights, TaskResult, Fact, PenaltyEngineTests

### Community 45 - "The Competition Class notation — draft spec"
Cohesion: 0.06
Nodes (30): 10. Findings F1–F15, 11. Findings F16–F21, 12. Findings F22–F23 — the F3F probe, 13. Findings F24–F27 — the NZ probe, 14. Finding F28 — the F3F re-check, 1. Three rules the notation obeys, 2. Shape, 3. Class level (+22 more)

### Community 46 - "TaskRoundRecordingPropertyTests"
Cohesion: 0.11
Nodes (27): GenEntry, GenFlight, Noise, PlacedEntry, Shape, Competition, DateTimeOffset, Entry (+19 more)

### Community 47 - "PhaseAggregatorTests"
Cohesion: 0.36
Nodes (4): Fact, ImmutableArray, IReadOnlyDictionary, PhaseAggregatorTests

### Community 48 - "Refined plan"
Cohesion: 0.09
Nodes (21): API — `src/Soarscore.Api` (`Commands.cs` / `Queries.cs`, kebab-case), Application commands — `src/Soarscore.Application/Commands/Competitions/`, Application queries — derived in-handler from the Competition aggregate (no new read-model documents), Classification engine — new `src/Soarscore.Domain/Scoring/TeamClassification.cs`, Cross-reference (house rule 2 — done during refinement, 2026-09-02), Decide functions (`Competition.cs`, defect-chain style, own code prefixes), Decisions settled with the owner (2026-09-02), Domain model — all inside the Competition aggregate (+13 more)

### Community 49 - "CompetitionEvent"
Cohesion: 0.08
Nodes (32): PenaltyRecorded, DateTimeOffset, Group, ImmutableArray, Penalty, ReflightRuling, CompetitionEvent, CompetitorWithdrawn (+24 more)

### Community 50 - "When"
Cohesion: 0.15
Nodes (11): When, CompetitionId, Given, Group, HttpClient, HttpResponseMessage, List, ProblemDetails (+3 more)

### Community 51 - "2 SOARING (All Classes)"
Cohesion: 0.07
Nodes (30): 2.1 THERMAL SOARING, 2.2.1 General., 2.2.2 Launch apparatus shall conform to the following specifications:, 2.2 LAUNCHING, 2.3.1 Timing of the flight commences when the parachute/pennant is seen to drop from the, 2.3.2 Timing of the flight shall finish when the sailplane first touches the ground or a ground based, 2.3.3 Models already in the air and being timed at the completion of the round, may complete that, 2.3.4 If the sailplane comes into contact with a person during the flight and before the model (+22 more)

### Community 52 - "B.4 DEFINITIONS OF EXPRESSIONS"
Cohesion: 0.04
Nodes (47): B.1.1 General definition, B.1.2.1 Category F1 - Free Flight, B.1.2.2 Category F2 - Control Line Flight, B.1.2.3 Category F3 - Radio Controlled Flight, B.1.2.4 Category F4 - Scale Model Aircraft, B.1.2.5 Category F5 - Radio Control Electric Powered Aircraft, B.1.2.6 Category F7 - Radio Controlled Aerostats, B.1.2.7 Category F9 - Drone Sports (+39 more)

### Community 53 - "ScoringTeamsSteps"
Cohesion: 0.15
Nodes (13): StandingsSnapshot, CompetitionId, Dictionary, Given, Group, HttpClient, Name, Round (+5 more)

### Community 54 - ".LoadCurrentAsync"
Cohesion: 0.26
Nodes (10): CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task, EntryIndexProjection (+2 more)

### Community 55 - "FinaliseDecideTests"
Cohesion: 0.16
Nodes (7): DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, FinaliseDecideTests, OneTeamResult

### Community 56 - "ScoringTeamId"
Cohesion: 0.11
Nodes (18): ScoringTeamMembershipAssigned, Guid, IFormatProvider, ProtectionGroup, Id, Name, ProtectionGroupMembership, CompetitorRef (+10 more)

### Community 57 - "Scoring Service Build Plan"
Cohesion: 0.07
Nodes (26): Dependency Graph, Design Rules Every Agent Must Uphold, File Layout, Issue Tracking, Open Issues, Overview, Parallelism Summary, Scoring Service Build Plan (+18 more)

### Community 58 - "OpenFlight"
Cohesion: 0.27
Nodes (11): CancellationToken, IClock, IEventStore, Task, OpenFlightHandler, DateTimeOffset, Fact, FakeEventStore (+3 more)

### Community 59 - "FakeEventStore"
Cohesion: 0.09
Nodes (33): CancellationToken, IClock, IEventStore, Task, ConfigureTeamClassificationHandler, CancellationToken, IEventStore, ImmutableArray (+25 more)

### Community 60 - "Work items"
Cohesion: 0.08
Nodes (25): 1. `TaskRoundState.InProgress` stays unreachable — but `TaskRoundReopened` is added, 2. Finalisation is competition-scope only this thread, Before starting — done, Out of scope — deliberately, Plan — Task-round lifecycle: `TaskRoundCompleted` / `TaskRoundReopened` / `TaskRoundAnnulled` / `Finalised`, Risks, The governing principle: the system does not order score capture, Three decisions taken up front (+17 more)

### Community 61 - "MetricDefinition"
Cohesion: 0.05
Nodes (41): MeasuredKind, Flag, Number, PromotionKind, TopN, TopPercent, RoundingMode, Ceiling (+33 more)

### Community 62 - ".Exact"
Cohesion: 0.19
Nodes (15): CancellationToken, IClock, IEventStore, Task, BindParameterHandler, ParameterBound, AdoptedRules, DateTimeOffset (+7 more)

### Community 63 - "JasperFxEventStore"
Cohesion: 0.18
Nodes (11): CancellationToken, Exception, Guid, IDocumentReadOperations, IDocumentSessionFactory, IDocumentSessionOperations, IEventStoreOperations, IQueryEventStore (+3 more)

### Community 64 - "Plan — Catalogue-choice draws: the CD picks each round's task"
Cohesion: 0.08
Nodes (24): Acceptance, Appendix A — the deferred follow-on: per-round parameter bindings, Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Domain, Phase B — Application (+16 more)

### Community 65 - "ReflightRole"
Cohesion: 0.15
Nodes (11): ReflightRole, Entitled, Filler, Original, List, Result, DateTimeOffset, Dictionary (+3 more)

### Community 66 - "CompetitionReplaceTaskRoundPropertyTests"
Cohesion: 0.12
Nodes (15): EventKind, phaseCount, roundsPerPhase, targetPhase, targetTaskRound, taskRoundsPerRound, Gen, kind (+7 more)

### Community 67 - "Story — Normalisation lower clamp (floor NormalisedScore at 0)"
Cohesion: 0.11
Nodes (18): As built (2026-08-28) — WI-1..WI-4 landed, WI-5 fast loop green, Context map (keep the implementer's window small), D1 — The clamp is uniform: every arrangement with a `Normalisation`, both directions, D2 — Rulebook position (cited): the clamp implements "negative → zero" at the normalised grain, D3 — Placement and exact form, D4 — Deliberately out of scope (do not "fix" here), Decisions settled during planning (do not relitigate), Ground truth — witness cell and expected post-change outcomes (+10 more)

### Community 68 - "Design decisions — settled here, do not relitigate"
Cohesion: 0.09
Nodes (22): Before starting, D1 — The composition formula and its row condition, D2 — Which flights: all of the entry's, guarded to be equivalent to the selection, D3 — Term source: the resolved task, per round, D4 — Metrics construction: decode, plus the intrinsic — do not call Interpret, D5 — The classification split dissolves; the mirror survives, re-anchored, D6 — Transitional parity gate, then delete (the prior story's proven pattern), D7 — Nothing outside `tests/` + `kanban/` (+14 more)

### Community 69 - "GroupSpotsPropertyTests"
Cohesion: 0.06
Nodes (39): DrawOp, IReadOnlyCollection, OpKind, Ops, SpotBase, Op, DateTimeOffset, Fact (+31 more)

### Community 70 - "Work items"
Cohesion: 0.08
Nodes (23): Before starting — done, Decisions settled before planning (user, 2026-08-21), Findings from reading the tree, Out of scope — deliberately, Plan, Planner's calls — flag for veto when this plan is reviewed, Risks, Story — Reflights: `ReflightGroupAppended` (+15 more)

### Community 71 - "TeamsDecideTests"
Cohesion: 0.14
Nodes (7): Competitors, DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, TeamsDecideTests

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

### Community 76 - "Work items"
Cohesion: 0.06
Nodes (32): As built (2026-08-28), Before starting, D1 — Replay mechanics per fixture, D2 — Divergence citation register (new token N1), D3 — F5J class-definition authoring spec (comps 45, 135, 121), D4 — F3K class-definition authoring spec (comps 17, 54), D5 — ReplayDriver / ReplaySteps widening (WI-1, exhaustive; shared files), D6 — The G4 comparator-property step (new Then, shared file, WI-1) (+24 more)

### Community 77 - "CompetitionId"
Cohesion: 0.06
Nodes (76): ICommand, ICommandHandler, IHttpMaxRequestBodySizeFeature, WebApplication, Commands, IReadOnlyList, WebApplication, Composition (+68 more)

### Community 78 - "Person"
Cohesion: 0.12
Nodes (15): DateTimeOffset, Defect, Result, ClubAffiliation, ClubName, MembershipNumber, Person, Club (+7 more)

### Community 79 - ".BuildDrawnCompetition"
Cohesion: 0.19
Nodes (7): Competitors, DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, AppendReflightGroupDecideTests

### Community 80 - "TaskRoundCompleted"
Cohesion: 0.20
Nodes (7): TaskRoundCompleted, Fact, DateTimeOffset, Fact, InlineData, Theory, TaskRoundLifecycleDecideTests

### Community 81 - ".HandleAsync"
Cohesion: 0.24
Nodes (11): CancellationToken, IClock, IEventStore, Task, AmendMeasurementHandler, DateTimeOffset, Fact, FakeEventStore (+3 more)

### Community 82 - "Work items"
Cohesion: 0.09
Nodes (21): Before starting — done, Decisions settled before planning (user, 2026-08-24), Findings from reading the tree, Out of scope — deliberately, Plan, Planner's calls — flag for veto when this plan is reviewed, Property-based invariants (named now, per CLAUDE.md), Reflight-scoring rulings (+13 more)

### Community 83 - ".ComputeEntries"
Cohesion: 0.18
Nodes (9): ImmutableArray, ImmutableDictionary, PairwiseCoOccurrence, PairwiseCoOccurrenceEntry, Dictionary, Fact, Group, Round (+1 more)

### Community 84 - "ScoringServicePropertyTests"
Cohesion: 0.13
Nodes (15): Scope, InfractionType, SubjectIndex, Competitors, DateTimeOffset, Dictionary, Entries, Fact (+7 more)

### Community 85 - "Story — Model tie-break policy as class data"
Cohesion: 0.10
Nodes (19): Adoption checks 17–19 (the inventory grows by three), Decisions (pre-answered during flesh-out 2026-08-30; D1, D8, D10 and the, Engine design, Invariant T — the property, named here per CLAUDE.md (goes verbatim into, Known traps (pre-answered — do not reopen inside this story), Out of scope (restated for sign-off), Record (close-out 2026-08-30), Story invariant for sign-off (+11 more)

### Community 86 - ".Validate"
Cohesion: 0.23
Nodes (4): IReadOnlyList, Fact, ClassDefinitionValidationTests, ClassDefinitionFixtures

### Community 87 - "Core Principles"
Cohesion: 0.10
Nodes (20): Access is strictly via an REST based API, Append only immutable log as state storage (Event Sourced), Commands and Queries only, Core-owned invariants, Core Principles, CQRS pattern to cleanly seperate Reads from Writes, Domain Driven Design, Functional-Like as a Core Princple (+12 more)

### Community 88 - "00-general-rules.md"
Cohesion: 0.25
Nodes (4): CIAM General Rules — 2026 Edition (extracted source text), 2 F3F - RADIO CONTROL SLOPE SOARING GLIDERS, F3 Radio Control Soaring — 2025 Edition v2 (extracted source text), F5 Radio Control Electric Powered Motor Gliders — 2026 Edition 2 (extracted source text)

### Community 89 - "RC Soaring Competitions — Key Concepts"
Cohesion: 0.08
Nodes (24): Competition, Competition Class, Competitor, Contribution Eligibility, Draw, Entry, Finalisation, Flight (+16 more)

### Community 90 - "Plan — Command-side steel thread: Person end-to-end"
Cohesion: 0.10
Nodes (20): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Foundations, Phase B — The Application kernel, Phase C — Adapters, Plan — Command-side steel thread: Person end-to-end (+12 more)

### Community 91 - "HarnessSelfCheckSteps"
Cohesion: 0.17
Nodes (6): Given, IReadOnlyList, Task, Then, When, HarnessSelfCheckSteps

### Community 92 - ".DrawnCompetitionAsync"
Cohesion: 0.23
Nodes (11): CancellationToken, IClock, IEventStore, Task, AppendReflightGroupHandler, CancellationToken, Fact, List (+3 more)

### Community 93 - ".New"
Cohesion: 0.07
Nodes (36): Round, CompetitorRegistered, DrawAccepted, PhaseDrawn, Store, FakeEventStore, Store, Store (+28 more)

### Community 94 - ".AppendAsync"
Cohesion: 0.15
Nodes (18): CancellationToken, Guid, IReadOnlyList, Task, IEventStore, RecordedEvent, CancellationToken, Guid (+10 more)

### Community 95 - "Work items"
Cohesion: 0.11
Nodes (18): Before starting, Decisions settled during planning (2026-08-25), Execution plan — how an agent (or agents) runs this, Findings from reading the tree (verified 2026-08-25), Out of scope (deferrals restated, untouched), Plan, Story — Prescribed-draw import capability, What (+10 more)

### Community 96 - "DrawAcceptanceDecideTests"
Cohesion: 0.20
Nodes (7): CompetitorRef, DateTimeOffset, Fact, GroupRef, InlineData, Theory, DrawAcceptanceDecideTests

### Community 97 - "5.5.10 F5K – RC THERMAL DURATION GLIDERS FOR MULTIPLE TASK COMPETITION WITH"
Cohesion: 0.10
Nodes (20): 5.5.10.10 Number of Model Aircraft, 5.5.10.11 Launch and Landing area (Pilots Area), 5.5.10.12 Penalty overview, 5.5.10.13 Reflight, 5.5.10.14 Preparation time, 5.5.10.15 Scoring, 5.5.10.16 Final score, 5.5.10.17 Resolution of a tie (+12 more)

### Community 98 - "Work items"
Cohesion: 0.10
Nodes (19): Before starting, Decisions settled during planning (2026-08-24), Execution plan — how an agent (or agents) runs this, Findings from reading the tree (re-verified 2026-08-24), Out of scope (deferrals restated, untouched), Plan, Story — Accepting or rejecting the draw, and redrawing, What (+11 more)

### Community 99 - "Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor`"
Cohesion: 0.10
Nodes (19): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Domain, Phase B — Application, Phase C — Api and verification, Plan — The field: `RegisterCompetitor` and `WithdrawCompetitor` (+11 more)

### Community 100 - "test_triage.py"
Cohesion: 0.09
Nodes (30): assignments_of(), _convertible_record_sets(), _expected_assignment(), make_record(), composite, given, parametrize, settings (+22 more)

### Community 101 - "ReplayDriver"
Cohesion: 0.18
Nodes (13): Kept, ReflightRows, SlotCapture, ScoresRow, HttpClient, IEnumerable, IReadOnlyDictionary, IReadOnlyList (+5 more)

### Community 102 - "PersonRegistered"
Cohesion: 0.18
Nodes (13): DateTimeOffset, ClubAffiliationChanged, ContactDetailsChanged, PersonEvent, PersonRegistered, PersonRenamed, Fact, PersonEventJsonTests (+5 more)

### Community 103 - "F3F.1 GENERAL RULES"
Cohesion: 0.11
Nodes (19): F3F.1.10 Safety, F3F.1.11 Judging, F3F.1.12 Scoring, F3F.1.13 Classification, F3F.1.14 Team Classification, F3F.1.15 Organisation of the Contest, F3F.1.16 Changes, F3F.1.17 Weather Conditions and interruptions (+11 more)

### Community 104 - "IDispatcher"
Cohesion: 0.07
Nodes (43): CountLetters, Echo, CancellationToken, IServiceProvider, Task, Type, Dispatcher, ICommand (+35 more)

### Community 105 - "Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition`"
Cohesion: 0.11
Nodes (18): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — `Validate()` and ingestion limits, Phase B — `class_library` read model and the write path, Phase C — Api and end-to-end verification, Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition` (+10 more)

### Community 106 - "Design decisions (settled here, cited from code)"
Cohesion: 0.08
Nodes (23): Before starting — resolved at scoping (2026-08-30), Cross-references checked (housekeeping rule 2), D-A1 — Aggregate-scoped Zero* acts at the task-round stage, through the existing raw-stage engine path, D-A2 — Anchoring: the Zero* record must name the task-round it zeroes, D-A3 — A Zero* record with no `TaskRound` coordinate cannot be anchored: refused at record time, refused loudly at score time, D-A4 — Mixed-effect definitions act in both stages; that is the rule, not a double-count, D-B1 — `ApplyRawPenalties` surfaces a Disqualify flag; the raw stage's return type grows, D-B2 — The flag is flag-only: no score change, OR-accumulated through the walk (+15 more)

### Community 107 - "PublishClassDefinition"
Cohesion: 0.09
Nodes (34): Hash, CancellationToken, IClock, IEventStore, Task, PublishClassDefinition, PublishClassDefinitionHandler, CancellationToken (+26 more)

### Community 108 - "Story — Entry-scoped point-deduction penalties are inert"
Cohesion: 0.10
Nodes (20): Cross-references checked (housekeeping rule 2), D1 — Stage follows recorded scope; effect picks the action within the stage, D2 — Accrual and exclusion-group semantics at the raw stage are identical to the aggregate stage, D3 — Ordering within one entry's penalty set: contribution, suppression, then zeroing dominance, D4 — Floor: a deducted HigherIsBetter raw never goes below zero, D5 — Existing fixtures and seed classes are unaffected byte-for-byte, D6 — Read-side tolerance unchanged, Decision (argued, per "to be argued in-story") (+12 more)

### Community 109 - "RecordCompetitionPenaltyDecideTests"
Cohesion: 0.21
Nodes (6): DateTimeOffset, Fact, Gen, Penalty, PenaltyScope, RecordCompetitionPenaltyDecideTests

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

### Community 119 - "ScoringVocabulary.cs"
Cohesion: 0.07
Nodes (33): Comparator, EqualTo, GreaterOrEqual, GreaterThan, LessOrEqual, LessThan, TargetAssignment, AnyOrder (+25 more)

### Community 120 - "Plan — Create-competition steel thread: `CreateCompetition`"
Cohesion: 0.12
Nodes (16): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — `competitions` read model, Phase B — `CreateCompetition` write path, Phase C — Api and end-to-end verification, Plan — Create-competition steel thread: `CreateCompetition` (+8 more)

### Community 121 - "EndpointRouteBuilderExtensions"
Cohesion: 0.28
Nodes (4): IEndpointRouteBuilder, IResult, EndpointRouteBuilderExtensions, Func

### Community 122 - ".Normalise"
Cohesion: 0.13
Nodes (20): NormalisationDirection, HigherIsBetter, LowerIsBetter, Normalisation, Direction, Round, WinnerScore, Rounding (+12 more)

### Community 123 - "2. Findings"
Cohesion: 0.15
Nodes (12): 1. Why, 2.1 Competition catalogue (public, easy), 2.2 What `eScoringInterface.exe` actually is, 2.3 Server API (recovered by decompiling GliderScore.exe 6.79 U5), 2.4 Download zip contents, 2.5 Caveats learned the hard way, 2. Findings, 3. Fit with the existing fixture pipeline (+4 more)

### Community 124 - ".ScoreCompetition"
Cohesion: 0.19
Nodes (16): DateTimeOffset, ReflightRuling, At, By, CompetitorRef, Reason, Selection, TaskRound (+8 more)

### Community 125 - "RegisterCompetitorHandler"
Cohesion: 0.17
Nodes (16): CancellationToken, IClock, IEventStore, Task, RegisterCompetitorHandler, DateTimeOffset, Fact, FakeEventStore (+8 more)

### Community 126 - "Rule map — topic × class"
Cohesion: 0.12
Nodes (15): Contest shape, Contest shape, Cross-class NZ rules, Drop-worst, Flight points and landing bonus, Launch-height scoring (F5 only), Normalisation and rounding, NZ national classes (NZMAA Section 5: Soaring, March 2024) (+7 more)

### Community 127 - "SECTION A - CIAM INTERNAL REGULATIONS"
Cohesion: 0.06
Nodes (36): A.10.1 Requirements for proposals, A.10.2 Effective date of rule changes, A.10.3 Submission procedure, A.10 SUBMISSION OF PROPOSALS TO THE CIAM, A.11.1 Emergency safety rules, A.11.2 Emergency safety notices, A.11 EMERGENCY SAFETY RULES & NOTICES, A.12 AEROMODELLING FUND (+28 more)

### Community 128 - "Design decisions — settled here, do not relitigate"
Cohesion: 0.08
Nodes (23): As-built (2026-08-29), Before starting, D1 — Exact semantics of the exposed value, D2 — Placement: parallel map on `GroupResult`, not a second field on `TaskResult`, D3 — Population rules inside `NormalisationEngine.Normalise` (both branches), D4 — Fail-loud view mapping, D5 — API surface changes none, D6 — Harness grain-1 flips to HTTP where the authored class permits it (+15 more)

### Community 129 - "IDomainEvent"
Cohesion: 0.27
Nodes (10): IEventStore, IDomainEvent, CancellationToken, ExpectedVersion, Guid, IReadOnlyList, List, RecordedEvent (+2 more)

### Community 130 - "F3K.11 DEFINITIONS OF TASKS"
Cohesion: 0.13
Nodes (15): F3K.11.10 Task J (Three last flights), F3K.11.11 Task K (Increasing time by 30 seconds, “Big Ladder”), F3K.11.12 Task L (One flight), F3K.11.13 Fly-off Task M (Increasing time by 2 minutes “Huge Ladder”), F3K.11.14 Task N (Best flight), F3K.11.1 Task A (Last flight), F3K.11.2 Task B (Next to last and last flight), F3K.11.3 Task C (All up, last down) (+7 more)

### Community 131 - "IProjection"
Cohesion: 0.12
Nodes (20): IProjection, IDocumentOperations, IDocumentSession, ClassDefinitionSummaryProjection, FisherClassDefinitionSummaryProjection, MartenClassDefinitionSummaryProjection, CancellationToken, Guid (+12 more)

### Community 132 - "Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server"
Cohesion: 0.13
Nodes (14): Also done, not in the original plan, Before starting, Deliberately not done, One thing deliberately left short of the story's title, Outcome — as built, 2026-08-16, Plan, Property-based testing, Scope of this pass — Fisher/SQLite only (+6 more)

### Community 133 - "WithdrawCompetitorHandler"
Cohesion: 0.23
Nodes (11): CancellationToken, IClock, IEventStore, Task, WithdrawCompetitorHandler, DateTimeOffset, Fact, FakeEventStore (+3 more)

### Community 134 - ".LoadCurrentAsync"
Cohesion: 0.24
Nodes (11): IJasperFxProjection, CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task (+3 more)

### Community 135 - ".BuildDispatcher"
Cohesion: 0.32
Nodes (11): IClock, IEventStore, RenamePerson, RenamePersonHandler, GetPerson, Fact, FakeClock, FakeEventStore (+3 more)

### Community 136 - "1 GENERAL DEFINITIONS"
Cohesion: 0.14
Nodes (14): 1.1 DEFINITIONS, 1.2 CHARACTERISTICS, 1.3 RADIO CONTROL TRANSMITTER., 1.4.1 Unless otherwise specified in class rules, the competitor may use a maximum of two models, 1.4.2 The competitor must own the model(s) flown but is not required to have built them., 1.4.3 A model may be flown in a contest by only one competitor., 1.4 NUMBER OF MODELS, OWNERSHIP AND OPERATION., 1.5 BALLASTING (+6 more)

### Community 137 - ".AddSoarscoreInfrastructure"
Cohesion: 0.09
Nodes (19): IConfiguration, IServiceCollection, CancellationToken, DateOnly, IDocumentSessionFactory, IReadOnlyList, Task, DocumentCompetitionsQuery (+11 more)

### Community 138 - ".Classify"
Cohesion: 0.08
Nodes (28): Member, HashSet, ImmutableArray, Result, Candidate, Member, TeamClassificationEngine, TeamClassificationResult (+20 more)

### Community 139 - "PhaseDefinition"
Cohesion: 0.12
Nodes (17): ImmutableArray, PhaseDefinition, Drops, Ordinal, Promotion, Rounds, Tasks, TieBreaks (+9 more)

### Community 140 - "NzNdcSeedArithmeticTests"
Cohesion: 0.26
Nodes (5): Dictionary, Fact, InlineData, Theory, NzNdcSeedArithmeticTests

### Community 141 - "SeedF3K"
Cohesion: 0.10
Nodes (19): ImmutableArray, SeedF3K, Catalogue, Definition, FlightMetrics, TaskA, TaskB, TaskC (+11 more)

### Community 142 - "F3G.1 GENERAL RULES"
Cohesion: 0.08
Nodes (24): 3 F3G - RADIO CONTROLLED MULTI-TASK GLIDERS WITH ELECTRIC, F3G.1.10 Organisation of Contests, F3G.1.11 Safety Rules, F3G.1.12 Weather Conditions/Interruptions, F3G.1.1 Definition of a Radio-Controlled Glider with Electric Motor, F3G.1.2 Characteristics data of Radio-Controlled Gliders F3G, F3G.1.3 Technical equipment, F3G.1.4 General requirements (+16 more)

### Community 143 - "Gliderscore Jet DB — Schema Analysis and Indicative Domain Mapping"
Cohesion: 0.15
Nodes (12): 1. Table inventory, 2. Relationships, 3. Indicative mapping to the Soarscore domain, 4. Observations on `Scores`, 5. The structural lesson, 6. Concept gaps surfaced (require glossary approval — not silently added), Competition setup, Event-time records (+4 more)

### Community 144 - "Story — Gliderscore golden-fixture pipeline"
Cohesion: 0.15
Nodes (12): Before starting, Export format — resolved from source 2026-08-25, Ranking oracle — decided 2026-08-25 (hybrid), Sequencing, Story — Gliderscore golden-fixture pipeline, What, Why it matters, WI-1 — Extraction tool (+4 more)

### Community 145 - ".BuildDrawnCompetition"
Cohesion: 0.18
Nodes (13): ReflightSelection, BetterOf, NotPermitted, Replacement, UndefinedRequiresRuling, Competition, Competitors, DateTimeOffset (+5 more)

### Community 146 - "Pre-requisites (sub-agent dispatchable — gate WI-1–4)"
Cohesion: 0.12
Nodes (15): As built 2026-08-27, Before starting, PRE-1 — Per-comp export: comp 45, 2019 F5J Christchurch (`f5j-christchurch-2019`), PRE-2 — Per-comp export: comp 135, F5J Hawkes Bay and Team Trials (`f5j-hawkes-bay-trials`), PRE-3 — Per-comp export: comp 17, Southern Fling (`f3k-southern-fling`), PRE-4 — Per-comp export: comp 121, NZ South Island F5J (`f5j-nz-south-island`), PRE-5 — Per-comp export: comp 54, 2020 June F3K (`f3k-june-2020`), Pre-requisites (sub-agent dispatchable — gate WI-1–4) (+7 more)

### Community 147 - "ReplaySteps"
Cohesion: 0.21
Nodes (6): Table, Task, Then, When, ReplaySteps, Fixture

### Community 148 - "triage.py"
Cohesion: 0.19
Nodes (18): _assignment_sort_key(), check_draw_completeness(), _common_fields(), convert_records(), _decode_duration_slots(), _decode_f3k_slots(), _decode_f5k_flights(), _decode_passthrough() (+10 more)

### Community 149 - "EntryModelBasedFoldTests"
Cohesion: 0.27
Nodes (10): actual, Actual, Model, model, Gen, GenOperation, CompetitionModelBasedFoldTests, Gen (+2 more)

### Community 150 - "CLAUDE.md — Soarscore"
Cohesion: 0.17
Nodes (12): CLAUDE.md — Soarscore, Core architectural law: Competition Class model vs. core system, Domain in one screen, graphify, House-keeping rules, Key constraints, Pointers, Project status (+4 more)

### Community 151 - "fai-rule.sh"
Cohesion: 0.39
Nodes (9): cmd_check_links(), cmd_find(), cmd_show(), cmd_toc(), die(), norm_ref(), fai-rule.sh script, volume_file() (+1 more)

### Community 152 - "RankingEnginePropertyTests"
Cohesion: 0.24
Nodes (12): Cells, Disq, IList, Position, Entry, Fact, Gen, ImmutableArray (+4 more)

### Community 153 - "validate.py"
Cohesion: 0.28
Nodes (17): base_competition(), check_integrity(), check_rule_1(), check_rule_2(), check_rule_3(), check_rule_4(), check_rule_5(), composite_key() (+9 more)

### Community 154 - "TaskDefinition"
Cohesion: 0.06
Nodes (34): TaskDefinition, Code, Flights, FlightValidWhen, Group, Metrics, Name, Normalise (+26 more)

### Community 155 - "4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS"
Cohesion: 0.22
Nodes (9): 4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS, F3J.12 WEATHER CONDITIONS AND INTERRUPTIONS, F3J.3.1 Rounds and Attempts, F3J.3 CONTEST FLIGHTS, F3J.4 RE-FLIGHTS, F3J.5.1 Judging, F3J.5.2 Neutralisation of a flight group, F3J.5 CANCELLATION OF A FLIGHT AND/OR DISQUALIFICATION (+1 more)

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

### Community 161 - "PrescribeDrawPropertyTests"
Cohesion: 0.31
Nodes (7): Mutation, DateTimeOffset, Fact, IReadOnlyList, Rounds, PrescribeDrawPropertyTests, WithdrawnId

### Community 162 - "GliderScore fixture extraction"
Cohesion: 0.08
Nodes (24): Adding a fixture, Deterministic row order, Differential gate result, GliderScore fixture extraction, How the corpus is consumed, Index contract (rule 5), Limitations, NZ master caveat and opt-in tolerant mode (+16 more)

### Community 163 - "ReflightRule"
Cohesion: 0.15
Nodes (13): ReflightRule, EntitledScores, MinNewGroupSize, OthersScore, Competitors, DateTimeOffset, Dictionary, Entries (+5 more)

### Community 164 - "test_csvparse.py"
Cohesion: 0.10
Nodes (22): assert_record_typed_equal(), _corrupted_documents(), default_line(), document(), _download_records(), composite, given, parametrize (+14 more)

### Community 166 - "mine_catalogue.py"
Cohesion: 0.10
Nodes (31): build_range_postback(), collect_comps(), extract_form_fields(), fetch_catalogue(), find_range_select(), is_comp_id_value(), locate_comp_select(), main() (+23 more)

### Community 167 - "NZ Class M — ALES 200 (Altitude Limited Electric Soaring)"
Cohesion: 0.18
Nodes (11): 1. Pilot assignment to groups (the draw), 2. Launch (`NZ.3.12.1`), 3. Data the timer / helper collects, 4. The task (`NZ.3.12.1 f, g, m, n`), 5. Group score (`NZ.3.12.3`), 6. Round and final score, 7. Re-flights (`NZ.3.12.5 l`), 8. NDC format (`NZ.3.12.7`) — a different scoring pipeline (+3 more)

### Community 168 - "PenaltyEffect"
Cohesion: 0.33
Nodes (6): PenaltyEffect, DeductPoints, Disqualify, ZeroFlight, ZeroRound, ZeroTask

### Community 169 - "3.17 CLASS R: E-RES 2M (Electric Rudder Elevator Spoiler 2M Glider)"
Cohesion: 0.18
Nodes (11): 3.17.0 Contents:, 3.17.1 Introduction, 3.17.2 Model Specifications, 3.17.3 Competition Terrain, 3.17.4 Cancellation, 3.17.5 Competition Flights, 3.17.6 Launching, 3.17.7 Landing (+3 more)

### Community 170 - "BindParameterPropertyTests"
Cohesion: 0.28
Nodes (4): DateTimeOffset, Fact, Ref, BindParameterPropertyTests

### Community 171 - "DrawOp"
Cohesion: 0.40
Nodes (5): DrawOp, Accept, Register, Reject, Withdraw

### Community 172 - "Soarscore — Users"
Cohesion: 0.18
Nodes (10): 1. Organiser, 2. Contest Director, 3. Scorer, 5. Pilot / Competitor, Direct users, Indirect users, Multiple "Hats" Rule, Purpose (+2 more)

### Community 173 - "Drop-worst"
Cohesion: 0.18
Nodes (11): 1. Configuration source (Comps table), 2. `DropScoreOption` decode and gating, 3. Staged activation — how many drops at R rounds flown, 4. Selection basis (task-driven vs round-driven), 5. Tie-breaking among equal drop candidates — deterministic, 6. Marking and exclusion, 7. Re-flights, 8. F3K-gated variant (`f3kRecord`) (+3 more)

### Community 174 - "ClassDefinitionSummary"
Cohesion: 0.15
Nodes (17): DateTimeOffset, Guid, ClassDefinitionSummary, CancellationToken, IReadOnlyList, Task, FindClassDefinitions, FindClassDefinitionsHandler (+9 more)

### Community 175 - "DrawingACatalogueChoicePhaseSteps"
Cohesion: 0.20
Nodes (11): CompetitionId, HttpClient, HttpResponseMessage, List, ProblemDetails, Table, Task, Then (+3 more)

### Community 176 - "PersonSummary"
Cohesion: 0.19
Nodes (15): CancellationToken, IReadOnlyList, Task, FindPeople, FindPeopleHandler, CancellationToken, IReadOnlyList, Task (+7 more)

### Community 177 - "LADR-0002 — Competition Class definition: representation, ingestion and identity"
Cohesion: 0.20
Nodes (9): 1. Users POST definitions, 2. Authoring: C# records, not a fluent DSL, 3. No notation parser in the core, 4. Ingestion — one path, 5. Identity: content hash, not versions, 6. Transcribing `seed-data/*.class`, 7. Citations are not in the model — decided, rejected, Decision (+1 more)

### Community 178 - ".Select"
Cohesion: 0.17
Nodes (10): CountsFor, Role, Entry, IReadOnlyList, Score, ReflightSelector, Fact, InlineData (+2 more)

### Community 179 - "F5K — RC Electric Thermal Duration, Multiple-Task"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`5.5.10.15`), 4. Round score, 5. Final classification (`5.5.10.16–10.18`), 6. Re-flights (`5.5.10.13`), F5K — RC Electric Thermal Duration, Multiple-Task, Nominal Launch Height (NLH) and launch points (`5.5.10.3–10.4`) (+2 more)

### Community 180 - "RecordEntryPenaltyDecideTests"
Cohesion: 0.26
Nodes (6): Fact, Gen, ImmutableArray, Penalty, PenaltyScope, RecordEntryPenaltyDecideTests

### Community 181 - "F3J — RC Thermal Duration Gliders"
Cohesion: 0.22
Nodes (9): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`F3J.10.10–10.11`), 4. Round score, 5. Final classification (`F3J.3.1`, `F3J.11`), 6. Re-flights (`F3J.4`, `F3J.5.2`), F3J — RC Thermal Duration Gliders, Penalty schedule (+1 more)

### Community 182 - "C.16.2 Requirements for radio control"
Cohesion: 0.20
Nodes (10): C.16.1 General requirements, C.16.2.1 Flight area, C.16.2.2 Transmitter pound, C.16.2.3 Spread spectrum transmitters, C.16.2.4 AM/FM transmitters, C.16.2.5 Detection of radio interference, C.16.2.6 Starting order, C.16.2.7 Other requirements (+2 more)

### Community 183 - "C.2.1 First category events"
Cohesion: 0.20
Nodes (10): C.2.1.1 World Championships, C.2.1.2 Continental Championships, C.2.1.3 World Air Games and World Games, C.2.1 First category events, C.2.2.1 Open International, C.2.2.2 International Series, C.2.2.3 World Cup, C.2.2 Second category events (+2 more)

### Community 184 - "5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS"
Cohesion: 0.06
Nodes (31): 5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS, F3K.10.1 Final score, F3K.10.2 Resolution of a tie, F3K.10.3 Fly-off, F3K.10.4 Team Classification, F3K.10 SCORING, F3K.1.1 Timekeepers, F3K.1.2 Helper (+23 more)

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

### Community 189 - "test_fetch_comp.py"
Cohesion: 0.19
Nodes (34): boolean_token_csv_bytes(), check_urls(), csv_member_name(), duration_csv_bytes(), fetch(), fixture_csv_bytes(), make_client(), make_zip_bytes() (+26 more)

### Community 190 - "FinaliseValidityPropertyTests"
Cohesion: 0.14
Nodes (13): minRounds, minTasks, outcomes, RoundOutcome, taskRefs, DateTimeOffset, Gen, rounds (+5 more)

### Community 191 - "Soarscore.Infrastructure.Tests.csproj"
Cohesion: 0.20
Nodes (9): $(SoarscoreTargetFramework), AwesomeAssertions, Fisher, Marten, Microsoft.NET.Test.Sdk, Testcontainers.PostgreSql, xunit.runner.visualstudio, xunit.v3 (+1 more)

### Community 192 - "non-functional-requirements.md"
Cohesion: 0.20
Nodes (6): NFR-1 — One centralised, flexible competition class model, NFR-2 — Additive-only extensibility for new competition types, NFR-3 — Core System Only, NFR-4 — No imposed ordering on score capture, Scope amendment (2026-09-02, owner-approved) — teams in MVP software scope, Soarscore — Non-Functional Requirements

### Community 193 - "Competition rules for RC soaring"
Cohesion: 0.22
Nodes (8): Auditing a change for compliance, Competition rules for RC soaring, Invariants — **FAI classes only**, Never `Read` a file in `source-docs/`, Retrieval ladder — stop at the first rung that answers the question, Rules → architecture, Rules for working with this corpus, The corpus

### Community 194 - "Competition Rules — Generally Applicable (all contest types)"
Cohesion: 0.22
Nodes (9): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (normalisation), 4. Round score, 5. Final classification (common), 6. Penalties (common), 7. Re-flights (common pattern), Competition Rules — Generally Applicable (all contest types) (+1 more)

### Community 195 - "GsClient"
Cohesion: 0.23
Nodes (3): GsClient, Read-only, rate-limited, auditable client for gliderscore.com., ValueError

### Community 196 - "ScoreTerm"
Cohesion: 0.12
Nodes (22): FlightSelection, ScoreTerm, ImmutableArray, IReadOnlyDictionary, FlightSelector, IReadOnlyDictionary, FlightResult, FlightResultState (+14 more)

### Community 197 - "C.13 REQUIREMENTS FOR ORGANISATION OF INTERNATIONAL EVENTS"
Cohesion: 0.22
Nodes (9): C.13.1 Organisation, C.13.2 Local rules, C.13.3 Number of entries, C.13.4 Entry forms, C.13.5 Junior classification in an Open International, C.13.6 Female classification in an Open International, C.13.7 Results of international events, C.13.8 Fuel (+1 more)

### Community 198 - "C.21 CIAM TROPHIES"
Cohesion: 0.22
Nodes (9): C.21.1 Registration of CIAM trophies, C.21.2 Acceptance of CIAM trophies, C.21.3 Award of CIAM trophies, C.21.4 CIAM trophies report forms, C.21.5 Championship trophies, C.21.6 World Cup trophies, C.21.7 Responsibilities of the holder of a CIAM trophy, C.21.8 Loss of a CIAM trophy (+1 more)

### Community 199 - "C.5.1 Competitor"
Cohesion: 0.22
Nodes (9): C.5.1.1 Age of participants for Junior World or Continental Championships, C.5.1.2 Builder of the model, C.5.1.3 Competitor's proxy and substitution of team members, C.5.1.4 Anti-Doping Policy for Competitors, C.5.1 Competitor, C.5.2 Team manager, C.5.3 National team for World and Continental Championships, C.5.4 Competitor Invitation Procedure Phases (+1 more)

### Community 200 - "CompetitionSummary"
Cohesion: 0.19
Nodes (17): DateOnly, CompetitionSummary, CancellationToken, DateOnly, IReadOnlyList, Task, FindCompetitions, FindCompetitionsHandler (+9 more)

### Community 201 - "5.5.3 CLASS F5A – RC ELECTRIC POWERED GPS MOTOR GLIDERS (PROVISIONAL RULE)"
Cohesion: 0.22
Nodes (9): 5.5.3.1 Definition, 5.5.3.2 Energy Management, 5.5.3.3 Course Layout, 5.5.3.4 Launching, 5.5.3.5 Distance Task, 5.5.3.6 Landing Task, 5.5.3.7 Contest organisation, 5.5.3.8 Scoring (+1 more)

### Community 202 - "5.5.4 CLASS F5B – RC ELECTRIC POWERED MULTI TASK GLIDERS"
Cohesion: 0.22
Nodes (9): 5.5.4.1 Definition, 5.5.4.2 Course Layout and Organisation, 5.5.4.3 F5B Contest Site Layout, 5.5.4.4 Scoring, 5.5.4.5 Launching, 5.5.4.6 Distance Task, 5.5.4.7 Duration and Landing Task, 5.5.4.8 Site (+1 more)

### Community 203 - "ClassDefinitionValidationPropertyTests"
Cohesion: 0.28
Nodes (6): ExpectedCode, Definition, Fact, Func, Gen, ClassDefinitionValidationPropertyTests

### Community 204 - "csvparse.py"
Cohesion: 0.16
Nodes (17): _convert(), CsvParseError, DownloadRecord, parse_csv(), parse_field(), parse_line(), A download CSV line/document violates the wire contract., One wire row with every field strictly typed (order = wire order). (+9 more)

### Community 205 - ".TwoRoundCompetitionAsync"
Cohesion: 0.15
Nodes (14): IClassFixture, Round2GroupRef, ClassDefinitionEventStoreTests, PostgresClassDefinitionEventStoreTests, SqliteClassDefinitionEventStoreTests, CancellationToken, Entry, Fact (+6 more)

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
Cohesion: 0.18
Nodes (10): Annulments and penalties, Competition class model, Decisions that have since been taken up, Deferred decisions, Draw, Event store, GliderScore replay harness, Score capture and corrections (+2 more)

### Community 210 - ".LoadAsync"
Cohesion: 0.29
Nodes (6): CancellationToken, Entry, IEventStore, Task, Version, EntryLoader

### Community 211 - "Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC"
Cohesion: 0.20
Nodes (9): Before starting / cross-references (house rule 2), Completion note (2026-09-04), Interpretations made (no ruling requested; Pete may veto any), Plan, Related finding (out of scope here, filed in tech-debt), Rulebook defects found (left as written; NZMAA's to fix), Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC, What (+1 more)

### Community 212 - "ScoringCorpusPropertyTests"
Cohesion: 0.25
Nodes (7): DateTimeOffset, Fact, ImmutableArray, ImmutableDictionary, Name, Value, ScoringCorpusPropertyTests

### Community 213 - "Compliance check"
Cohesion: 0.25
Nodes (7): 1. Scope the check, 2. Pull the rules, 3. Verify numbers against source, 4. Check it against the architectural law, 5. Check it against the rest of the corpus, 6. Report, Compliance check

### Community 214 - ".ApplyAggregatePenalties"
Cohesion: 0.30
Nodes (8): AccruedInfo, Dictionary, HashSet, ImmutableArray, AccruedInfo, HasDisqualify, TotalDeduction, PenaltyEngine

### Community 215 - "test_mine_catalogue.py"
Cohesion: 0.15
Nodes (17): build_page(), fake_sleep(), FakeClock, FakeTransport, make_harness(), option(), given, settings (+9 more)

### Community 216 - "RC Soaring Competitions — Domain Class Diagram"
Cohesion: 0.25
Nodes (6): 1. The competition spine, 2. Competition Class — structure, 3. Competition Class — the scoring vocabulary, 4. Scoring, Modelling notes, RC Soaring Competitions — Domain Class Diagram

### Community 217 - "GroupId"
Cohesion: 0.06
Nodes (24): IParsable, CancellationToken, Task, CancellationToken, Task, CancellationToken, IReadOnlyList, Task (+16 more)

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

### Community 223 - ".Seed"
Cohesion: 0.17
Nodes (10): Id, Competition, DateTimeOffset, Dictionary, Entries, Entry, Fact, FakeEventStore (+2 more)

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

### Community 231 - "DrawProtectionPropertyTests"
Cohesion: 0.15
Nodes (16): BigSecond, MaxPairwise, MaxRoundViolations, PairCount, ProtectedPair, DateTimeOffset, Dictionary, Field (+8 more)

### Community 232 - "Soarscore.Application.Tests.csproj"
Cohesion: 0.25
Nodes (7): $(SoarscoreTargetFramework), AwesomeAssertions, CsCheck, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 233 - "ResultTests"
Cohesion: 0.39
Nodes (3): Fact, InvalidOperationException, ResultTests

### Community 234 - "SeeingWhatIsRecordedSteps"
Cohesion: 0.20
Nodes (7): CompetitionId, Given, HttpClient, List, Task, SeeingWhatIsRecordedSteps, Client

### Community 235 - "Soarscore.Domain.Tests.csproj"
Cohesion: 0.25
Nodes (7): $(SoarscoreTargetFramework), AwesomeAssertions, CsCheck, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk

### Community 236 - "RC Soaring — Aggregate Boundaries"
Cohesion: 0.29
Nodes (7): 1. CompetitionClass — the rulebook library, 2. Person — a registered person, 3. Competition — the event structure, field and schedule, 4. Entry — the live flying record, RC Soaring — Aggregate Boundaries, Scoring is cross-aggregate (not a root), Why there are four roots, not three

### Community 237 - "f3k-june-2020/ladder.py"
Cohesion: 0.52
Nodes (6): check(), fail(), load(), main(), GladerScore GlobalFunctions_MOD.vb:3116-3134 - Int(Nbr*Scale + 0.5)/Scale., round_number()

### Community 238 - "TeamClassificationEngineTests"
Cohesion: 0.33
Nodes (6): TeamClassificationConfiguration, Enabled, Method, Fact, Result, TeamClassificationEngineTests

### Community 239 - "2.4 LANDING"
Cohesion: 0.29
Nodes (7): 2.4.1 An in-flight sailplane has right of way over a launching sailplane., 2.4.2 In contests requiring precision (spot) landings, the pilot and timekeeper must stand upwind, 2.4.3 Models are to be scored and retrieved by the pilot / timekeeper with haste and caution so, 2.4.4 Precision Landings for Gliding events, 2.4.5 Precision Landings for Electric Events, 2.4.6 The Flight is cancelled and recorded as a zero score if during landing, the nose of the model, 2.4 LANDING

### Community 240 - "3.3 CLASS C: PREMIER THERMAL DURATION."
Cohesion: 0.29
Nodes (7): 3.3.1 Launching: The launch of the model may be by one of the following means:, 3.3.2 Organisation of Starts, 3.3.3 Scoring, 3.3.4 Definition of an Attempt and Official Flight., 3.3.5 Number of Rounds., 3.3.6 Partial Scores, 3.3 CLASS C: PREMIER THERMAL DURATION.

### Community 241 - "f5j-christchurch-2019/ladder.py"
Cohesion: 0.38
Nodes (6): get_time_in_seconds(), load(), main(), GlobalFunctions_MOD.vb:3116-3134 RoundNumber, VB Int floors toward -inf., Scoring_MOD.vb:626-631 GetTimeInSeconds, Fix() truncates toward zero., round_number()

### Community 242 - "C.20 COMPLAINTS AND PROTESTS"
Cohesion: 0.29
Nodes (7): C.20.1.1 Complaints prior to an event, C.20.1.2 Complaints during an event, C.20.1 Complaints, C.20.2 Protests, C.20.3 Time limit for lodging protests, C.20.4 Appeals, C.20 COMPLAINTS AND PROTESTS

### Community 243 - "TeamClassificationPropertyTests"
Cohesion: 0.15
Nodes (17): Memberships, Random, Row, Scenario, Teams, Fact, Gen, IEnumerable (+9 more)

### Community 244 - ".DrawnCompetitionAsync"
Cohesion: 0.23
Nodes (12): CancellationToken, IClock, IEventStore, Task, RecordReflightRulingHandler, CancellationToken, Competition, Fact (+4 more)

### Community 245 - "5.5.2 CONTEST RULES"
Cohesion: 0.29
Nodes (7): 5.5.2.1 Definition of an Official Flight, 5.5.2.2 Cancelling of a Flight and Disqualification, 5.5.2.3 Organisation of the Contest, 5.5.2.4 Organisation of Starts, 5.5.2.5 Processing of Energy Limiters, 5.5.2.6 Judging, 5.5.2 CONTEST RULES

### Community 246 - "Model"
Cohesion: 0.25
Nodes (8): CompetitorModel, List, Model, Competitors, FinalisationCount, ParameterBindingCount, PenaltyCount, RulesAmendmentCount

### Community 247 - "Normalisation"
Cohesion: 0.29
Nodes (7): Decision matrix — `GroupScoreOption` × task family × `varFltDednIdx`, Names and configuration plumbing, Normalisation, Re-run paths (who recomputes normalised scores), Shape of `Update_GroupScores` (`Scoring_MOD.vb:247-486`), Unresolved, Validation gate — sample comp reproduced from the matrix

### Community 248 - "Soarscore.Application.csproj"
Cohesion: 0.29
Nodes (5): Microsoft.AspNetCore.OpenApi, Microsoft.NET.Sdk.Web, $(SoarscoreTargetFramework), $(SoarscoreTargetFramework), Microsoft.NET.Sdk

### Community 249 - "Soarscore.Infrastructure.csproj"
Cohesion: 0.29
Nodes (6): Microsoft.Data.Sqlite, Npgsql, $(SoarscoreTargetFramework), Fisher, Marten, Microsoft.NET.Sdk

### Community 250 - "NZMAA Flying Rules, Section 5: Soaring — March 2024 (extracted source text)"
Cohesion: 0.29
Nodes (7): 0.0 NDC Rules for FAI Events, 0.1 F3F - RC SLOPE SOARING GLIDERS, 0.2.1 FAI F3K NDC Tasks:, 0.2 F3K - RC HAND LAUNCH GLIDERS, 0.3 F5J - RC ELECTRIC POWERED THERMAL DURATION GLIDERS, 0.4 F5K - RC ELECTRIC POWERED HAND LAUNCH GLIDERS, NZMAA Flying Rules, Section 5: Soaring — March 2024 (extracted source text)

### Community 251 - "SeedF5kNdc"
Cohesion: 0.14
Nodes (11): ImmutableArray, SeedF5kNdc, Catalogue, Definition, FlightMetrics, FlightValidWhen, LaunchAdjustment, TaskA (+3 more)

### Community 252 - "ScoringServiceZeroRoutingTests"
Cohesion: 0.15
Nodes (12): Other, Penalty, Result, TaskRoundCoordinate, Subject, DateTimeOffset, Entries, Fact (+4 more)

### Community 253 - "Story — Permitted scopes on PenaltyDefinition (adoption-time scope hardening)"
Cohesion: 0.11
Nodes (17): Before starting, D-1 — Field shape: `PenaltyScope[]?`, exactly as approved, D-2 — Check placement and precedence: scope refusal outranks payload completeness, D-3 — Adoption check 20 rejects only the empty list; no effect×scope cross-check, D-4 — Read path untouched; engine, views, handlers: zero edits, D-5 — Seeds and fixtures untouched, Design decisions (settled here, cited from code), Out of scope (+9 more)

### Community 254 - "Competitor"
Cohesion: 0.10
Nodes (20): closure, ClosureKind, Competitor, CompetitorNumber, Id, PersonRef, RegisteredAt, WithdrawnAt (+12 more)

### Community 255 - "AssigningSpotsSteps"
Cohesion: 0.16
Nodes (11): HttpClient, HttpResponseMessage, IReadOnlyList, Task, AssigningSpotsSteps, Client, CompetitionId, IReadOnlyList (+3 more)

### Community 256 - "Story — Ranking's secondary key: RawScore tie-break"
Cohesion: 0.13
Nodes (14): As built (2026-08-29), Decisions (settled during planning 2026-08-28; D1 gets Pete's sign-off at WI-0), Engine design (the entire `src/` change), Known traps (pre-answered — do not reopen inside this story), Out of scope (restated for sign-off), Story invariant for sign-off, Story — Ranking's secondary key: RawScore tie-break, What (+6 more)

### Community 257 - "TeamsEventJsonTests"
Cohesion: 0.38
Nodes (3): DateTimeOffset, Fact, TeamsEventJsonTests

### Community 258 - "Story — GliderScore webmine tool (read-only online comp acquisition)"
Cohesion: 0.20
Nodes (9): As built (2026-08-27), Before starting, Confidentiality, Open questions carried forward, Plan, Story — GliderScore webmine tool (read-only online comp acquisition), Validation of the mining approach (source cross-reference, 2026-08-26), What (+1 more)

### Community 259 - ".ScoreGroup"
Cohesion: 0.28
Nodes (6): GroupResult, ImmutableArray, ImmutableDictionary, IReadOnlyDictionary, EmittedCell, ScoringService

### Community 260 - "PersonId"
Cohesion: 0.14
Nodes (14): CancellationToken, IClock, IEventStore, Task, RegisterPerson, RegisterPersonHandler, Guid, IFormatProvider (+6 more)

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

### Community 265 - "fetch_comp.py"
Cohesion: 0.31
Nodes (9): _emitter(), _excerpt(), fetch_competition(), main(), _perform_fetch(), _ProtocolAbort, Exception, Run the four-step sequence plus WI-4 conversion for one CompID. Returns the… (+1 more)

### Community 266 - "C.19.1 Penalties imposed by the Contest Director"
Cohesion: 0.33
Nodes (6): C.19.1.1 Range of penalties imposed by the Contest Director with the consent of the FAI Jury, C.19.1.2 Information and publication, C.19.1 Penalties imposed by the Contest Director, C.19.2.1 Types of penalties imposed by CIAM Bureau, C.19.2 Penalties imposed by CIAM Bureau, C.19 PENALTIES

### Community 267 - "C.7 CONTEST OFFICIALS"
Cohesion: 0.33
Nodes (6): C.7.1 FAI Jury, C.7.2 FAI Jury at World and Continental Championships & WAG, C.7.3 FAI Jury at Open International, C.7.4 World Cup Board, C.7.5 Contest officials, C.7 CONTEST OFFICIALS

### Community 268 - ".BuildDrawnCompetition"
Cohesion: 0.19
Nodes (9): Competitors, DateTimeOffset, Fact, IEnumerable, ImmutableArray, InlineData, IReadOnlyList, Theory (+1 more)

### Community 269 - "People/TestDoubles.cs"
Cohesion: 0.14
Nodes (10): Soarscore.Application.Tests.Shared.People, Soarscore.Application.Tests.Queries.People, Soarscore.Application.Tests.Commands.People, IClock, DateTimeOffset, FakeClock, UtcNow, DateTimeOffset (+2 more)

### Community 270 - "Story — Coverage: normalisation is per group, not per round"
Cohesion: 0.33
Nodes (5): As built — notes, Deferred, Story — Coverage: normalisation is per group, not per round, What, Why it mattered

### Community 271 - "Stop storing `Entry.WorkingTime`"
Cohesion: 0.33
Nodes (6): Before starting, Decisions — user-confirmed 2026-08-21, Stop storing `Entry.WorkingTime`, What, What a removal must not break, Why it matters

### Community 272 - "FakeEventStore"
Cohesion: 0.31
Nodes (8): CancellationToken, Guid, IReadOnlyDictionary, IReadOnlyList, List, Task, FakeEventStore, Streams

### Community 273 - ".Apply"
Cohesion: 0.33
Nodes (4): PeopleProjection, ArgumentException, Fact, PeopleProjectionTests

### Community 274 - ".SetUpAsync"
Cohesion: 0.28
Nodes (11): CancellationToken, IEventStore, Task, GetTaskRoundRecording, GetTaskRoundRecordingHandler, TaskRoundRecordingView, Fact, Group (+3 more)

### Community 275 - "OpKind"
Cohesion: 0.17
Nodes (12): OpKind, AnnulRound, EmptyList, InvalidSpot, MissingMember, RepeatedCompetitor, RepeatedSpot, UnknownCompetitor (+4 more)

### Community 276 - "AdoptedRules"
Cohesion: 0.10
Nodes (13): CompetitionProjection, AdoptedRules, AdoptedAt, Definition, SourceClassId, SourceVersion, AdoptedRules, Fact (+5 more)

### Community 277 - "Work items"
Cohesion: 0.10
Nodes (19): Decisions settled during planning (2026-08-31, owner-confirmed), Execution plan — how an agent with sub-agents runs this, Findings from reading the tree (verified 2026-08-31), Out of scope (deferrals restated, untouched), Plan, Rules check (fai-rules, 2026-08-31), Story — Lane/spot assignment for drawn groups, What (+11 more)

### Community 278 - "3.2 CLASS B: 10 MINUTE THERMAL DURATION"
Cohesion: 0.40
Nodes (5): 3.2.1 Launching: The launch of the model may be by one of the following means:, 3.2.2 Scoring, 3.2.3 Number of Flights, 3.2.4 National Decentralised Contest (NDC), 3.2 CLASS B: 10 MINUTE THERMAL DURATION

### Community 279 - "3.9 CLASS J: THERMAL 2,4,6,8,10"
Cohesion: 0.40
Nodes (5): 3.9.1 Launching, 3.9.2 Scoring, 3.9.3 Contest time, 3.9.4 NDC Competition, 3.9 CLASS J: THERMAL 2,4,6,8,10

### Community 280 - "F3B.1 GENERAL RULES"
Cohesion: 0.08
Nodes (24): 1 F3B – RADIO CONTROL MULTI-TASK GLIDERS, F3B.1.10 Safety Rules, F3B.1.11 Weather Conditions / Interruptions, F3B.1.1 Definition of a Radio-Controlled Glider, F3B.1.2 Prefabrication of F3B Model Aircraft, F3B.1.3 Characteristics of Radio-Controlled Gliders F3B, F3B.1.4 Competitors and Helpers, F3B.1.5 Definition of an Attempt (+16 more)

### Community 281 - "reflight-aggregate-destination.md"
Cohesion: 0.09
Nodes (22): As built (2026-08-28), Before starting — all settled, Decisions settled during planning (owner, 2026-08-28 — do not relitigate), Dispatch model, Doc amendments (approved 2026-08-28 — apply verbatim in WI-5), Execution plan, Known traps (pre-answered), Out of scope — deliberately (+14 more)

### Community 282 - ".BuildGroups"
Cohesion: 0.31
Nodes (7): Remaining, Dictionary, HashSet, IEnumerable, ImmutableArray, PhaseDraw, Violations

### Community 283 - "webmine/ — GliderScore online competition acquisition (read-only)"
Cohesion: 0.25
Nodes (7): Etiquette and volumes, Layout, Permission state, Pipeline position, Usage, webmine/ — GliderScore online competition acquisition (read-only), Wire-format facts worth remembering (cited, not re-derived)

### Community 284 - "DrawPhase"
Cohesion: 0.13
Nodes (23): CancellationToken, IClock, IEventStore, Task, DrawPhaseHandler, AdoptedRules, DateTimeOffset, Fact (+15 more)

### Community 285 - "SeedNzF3kNdc"
Cohesion: 0.22
Nodes (8): ImmutableArray, SeedNzF3kNdc, Definition, FlightMetrics, TaskB, TaskD, TaskG, TaskH

### Community 286 - ".PostCommandRawAsync"
Cohesion: 0.33
Nodes (6): HttpClient, HttpResponseMessage, JsonSerializerOptions, Task, ApiClient, Options

### Community 287 - ".Rank"
Cohesion: 0.17
Nodes (16): AdditionalFullRound, BestDroppedScore, ClassificationRounds, EqualPlaces, QualifyingPosition, SourcePhaseOrdinal, TieBreakDirective, TieBreakFlyoff (+8 more)

### Community 288 - "Story — Resolve GliderScore scoring arithmetic from source"
Cohesion: 0.40
Nodes (4): Before starting, Story — Resolve GliderScore scoring arithmetic from source, What, Why it matters

### Community 289 - "Findings"
Cohesion: 0.40
Nodes (5): Divergences from FAI/NZ rules, Findings, Formula narrative (consolidated), Handoff notes, Reconciliation result

### Community 290 - "RecordCompetitionPenalty"
Cohesion: 0.20
Nodes (15): CancellationToken, IEventStore, Task, RecordCompetitionPenalty, RecordCompetitionPenaltyHandler, CancellationToken, DateTimeOffset, Fact (+7 more)

### Community 291 - ".Apply"
Cohesion: 0.40
Nodes (4): EntryProjection, DateTimeOffset, Fact, EntryProjectionTests

### Community 292 - "Story — NZ F3K NDC seed class"
Cohesion: 0.22
Nodes (8): Before starting (residual items for the builder), Build plan (WI-1 .. WI-4), Completion note (2026-08-30), Rulebook findings (verified against the corpus this session), Rulings (2026-08-30 — Pete; these supersede the story's earlier, Story — NZ F3K NDC seed class, What, Why it matters

### Community 293 - "CompetitorId"
Cohesion: 0.25
Nodes (9): CompetitorId, Dictionary, Fact, Field, Gen, IEnumerable, ImmutableArray, MinPerGroup (+1 more)

### Community 294 - "f5j-nz-south-island/ladder.py"
Cohesion: 0.27
Nodes (11): build_notes(), decode_packed_mmss(), frac(), half_up(), height_penalty(), load(), main(), problems() (+3 more)

### Community 295 - "extract-mssql.py"
Cohesion: 0.06
Nodes (54): Decimal, encode(), extract_table(), _install_tolerant_parser_patch(), load_recovered_texts(), main(), merge_recovered_texts(), apply_redaction() (+46 more)

### Community 296 - "GliderScore fixture corpus index"
Cohesion: 0.40
Nodes (4): Competitions, Diversity wanted, GliderScore fixture corpus index, Standing skip reasons

### Community 297 - "PenaltyDefinition"
Cohesion: 0.08
Nodes (22): PenaltyScope, PenaltyDefinition, Accrual, Effects, ExclusionGroups, PermittedScopes, PenaltyAccrual, OncePerAttempt (+14 more)

### Community 298 - "Refined plan"
Cohesion: 0.14
Nodes (13): Before starting, Handoff notes (read this before any sub-agent task), Hunt log (WI-2A — 2026-09-03), Move 1 — f3j-international GS team-ladder oracle + comparison, Move 2 — Grow the corpus with team-bearing comps, Refined plan, Story — Team-parity fixtures: validate team results against GliderScore, What (+5 more)

### Community 299 - "Seed classes — the authoring source"
Cohesion: 0.40
Nodes (4): How the notation maps, Running it, Seed classes — the authoring source, Status of the transcription

### Community 300 - "NumberOrParam"
Cohesion: 0.06
Nodes (39): Bands, Builder, Rows, CapScope, PerFlight, PerTask, NumberOrParam, Band (+31 more)

### Community 301 - "RecordEntryPenalty"
Cohesion: 0.20
Nodes (15): CancellationToken, IEventStore, Task, RecordEntryPenalty, RecordEntryPenaltyHandler, CancellationToken, DateTimeOffset, Fact (+7 more)

### Community 302 - "C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY"
Cohesion: 0.50
Nodes (4): C.10.1 Class F - Model aircraft, C.10.2 Class S - Space models, C.10.3 General requirements, C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY

### Community 303 - "C.15.6 Classification"
Cohesion: 0.50
Nodes (4): C.15.6.1 Individual classification, C.15.6.2 National team classification, C.15.6.3 Overall classification in multiple contest categories, C.15.6 Classification

### Community 304 - ".Aggregate"
Cohesion: 0.32
Nodes (9): aggregate, dropped, ImmutableArray, IReadOnlyDictionary, List, PhaseAggregator, PhaseScores, BestDroppedAggregate (+1 more)

### Community 305 - "AnnulEntryDecideTests"
Cohesion: 0.35
Nodes (5): DateTimeOffset, Fact, Gen, ImmutableArray, AnnulEntryDecideTests

### Community 306 - "F3J.10 SCORING"
Cohesion: 0.17
Nodes (12): F3J.10.10 Group Winner, F3J.10.11 Corrected Score, F3J.10.1 Flight Timing, F3J.10.2 Flight Time Recording, F3J.10.3 Overflying of the Working Time, F3J.10.4 Long Overflying, F3J.10.5 Landing Evaluation, F3J.10.6 Landing distance Measuring (+4 more)

### Community 307 - ".SeedDrawnCompetitionAsync"
Cohesion: 0.24
Nodes (9): GroupRef, DateTimeOffset, Fact, FakeEventStore, ImmutableArray, Members, Store, Task (+1 more)

### Community 308 - "5.5.11.1 General Rules"
Cohesion: 0.50
Nodes (4): 5.5.11.1.1 Definition of a Radio Controlled Glider with Electric Motor, 5.5.11.1.2 Prefabrication of the Model Aircraft, 5.5.11.1.3 Characteristics of Radio Controlled Gliders with electric motor and altimeter/motor run, 5.5.11.1 General Rules

### Community 309 - "opencode.json"
Cohesion: 0.50
Nodes (3): plugin, $schema, .opencode/plugins/graphify.js

### Community 310 - ".SeedCompetitionWithProtectedPair"
Cohesion: 0.12
Nodes (23): IQueryHandler, CancellationToken, Competition, IEventStore, ImmutableArray, Task, DrawProtectionDiagnosticsView, DrawProtectionViolationView (+15 more)

### Community 311 - "PostgresFixture"
Cohesion: 0.04
Nodes (51): IAsyncLifetime, PostgresCatalogueDrawEventStoreTests, SqliteCatalogueDrawEventStoreTests, PostgresCompetitionEventStoreTests, SqliteCompetitionEventStoreTests, PostgresDrawAcceptanceEventStoreTests, SqliteDrawAcceptanceEventStoreTests, SqliteDrawPhaseEventStoreTests (+43 more)

### Community 312 - "GliderScore golden comparison — state after http-grain-one-metric-bridge"
Cohesion: 0.29
Nodes (6): Confidence, Corpus, GliderScore golden comparison — state after http-grain-one-metric-bridge, Harness shape, Ledgers, What could improve

### Community 313 - "FixtureLoader"
Cohesion: 0.29
Nodes (4): Given, IReadOnlyList, JsonSerializerOptions, FixtureLoader

### Community 317 - "F3 Soaring — Generally Applicable Rules"
Cohesion: 0.29
Nodes (7): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score, 4. Round & final score, 5. Re-flights, F3 Soaring — Generally Applicable Rules, Source references

### Community 318 - "F3K — RC Hand-Launch Gliders"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`F3K.9.1`), 4. Round score, 5. Final classification (`F3K.10`), 6. Re-flights (`F3K.9.6`, `F3K.4.2`, `F3K.2.4`), F3K — RC Hand-Launch Gliders, Penalty schedule (+2 more)

### Community 319 - "IQuery"
Cohesion: 0.16
Nodes (18): IQuery, DateTimeOffset, IClock, UtcNow, CancellationToken, IEventStore, Task, GetClassDefinition (+10 more)

### Community 320 - "CompetitionResult"
Cohesion: 0.12
Nodes (18): PromotionRule, CarryPenalties, Kind, MaxGroupSize, MinGroupSize, TopN, TopPercent, FinalRankingKind (+10 more)

### Community 321 - "F5 Electric Soaring — Generally Applicable Rules"
Cohesion: 0.29
Nodes (7): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score, 4. Round & final score, 5. Re-flights, F5 Electric Soaring — Generally Applicable Rules, Source references

### Community 323 - "Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness)"
Cohesion: 0.40
Nodes (4): Before starting, Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness), What, Why it matters

### Community 324 - ".RunPayoffCaptureScenarioAsync"
Cohesion: 0.50
Nodes (3): Fact, Task, EntryCaptureEventStoreTests

### Community 325 - "TeamContributionState"
Cohesion: 0.17
Nodes (11): CompetitorFinalScoreView, Contributes, TeamContributionState, Contributor, Disqualified, EligibleNotCounting, Ineligible, NoScoreYet (+3 more)

### Community 326 - "gsclient.py"
Cohesion: 0.20
Nodes (9): classify_action(), Exception, True iff action is exactly a read-only allowlisted ACTION (case-sensitive)., Raised for any attempt outside the read-only allowlist., Wraps OS/HTTP-level transport failures (never an allowlist refusal)., Default transport: one urllib.request round trip per request dict., RefusedError, TransportError (+1 more)

### Community 327 - "TaskRound"
Cohesion: 0.06
Nodes (37): ResolvedSchedule, Round, Group, Func, ImmutableArray, Draw, CreatedAt, Status (+29 more)

### Community 328 - "ParameterBinding"
Cohesion: 0.29
Nodes (7): ParameterBinding, At, BoundValue, By, ParameterName, PhaseOrdinal, RoundOrdinal

### Community 330 - "RegisterCompetitorPropertyTests"
Cohesion: 0.24
Nodes (7): IsBogus, DateTimeOffset, Fact, FakeEventStore, Gen, Index, RegisterCompetitorPropertyTests

### Community 331 - ".HandleAsync"
Cohesion: 0.21
Nodes (10): CancellationToken, Task, CancellationToken, DateTimeOffset, Fact, Guid, IReadOnlyList, Task (+2 more)

### Community 332 - "Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus)"
Cohesion: 0.29
Nodes (6): Blast-radius audit (all 12 `T.Piecewise` call sites), Completion note (2026-09-04), Plan, Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus), What, Why it matters

### Community 333 - ".OpenAndCaptureFlightAsync"
Cohesion: 0.17
Nodes (17): CancellationToken, Entry, IEventStore, ImmutableArray, IReadOnlyDictionary, IReadOnlyList, Task, CompetitorTaskResultView (+9 more)

### Community 334 - "A.5 PLENARY MEETING"
Cohesion: 0.33
Nodes (6): A.5.1 Agenda, A.5.2 Technical Meetings, A.5.3 Voting procedure, A.5.4 Plenary Meeting Minutes, A.5.5 Extraordinary Cases, A.5 PLENARY MEETING

### Community 335 - "Story — webmine agent-skill wrapper"
Cohesion: 0.40
Nodes (4): Before starting, Story — webmine agent-skill wrapper, What, Why it matters

### Community 336 - "F3J.8 LAUNCHING"
Cohesion: 0.25
Nodes (8): F3J.8.1 Start Direction, F3J.8.2 Launching, F3J.8.3 Launching Procedure, F3J.8.4 Launching Area, F3J.8.5 Launching Device, F3J.8.6 Early Start, F3J.8.7 Towlines, F3J.8 LAUNCHING

### Community 337 - "Story — OmitFromTeamScore=true witness fixture"
Cohesion: 0.40
Nodes (4): Before starting, Story — OmitFromTeamScore=true witness fixture, What, Why it matters

### Community 338 - "Penalty"
Cohesion: 0.18
Nodes (11): Penalty, By, CompetitorRef, InfractionType, Scope, TaskRound, PenaltyScope, Competition (+3 more)

### Community 339 - ".DescribeContributors"
Cohesion: 0.40
Nodes (4): Func, Placing, Ref, Score

### Community 340 - ".ComputeGroupViews"
Cohesion: 0.33
Nodes (9): Competition, Entry, ImmutableArray, IReadOnlyDictionary, EntryGapsView, FlightGapsView, GroupRecordingView, GroupSpotView (+1 more)

### Community 341 - "NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)"
Cohesion: 0.18
Nodes (11): 1. Pilot assignment to groups (the draw) — **and the open problem**, 2. Launch (`NZ.3.15.1 d, f, g`), 3. Data the timer / helper collects, 4. The task (`NZ.3.15.1 c`), 5. Score (`NZ.3.15.1 i`), 6. Rounds, 7. Re-flights (`NZ.3.15.1 h`), 8. A defect in the rule text — `NZ.3.15.1 j` (+3 more)

### Community 342 - "ClassDefinitionPublished"
Cohesion: 0.08
Nodes (26): IDomainEvent, CancellationToken, IEventStore, Task, ClassDefinitionLoader, ClassDefinitionProjection, DateTimeOffset, ClassDefinitionEvent (+18 more)

### Community 343 - "DocumentClassLibraryQuery"
Cohesion: 0.38
Nodes (5): CancellationToken, IDocumentSessionFactory, IReadOnlyList, Task, DocumentClassLibraryQuery

### Community 344 - "F3B — RC Multi-Task Gliders"
Cohesion: 0.22
Nodes (9): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score — three partial scores (`F3B.2.6`), 4. Round score (`F3B.2.7`), 5. Final classification (`F3B.2.8`), 6. Re-flights (`F3B.1.5`, `F3B.1.11`), F3B — RC Multi-Task Gliders, Penalty schedule (+1 more)

### Community 345 - "SoarscoreEventTypes"
Cohesion: 0.50
Nodes (4): Alias, IReadOnlyList, Type, SoarscoreEventTypes

### Community 346 - ".HandleAsync"
Cohesion: 0.18
Nodes (15): CancellationToken, IClock, IEventStore, Task, AnnulEntryHandler, CancellationToken, DateTimeOffset, Fact (+7 more)

### Community 347 - "_FormScanner"
Cohesion: 0.29
Nodes (3): HTMLParser, _FormScanner, Collects form fields and selects-with-options from a WebForms page.

### Community 348 - "F5J — RC Electric Powered Thermal Duration Gliders"
Cohesion: 0.25
Nodes (8): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score (`5.5.11.12`), 4. Round score, 5. Final classification (`5.5.11.13`), 6. Re-flights (`5.5.11.6`), F5J — RC Electric Powered Thermal Duration Gliders, Source references

### Community 349 - "EntryEventJsonTests"
Cohesion: 0.47
Nodes (3): DateTimeOffset, Fact, EntryEventJsonTests

### Community 351 - "_documented_row"
Cohesion: 0.40
Nodes (5): _documented_row(), _picker_scenarios(), composite, One picker row built from documented parts, plus the parts themselves., Unique-value option lists, some duplicated, shuffled into a document order.

### Community 352 - "F3J.11 FINAL CLASSIFICATION"
Cohesion: 0.40
Nodes (5): F3J.11.2 Fly-off Working Time, F3J.11.3 Fly-off Scoring, F3J.11.4 Final Placing, F3J.11.5 Ranking for International Team Classification, F3J.11 FINAL CLASSIFICATION

### Community 353 - "F3J.1 GENERAL RULES"
Cohesion: 0.40
Nodes (5): F3J.1.1 Definition of a Radio-Controlled Glider, F3J.1.2 Prefabrication of the Model aircraft, F3J.1.3 Characteristics of Radio-Controlled Gliders, F3J.1.4 Competitors and Helpers, F3J.1 GENERAL RULES

### Community 354 - "F3J.2 THE FLYING SITE"
Cohesion: 0.40
Nodes (5): F3J.2.1 Site Surface, F3J.2.2 Site Marking, F3J.2.3 Landing Spots, F3J.2.4 Safety Rules, F3J.2 THE FLYING SITE

### Community 355 - "Soarscore.Application"
Cohesion: 0.08
Nodes (18): Soarscore.Infrastructure.Competitions, Soarscore.Infrastructure.Tests, Soarscore.Infrastructure.CompetitionClasses, Soarscore.Infrastructure.People, Soarscore.Application.Tests.Queries.Competitions, Soarscore.Application.Queries.People, Soarscore.Infrastructure, Soarscore.Application.Queries.Competitions (+10 more)

### Community 356 - "Story — Operational tie-break resolution: record the outcome, re-rank"
Cohesion: 0.40
Nodes (4): Before starting, Story — Operational tie-break resolution: record the outcome, re-rank, What, Why it matters

### Community 357 - "NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Launch (`NZ.3.13.1 d, f, g`), 3. Data the timer / helper collects, 4. The task (`NZ.3.13.1 c`), 5. Score (`NZ.3.13.1 i`), 6. Rounds, 7. Re-flights (`NZ.3.13.1 h`), 8. What is not stated (+2 more)

### Community 359 - "PenaltyEnginePropertyTests"
Cohesion: 0.43
Nodes (4): Fact, Gen, ImmutableArray, PenaltyEnginePropertyTests

### Community 360 - "Story — Literal record replay scenarios (a whole fixture readable as Gherkin)"
Cohesion: 0.40
Nodes (4): Before starting, Story — Literal record replay scenarios (a whole fixture readable as Gherkin), What, Why it matters

### Community 361 - "Result"
Cohesion: 0.08
Nodes (25): CancellationToken, Task, CancellationToken, Task, CancellationToken, Task, CancellationToken, Task (+17 more)

### Community 362 - "Story — Seed-definition parallel run (corpus fixtures under the seed classes)"
Cohesion: 0.40
Nodes (4): Before starting, Story — Seed-definition parallel run (corpus fixtures under the seed classes), What, Why it matters

### Community 363 - ".ApplyAsync"
Cohesion: 0.40
Nodes (4): CancellationToken, IEvent, IReadOnlyList, Task

### Community 364 - "DropPolicy"
Cohesion: 0.25
Nodes (8): DropPolicy, ApplyWhenResultsAtLeast, ApplyWhenRoundsCompletedAtLeast, Dimension, DropCount, DropDimension, ByRound, ByTask

### Community 366 - "SeedF3B"
Cohesion: 0.09
Nodes (17): DateTimeOffset, Gen, ClassDefinitionProjectionPropertyTests, SeedAggregate, Definition, TaskA, SeedF3B, Definition (+9 more)

### Community 368 - "RoundComposition"
Cohesion: 0.25
Nodes (8): RoundComposition, Kind, MaxRounds, RequireDistinctTaskPerRound, TasksPerRound, CompositionKind, ChooseFromCatalogue, FixedSequence

### Community 369 - "F3J.6 ORGANISATION OF THE FLYING"
Cohesion: 0.50
Nodes (4): F3J.12.1 for details., F3J.6.1 Rounds and Groups, F3J.6.2 Flying in Groups, F3J.6 ORGANISATION OF THE FLYING

### Community 370 - "F3J.13 ADVISORY INFORMATION"
Cohesion: 0.50
Nodes (4): F3J.13.1 Organisational Requirements, F3J.13.2 Time-keeper Duties, F3J.13.3 Groups, F3J.13 ADVISORY INFORMATION

### Community 371 - "ExpectedVersion"
Cohesion: 0.14
Nodes (16): ExpectedVersion, Any, IsAny, IsExact, IsNoStream, NoStream, Version, Kind (+8 more)

### Community 372 - "F3J.9 LANDING"
Cohesion: 0.50
Nodes (4): F3J.9.1 Landing SCircle, F3J.9.2 Timekeeper Position, F3J.9.3 Model Retrrieving, F3J.9 LANDING

### Community 373 - ".NormalisedDefinition"
Cohesion: 0.39
Nodes (5): DateTimeOffset, Fact, FakeEventStore, Task, ScoreTaskRoundHandlerTests

### Community 375 - "F3K.2 DEFINITION OF MODEL GLIDER"
Cohesion: 0.29
Nodes (7): F3K.2.1 Specifications, F3K.2.2 Losing a part of the model glider, F3K.2.3 Change of model glider, F3K.2.4 Retrieving of model glider, F3K.2.5 Radio frequencies, F3K.2.6 Ballast, F3K.2 DEFINITION OF MODEL GLIDER

### Community 376 - "Story — F5K fixture from the GliderScore server DB export"
Cohesion: 0.29
Nodes (6): As built (2026-09-04), Before starting, Plan, Story — F5K fixture from the GliderScore server DB export, What, Why it matters

### Community 377 - "RulesAmendment"
Cohesion: 0.40
Nodes (5): RulesAmendment, At, By, Definition, Reason

### Community 378 - "Mutation"
Cohesion: 0.29
Nodes (7): Mutation, DeleteMember, DuplicateMember, MoveBetweenGroups, SplitOffSingleton, SubstituteUnregistered, WithdrawAMember

### Community 380 - "SeedNzNAles123"
Cohesion: 0.67
Nodes (3): SeedNzNAles123, Definition, TaskD

### Community 381 - "SeedNzPRadian"
Cohesion: 0.67
Nodes (3): SeedNzPRadian, Definition, TaskD

### Community 382 - "NZ ALES — Generally Applicable Rules"
Cohesion: 0.33
Nodes (6): 1. The shared shape, 2. Data the timer / helper collects, 3. What is *not* scoring data, 4. Re-flights, NZ ALES — Generally Applicable Rules, Source references

### Community 383 - "CreateCompetitionPropertyTests"
Cohesion: 0.33
Nodes (6): DurationDays, OffsetDays, Location, Gen, Name, CreateCompetitionPropertyTests

### Community 384 - "Model"
Cohesion: 0.33
Nodes (6): FlightModel, List, Model, Flights, LastAnnulmentReason, PenaltyCount

### Community 385 - ".EvaluateTerm"
Cohesion: 0.34
Nodes (6): ConstantTerm, Value, IReadOnlyDictionary, FlightInterpreter, Intrinsic, TermContribution

### Community 387 - "ScoringServiceAnnulmentTests"
Cohesion: 0.40
Nodes (4): DateTimeOffset, Fact, ImmutableArray, ScoringServiceAnnulmentTests

### Community 388 - "FlightModel"
Cohesion: 0.40
Nodes (4): MeasurementModel, FlightModel, Measurements, Sequence

### Community 389 - "CompetitorModel"
Cohesion: 0.40
Nodes (5): Guid, CompetitorModel, CompetitorNumber, Id, Withdrawn

### Community 391 - "2.5 CONTESTS"
Cohesion: 0.67
Nodes (3): 2.5.1 Contestants Meeting., 2.5.2 Round Identification., 2.5 CONTESTS

### Community 393 - "Actual"
Cohesion: 0.67
Nodes (3): Competition, Actual, Value

### Community 394 - "Actual"
Cohesion: 0.67
Nodes (3): Entry, Actual, Value

### Community 395 - "MeasurementModel"
Cohesion: 0.67
Nodes (3): MeasurementModel, AmendmentCount, Metric

## Knowledge Gaps
- **2262 isolated node(s):** `context7`, `rider`, `$schema`, `.opencode/plugins/graphify.js`, `$(SoarscoreTargetFramework)` (+2257 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **16 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ClassDefinition` connect `ClassDefinition` to `FakeEntryQuery`, `ReflightingForAMissedRoundSteps`, `SystemClock`, `CatalogueDrawPropertyTests`, `ScoringTeamCommandHandlerTests`, `PrescribeDrawDecideTests`, `.New`, `.MapQueries`, `ResolvedTask`, `EntryId`, `IStoreFixture`, `.All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state`, `.CheckLimits`, `Corpus.cs`, `MeasuredValue`, `ClosingACompetitionSteps`, `.New`, `GliderscoreFixture`, `FlightOpened`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `ScoringACompetitionSteps`, `.Of`, `CompetitionEvent`, `When`, `ScoringTeamsSteps`, `FinaliseDecideTests`, `ScoringTeamId`, `OpenFlight`, `MetricDefinition`, `.Exact`, `CompetitionReplaceTaskRoundPropertyTests`, `GroupSpotsPropertyTests`, `.CompetitionAdopting`, `CompetitionId`, `.BuildDrawnCompetition`, `.HandleAsync`, `ScoringServicePropertyTests`, `.Validate`, `.DrawnCompetitionAsync`, `.New`, `DrawAcceptanceDecideTests`, `IDispatcher`, `PublishClassDefinition`, `RecordCompetitionPenaltyDecideTests`, `.ScoreCompetition`, `RegisterCompetitorHandler`, `WithdrawCompetitorHandler`, `PhaseDefinition`, `SeedF3K`, `.BuildDrawnCompetition`, `EntryModelBasedFoldTests`, `TaskDefinition`, `PrescribeDrawPropertyTests`, `ReflightRule`, `BindParameterPropertyTests`, `FinaliseValidityPropertyTests`, `ClassDefinitionValidationPropertyTests`, `.TwoRoundCompetitionAsync`, `.Seed`, `SeeingWhatIsRecordedSteps`, `.DrawnCompetitionAsync`, `SeedF5kNdc`, `ScoringServiceZeroRoutingTests`, `.ScoreGroup`, `.SetUpAsync`, `AdoptedRules`, `DrawPhase`, `SeedNzF3kNdc`, `RecordCompetitionPenalty`, `PenaltyDefinition`, `NumberOrParam`, `RecordEntryPenalty`, `.SeedDrawnCompetitionAsync`, `IQuery`, `CompetitionResult`, `.RunPayoffCaptureScenarioAsync`, `RegisterCompetitorPropertyTests`, `.HandleAsync`, `.OpenAndCaptureFlightAsync`, `ClassDefinitionPublished`, `SeedF3B`, `.NormalisedDefinition`, `RulesAmendment`, `SeedNzNAles123`, `SeedNzPRadian`, `CreateCompetitionPropertyTests`?**
  _High betweenness centrality (0.123) - this node is a cross-community bridge._
- **Why does `CompetitorId` connect `CompetitorId` to `Competition`, `Soarscore.Domain.PublishedClassDefinition`, `FakeEntryQuery`, `ReflightingForAMissedRoundSteps`, `SystemClock`, `CatalogueDrawPropertyTests`, `ScoringTeamCommandHandlerTests`, `PrescribeDrawDecideTests`, `.New`, `.MapQueries`, `EntryId`, `IStoreFixture`, `.All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state`, `EntryCapturePropertyTests`, `ClosingACompetitionSteps`, `.New`, `Entry`, `Comparator`, `FlightOpened`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `ScoringACompetitionSteps`, `AmendMeasurementDecideTests`, `ProtectionGroupId`, `.Of`, `TaskRoundRecordingPropertyTests`, `CompetitionEvent`, `When`, `ScoringTeamsSteps`, `ScoringTeamId`, `FakeEventStore`, `ReflightRole`, `GroupSpotsPropertyTests`, `TeamsDecideTests`, `CompetitionId`, `.BuildDrawnCompetition`, `.ComputeEntries`, `ScoringServicePropertyTests`, `.DrawnCompetitionAsync`, `.New`, `DrawAcceptanceDecideTests`, `ReplayDriver`, `IDispatcher`, `RecordCompetitionPenaltyDecideTests`, `.ScoreCompetition`, `RegisterCompetitorHandler`, `WithdrawCompetitorHandler`, `.Classify`, `.BuildDrawnCompetition`, `TaskDefinition`, `PrescribeDrawPropertyTests`, `ReflightRule`, `OpenFlightDecideTests`, `RecordEntryPenaltyDecideTests`, `.TwoRoundCompetitionAsync`, `GroupId`, `DrawProtectionPropertyTests`, `SeeingWhatIsRecordedSteps`, `TeamClassificationEngineTests`, `TeamClassificationPropertyTests`, `.DrawnCompetitionAsync`, `ScoringServiceZeroRoutingTests`, `Competitor`, `AssigningSpotsSteps`, `PersonId`, `.BuildDrawnCompetition`, `.SetUpAsync`, `.BuildGroups`, `DrawPhase`, `RecordCompetitionPenalty`, `AnnulEntryDecideTests`, `.SeedDrawnCompetitionAsync`, `.SeedCompetitionWithProtectedPair`, `.RunPayoffCaptureScenarioAsync`, `TeamContributionState`, `TaskRound`, `.OpenAndCaptureFlightAsync`, `Penalty`, `.DescribeContributors`, `.ComputeGroupViews`, `EntryEventJsonTests`, `Fact`, `ScoringServiceAnnulmentTests`?**
  _High betweenness centrality (0.094) - this node is a cross-community bridge._
- **Why does `CompetitionId` connect `CompetitionId` to `Competition`, `Soarscore.Domain.PublishedClassDefinition`, `FakeEntryQuery`, `IProjection`, `SystemClock`, `WithdrawCompetitorHandler`, `PersonId`, `ScoringTeamCommandHandlerTests`, `.New`, `.MapQueries`, `IStoreFixture`, `.SetUpAsync`, `EntryCapturePropertyTests`, `DrawPhase`, `Entry`, `RecordCompetitionPenalty`, `Comparator`, `FlightOpened`, `OpenFlightDecideTests`, `AmendMeasurementDecideTests`, `ProtectionGroupId`, `.Of`, `AnnulEntryDecideTests`, `.SeedDrawnCompetitionAsync`, `RecordEntryPenaltyDecideTests`, `.SeedCompetitionWithProtectedPair`, `FakeEventStore`, `.Exact`, `.RunPayoffCaptureScenarioAsync`, `CompetitionSummary`, `RegisterCompetitorPropertyTests`, `.OpenAndCaptureFlightAsync`, `.TwoRoundCompetitionAsync`, `.HandleAsync`, `GroupId`, `.DrawnCompetitionAsync`, `.New`, `EntryEventJsonTests`, `.Seed`, `ReplayDriver`, `IDispatcher`, `PublishClassDefinition`, `.DrawnCompetitionAsync`, `RegisterCompetitorHandler`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **What connects `context7`, `rider`, `$schema` to the rest of the system?**
  _2262 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Competition` be split into smaller, more focused modules?**
  _Cohesion score 0.07183908045977011 - nodes in this community are weakly interconnected._
- **Should `Soarscore.Domain.PublishedClassDefinition` be split into smaller, more focused modules?**
  _Cohesion score 0.04613681102362205 - nodes in this community are weakly interconnected._
- **Should `ReflightingForAMissedRoundSteps` be split into smaller, more focused modules?**
  _Cohesion score 0.12775842044134728 - nodes in this community are weakly interconnected._