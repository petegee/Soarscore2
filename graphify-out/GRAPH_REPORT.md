# Graph Report - SoarScore2  (2026-09-04)

## Corpus Check
- 628 files · ~1,078,012 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 7977 nodes · 21676 edges · 372 communities (364 shown, 8 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 2339 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `c66d53de`
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
- Soarscore.Domain
- CatalogueDrawPropertyTests
- .CheckLimits
- Teams: Findings and Implementation Options
- ScoringTeamCommandHandlerTests
- PrescribingADrawSteps
- DropPolicy
- .New
- .SeedWired
- ResolvedTask
- EntryId
- IStoreFixture
- .All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state
- IDispatcher
- Corpus.cs
- ClassDefinition
- MeasuredValue
- Soarscore.Application.Commands.CompetitionClasses
- AcceptanceFixture
- EntryCapturePropertyTests
- Soarscore.Application.Queries.Competitions
- ClosingACompetitionSteps
- .New
- GliderscoreFixture
- Phase
- Entry
- 3.16 CLASS Q: NZ F5K (Hand Launch Electric Glider)
- Comparator
- FlightOpened
- ReflightingAGroupSteps
- AcceptingTheDrawSteps
- Then
- AmendMeasurementDecideTests
- .Exact
- .Of
- Plan — Capturing a score: the Entry write path and `entry_index`
- Plan — Scoring: de-orphaning the scoring engine
- PenaltyDefinition
- The Competition Class notation — draft spec
- TaskRoundRecordingPropertyTests
- ReflightRole
- Refined plan
- PhaseDrawn
- When
- 2 SOARING (All Classes)
- B.4 DEFINITIONS OF EXPRESSIONS
- ScoringTeamsSteps
- .LoadCurrentAsync
- FinaliseDecideTests
- ScoringTeamMembership
- Scoring Service Build Plan
- .SeedOpenEntry
- .SeedScoredTeamCompetition
- Work items
- MetricDefinition
- .HandleAsync
- NumberOrParam
- Plan — Catalogue-choice draws: the CD picks each round's task
- .BuildCompetition
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
- PersonRegistered
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
- NormalisationEnginePropertyTests
- F3F.1 GENERAL RULES
- Dispatcher
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
- PrescribeDrawDecideTests
- Rule map — topic × class
- SECTION A - CIAM INTERNAL REGULATIONS
- Design decisions — settled here, do not relitigate
- FakeEventStore
- F3K.11 DEFINITIONS OF TASKS
- .LoadCurrentAsync
- Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server
- WithdrawCompetitorHandler
- IProjection
- ContactDetails
- 1 GENERAL DEFINITIONS
- .AddSoarscoreInfrastructure
- .Classify
- ReflightRule
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
- .SeedCompetition
- PrescribeDrawEventStoreTests
- TaskDefinition
- 4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS
- PART 5 – TECHNICAL REGULATIONS FOR RADIO
- Plan
- Plan
- Story — Move the store adapters onto the JasperFx shared contracts
- Raw score
- PrescribeDrawPropertyTests
- GliderScore fixture extraction
- PublishedClassDefinition
- test_csvparse.py
- OpenFlightDecideTests
- mine_catalogue.py
- NZ Class M — ALES 200 (Altitude Limited Electric Soaring)
- Enumerations.cs
- 3.17 CLASS R: E-RES 2M (Electric Rudder Elevator Spoiler 2M Glider)
- BindParameterPropertyTests
- DrawAcceptancePropertyTests
- Soarscore — Users
- Drop-worst
- ClassDefinitionSummary
- DrawingACatalogueChoicePhaseSteps
- PersonSummary
- LADR-0002 — Competition Class definition: representation, ingestion and identity
- .Select
- F5K — RC Electric Thermal Duration, Multiple-Task
- Annulment
- F3J — RC Thermal Duration Gliders
- C.16.2 Requirements for radio control
- C.2.1 First category events
- 5 F3K – RADIO CONTROL HAND LAUNCH GLIDERS
- Plan
- Scoring Service — Open Design Issues
- Soarscore.Acceptance.Tests.csproj
- Soarscore.sln
- test_fetch_comp.py
- .RunPayoffCaptureScenarioAsync
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
- .Apply
- Entry-completeness indicator
- Plan
- Ranking & tie-breaks
- Deferred decisions
- .LoadAsync
- Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC
- ScoringCorpusPropertyTests
- Compliance check
- f5j-hawkes-bay-trials/ladder.py
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
- CompetitionResult
- RegisterPersonHandler
- 5.5.2 CONTEST RULES
- Model
- Normalisation
- Soarscore.Application.csproj
- Soarscore.Infrastructure.csproj
- NZMAA Flying Rules, Section 5: Soaring — March 2024 (extracted source text)
- SeedF5kNdc
- .BuildCompetition
- Story — Permitted scopes on PenaltyDefinition (adoption-time scope hardening)
- PhaseDefinition
- AssigningSpotsSteps
- Story — Ranking's secondary key: RawScore tie-break
- TeamsEventJsonTests
- Story — GliderScore webmine tool (read-only online comp acquisition)
- .Apply
- RegisterCompetitorHandler
- LayerRuleTests
- LADR-0003 — Library choices
- 3.1 CLASS A: 6 MINUTE THERMAL DURATION
- 3.7 CLASS H : NEW ZEALAND THERMAL 2 METRE RULES
- fetch_comp.py
- C.19.1 Penalties imposed by the Contest Director
- C.7 CONTEST OFFICIALS
- .BuildDrawnCompetition
- FindEntries
- Story — Coverage: normalisation is per group, not per round
- Stop storing `Entry.WorkingTime`
- IDomainEvent
- .ComputeGroupViews
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
- FakeEventStore
- FakeClassLibraryQuery
- .PostCommandRawAsync
- .Rank
- Story — Resolve GliderScore scoring arithmetic from source
- Findings
- RecordEntryPenaltyDecideTests
- .Apply
- Story — NZ F3K NDC seed class
- CompetitorId
- f5j-nz-south-island/ladder.py
- extract.py
- GliderScore fixture corpus index
- SeedF3J
- Refined plan
- Seed classes — the authoring source
- LookupRow
- .SeedEntryUnderF5K
- C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY
- C.15.6 Classification
- ScoringTeamId
- AnnulEntryDecideTests
- F3J.10 SCORING
- .SearchAsync
- 5.5.11.1 General Rules
- opencode.json
- ProtectedPair
- Soarscore.Application
- GliderScore golden comparison — state after http-grain-one-metric-bridge
- FixtureLoader
- Story — Smaller items
- .mcp.json
- graphify.js
- F3 Soaring — Generally Applicable Rules
- F3K — RC Hand-Launch Gliders
- .BuildDispatcher
- .ScoreGroup
- F5 Electric Soaring — Generally Applicable Rules
- tech-debt.md
- Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness)
- .ScoreWith
- A.6 BUREAU
- IEntryQuery
- TaskRound
- F3B — RC Multi-Task Gliders
- RegisterCompetitorPropertyTests
- .SeedOpenEntryWithFlight
- Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus)
- .MapQueries
- f3k-southern-fling/ladder.py
- Story — webmine agent-skill wrapper
- F3J.8 LAUNCHING
- Story — OmitFromTeamScore=true witness fixture
- Mutation
- ComparisonReport
- NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)
- ClassDefinitionPublished
- DocumentClassLibraryQuery
- NZ ALES — Generally Applicable Rules
- SoarscoreEventTypes
- .HandleAsync
- _FormScanner
- F5J — RC Electric Powered Thermal Duration Gliders
- EntryEventJsonTests
- .TaskWithParameterisedWorkingTime
- _documented_row
- CreateCompetitionPropertyTests
- F3K.2 DEFINITION OF MODEL GLIDER
- F3K.9 DEFINITION OF A ROUND
- People/TestDoubles.cs
- Story — Operational tie-break resolution: record the outcome, re-rank
- NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)
- .ApplyRounding
- PenaltyEnginePropertyTests
- Result
- Soarscore.SeedData
- 2.5 CONTESTS
- FakeClock
- FakeTransport
- .Publishing_then_optionally_retiring_preserves_identity_fields_and_models_RetiredAt_correctly
- ExpectedVersion
- F3K.10 SCORING
- F3K.4 SAFETY
- RulesAmendment
- F3K.1 GENERAL
- RateTerm

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

## Communities (372 total, 8 thin omitted)

### Community 0 - "Competition"
Cohesion: 0.05
Nodes (47): ResolvedSchedule, DateOnly, DateTimeOffset, Defect, ImmutableArray, Penalty, ReflightRuling, Result (+39 more)

### Community 1 - "Soarscore.Domain.PublishedClassDefinition"
Cohesion: 0.07
Nodes (7): Soarscore.Domain.Scoring, Soarscore.Domain.Tests, Soarscore.Domain.Competitions, Soarscore.Domain.People, Soarscore.Domain.Entries, Soarscore.Domain.PublishedClassDefinition, Soarscore.Application.Tests.Queries.Scoring

### Community 2 - "FakeEntryQuery"
Cohesion: 0.22
Nodes (18): CancellationToken, IClock, IEventStore, Task, OpenEntryHandler, EntrySummary, EntryOpened, DateTimeOffset (+10 more)

### Community 3 - "ReflightingForAMissedRoundSteps"
Cohesion: 0.13
Nodes (12): CompetitionId, Dictionary, Given, Group, HttpClient, HttpResponseMessage, IReadOnlyList, List (+4 more)

### Community 4 - "SystemClock"
Cohesion: 0.09
Nodes (43): CancellationToken, IClock, IEventStore, Task, DrawPhaseHandler, CancellationToken, Competition, IEventStore (+35 more)

### Community 5 - "test_gsclient.py"
Cohesion: 0.07
Nodes (53): base_competition(), check_integrity(), check_rule_1(), check_rule_2(), check_rule_3(), check_rule_4(), check_rule_5(), composite_key() (+45 more)

### Community 6 - "Work items"
Cohesion: 0.09
Nodes (22): As built (2026-08-26), Before starting — all discharged, Decisions settled during planning (2026-08-26), Execution plan, Known traps (pre-answered by planning — verified against the tree), Out of scope (deferrals restated), Pipeline shape (one feature per fixture, shared machinery), Plan (+14 more)

### Community 7 - "Soarscore.Domain"
Cohesion: 0.08
Nodes (9): Soarscore.Application.Tests.Commands.Entries, Soarscore.Application.Shared.Entries, Soarscore.Application.Commands.Competitions, Soarscore.Application.Shared.People, Soarscore.Application.Tests.Queries.Entries, Soarscore.Application.Queries.Entries, Soarscore.Domain, Soarscore.Application.Tests.Shared.Entries (+1 more)

### Community 8 - "CatalogueDrawPropertyTests"
Cohesion: 0.13
Nodes (14): MinPerGroupByRound, Sizes, MinPerGroup, TaskCount, Competition, DateTimeOffset, Dictionary, Fact (+6 more)

### Community 9 - ".CheckLimits"
Cohesion: 0.15
Nodes (9): IReadOnlyList, JsonSerializerOptions, List, ClassDefinitionIngestion, ClassDefinitionIngestionFixtures, Fact, ClassDefinitionIngestionPropertyTests, Fact (+1 more)

### Community 10 - "Teams: Findings and Implementation Options"
Cohesion: 0.05
Nodes (43): Application, API, and infrastructure, Best fit, Best fit, Best fit, Changes required, Changes required, Changes required, Classification and finalisation (+35 more)

### Community 11 - "ScoringTeamCommandHandlerTests"
Cohesion: 0.15
Nodes (18): CancellationToken, IClock, IEventStore, Task, AssignScoringTeamMembershipHandler, CancellationToken, Task, CancellationToken (+10 more)

### Community 12 - "PrescribingADrawSteps"
Cohesion: 0.16
Nodes (13): CompetitionId, Given, HttpClient, HttpResponseMessage, IEnumerable, IReadOnlyList, List, Table (+5 more)

### Community 13 - "DropPolicy"
Cohesion: 0.11
Nodes (29): aggregate, dropped, DropPolicy, ApplyWhenResultsAtLeast, ApplyWhenRoundsCompletedAtLeast, Dimension, DropCount, ImmutableArray (+21 more)

### Community 14 - ".New"
Cohesion: 0.08
Nodes (28): Competition, DateOnly, CompetitionCreated, DrawAccepted, EntryQuery, FakeEventStore, ImmutableArray, Store (+20 more)

### Community 15 - ".SeedWired"
Cohesion: 0.19
Nodes (17): CancellationToken, IEventStore, ImmutableArray, Task, GetTeamRosters, GetTeamRostersHandler, ProtectionGroupRosterView, ScoringTeamMemberView (+9 more)

### Community 16 - "ResolvedTask"
Cohesion: 0.09
Nodes (15): Bindings, ClassDef, ExpectedRawScore, AllFlights, ResolvedTask, ResolvedTiming, ArgumentException, Fact (+7 more)

### Community 17 - "EntryId"
Cohesion: 0.09
Nodes (18): Guid, EntryId, CompetitionId, Dictionary, Given, Group, HttpClient, HttpResponseMessage (+10 more)

### Community 18 - "IStoreFixture"
Cohesion: 0.06
Nodes (51): ICommandHandler, CancellationToken, IClock, IEventStore, ImmutableArray, Task, FinaliseCompetition, FinaliseCompetitionHandler (+43 more)

### Community 19 - ".All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state"
Cohesion: 0.13
Nodes (21): DeclaredTeamContributor, CompetitorRef, Placing, Score, DeclaredTeamResult, BestIndividualPlacing, Contributors, Name (+13 more)

### Community 20 - "IDispatcher"
Cohesion: 0.19
Nodes (15): IDispatcher, GroupSpot, CancellationToken, Fact, Task, DrawAcceptanceEventStoreTests, Ct, CancellationToken (+7 more)

### Community 21 - "Corpus.cs"
Cohesion: 0.50
Nodes (4): ImmutableArray, Corpus, All, SeedClass

### Community 22 - "ClassDefinition"
Cohesion: 0.11
Nodes (24): Path, HashSet, IEnumerable, ImmutableArray, IReadOnlyDictionary, List, Phase, Task (+16 more)

### Community 23 - "MeasuredValue"
Cohesion: 0.11
Nodes (25): Exception, Parameter, AllowedValues, BoundAt, DefaultValue, Kind, Name, Unit (+17 more)

### Community 24 - "Soarscore.Application.Commands.CompetitionClasses"
Cohesion: 0.08
Nodes (11): Soarscore.Application.Tests, Soarscore.Application.Tests.Commands.CompetitionClasses, Soarscore.Application.Queries.CompetitionClasses, Soarscore.Application.Tests.Shared.CompetitionClasses, Soarscore.Application.Shared.CompetitionClasses, Soarscore.Application.Commands.CompetitionClasses, Soarscore.Application.Tests.Queries.CompetitionClasses, Fact (+3 more)

### Community 25 - "AcceptanceFixture"
Cohesion: 0.06
Nodes (28): AfterTestRun, BeforeTestRun, Soarscore.Api, Soarscore.ArchitectureTests, GeneratedRegex, HttpMethodMetadata, MethodInfo, Regex (+20 more)

### Community 26 - "EntryCapturePropertyTests"
Cohesion: 0.06
Nodes (33): DecideActual, DecideFlightModel, DecideModel, FlagValue, MetricIndex, NumericValue, Pick, PlannedCapture (+25 more)

### Community 27 - "Soarscore.Application.Queries.Competitions"
Cohesion: 0.12
Nodes (12): Soarscore.Application.Commands.Entries, Soarscore.Application.Commands.People, Soarscore.Application.Tests.Queries.Competitions, Soarscore.Acceptance.Tests.Support, Soarscore.Api.Queries, Soarscore.Application.Queries.Competitions, Soarscore.Application.Queries.Scoring, Soarscore.Acceptance.Tests.Support.Gliderscore (+4 more)

### Community 28 - "ClosingACompetitionSteps"
Cohesion: 0.16
Nodes (12): CompetitionId, DateTimeOffset, Dictionary, Given, Group, HttpClient, HttpResponseMessage, List (+4 more)

### Community 29 - ".New"
Cohesion: 0.11
Nodes (19): TaskRoundState, Annulled, Complete, Drawn, InProgress, DateTimeOffset, Fact, EntryTests (+11 more)

### Community 30 - "GliderscoreFixture"
Cohesion: 0.14
Nodes (28): JsonElement, List, Dictionary, IReadOnlyList, CompetitionFile, CompetitionIdentity, CompetitionScoring, CompPilotRow (+20 more)

### Community 31 - "Phase"
Cohesion: 0.14
Nodes (14): Round, Draw, CreatedAt, Status, Phase, Draw, Ordinal, Rounds (+6 more)

### Community 32 - "Entry"
Cohesion: 0.09
Nodes (27): DateTimeOffset, Func, ImmutableArray, Penalty, PenaltyRecorded, Result, Entry, Annulment (+19 more)

### Community 33 - "3.16 CLASS Q: NZ F5K (Hand Launch Electric Glider)"
Cohesion: 0.05
Nodes (37): 3.16.10 Landing rules:, 3.16.11 Retrieving of model glider, 3.16.12 Safety, 3.16.13 Mid-air collision, 3.16.14 Forbidden airspace, 3.16.15 Weather conditions / Interruptions, 3.16.16 Definition of landing, 3.16.17 Flight time (+29 more)

### Community 34 - "Comparator"
Cohesion: 0.16
Nodes (18): GroupNo, KeyCollection, Mismatches, PilotNo, RoundNo, StandingsCompared, Competition, Dictionary (+10 more)

### Community 35 - "FlightOpened"
Cohesion: 0.13
Nodes (21): FlightPlan, Amendment, At, By, NewValue, Reason, DateTimeOffset, Penalty (+13 more)

### Community 36 - "ReflightingAGroupSteps"
Cohesion: 0.16
Nodes (11): CompetitionId, Dictionary, Given, Group, HttpClient, HttpResponseMessage, List, ProblemDetails (+3 more)

### Community 37 - "AcceptingTheDrawSteps"
Cohesion: 0.14
Nodes (9): CompetitionId, Given, HttpClient, HttpResponseMessage, IReadOnlyList, List, Task, AcceptingTheDrawSteps (+1 more)

### Community 38 - "Then"
Cohesion: 0.10
Nodes (15): ImmutableArray, CompetitionScoreView, Then, CompetitionId, DateTimeOffset, Dictionary, Given, Group (+7 more)

### Community 39 - "AmendMeasurementDecideTests"
Cohesion: 0.14
Nodes (13): AmendmentFact, MeasurementDigest, DateTimeOffset, Fact, Gen, ImmutableArray, IReadOnlyList, AmendMeasurementDecideTests (+5 more)

### Community 40 - ".Exact"
Cohesion: 0.14
Nodes (24): CancellationToken, IClock, IEventStore, Task, AddProtectionGroupMemberHandler, CancellationToken, IClock, IEventStore (+16 more)

### Community 41 - ".Of"
Cohesion: 0.15
Nodes (7): Fact, InlineData, Theory, BindParameterDecideTests, Fact, ImmutableArray, CaptureMeasurementDecideTests

### Community 42 - "Plan — Capturing a score: the Entry write path and `entry_index`"
Cohesion: 0.06
Nodes (32): Acceptance, Context, Dependency order, Finding 1 — `entry_index` cannot be built from the Entry events as they stand · **fixed here**, Finding 2 — `TimeWindow.End` cannot be stated under `UntilAllFlightsComplete` · **fixed here**, Finding 3 — `OpenFlight` must not gate on the working-time window · **scope removal**, Finding 4 — capture-time rounding · **decided: apply it**, Four findings that shape the scope (+24 more)

### Community 43 - "Plan — Scoring: de-orphaning the scoring engine"
Cohesion: 0.06
Nodes (32): Acceptance, Context, Dependency order, Finding 1 — `ScoreCompetition` is a shell, not a mis-typed method, Finding 2 — amendment resolution exists nowhere in the tree, Finding 3 — the engine speaks `string`, the domain speaks typed ids, Finding 4 — `RecordedPenalty` and `Penalty` do not have the same shape, Finding 5 — nothing ever marks a task-round `Complete`, so the leaderboard must derive its own field (+24 more)

### Community 44 - "PenaltyDefinition"
Cohesion: 0.18
Nodes (21): AccruedInfo, PenaltyScope, PenaltyDefinition, Accrual, Effects, ExclusionGroups, PermittedScopes, PenaltyEffectSpec (+13 more)

### Community 45 - "The Competition Class notation — draft spec"
Cohesion: 0.06
Nodes (30): 10. Findings F1–F15, 11. Findings F16–F21, 12. Findings F22–F23 — the F3F probe, 13. Findings F24–F27 — the NZ probe, 14. Finding F28 — the F3F re-check, 1. Three rules the notation obeys, 2. Shape, 3. Class level (+22 more)

### Community 46 - "TaskRoundRecordingPropertyTests"
Cohesion: 0.11
Nodes (27): GenEntry, GenFlight, Noise, PlacedEntry, Shape, Competition, DateTimeOffset, Entry (+19 more)

### Community 47 - "ReflightRole"
Cohesion: 0.17
Nodes (10): ReflightRole, Entitled, Filler, Original, DateTimeOffset, Fact, ImmutableArray, List (+2 more)

### Community 48 - "Refined plan"
Cohesion: 0.09
Nodes (21): API — `src/Soarscore.Api` (`Commands.cs` / `Queries.cs`, kebab-case), Application commands — `src/Soarscore.Application/Commands/Competitions/`, Application queries — derived in-handler from the Competition aggregate (no new read-model documents), Classification engine — new `src/Soarscore.Domain/Scoring/TeamClassification.cs`, Cross-reference (house rule 2 — done during refinement, 2026-09-02), Decide functions (`Competition.cs`, defect-chain style, own code prefixes), Decisions settled with the owner (2026-09-02), Domain model — all inside the Competition aggregate (+13 more)

### Community 49 - "PhaseDrawn"
Cohesion: 0.10
Nodes (30): PenaltyRecorded, DateTimeOffset, Group, ImmutableArray, Penalty, ReflightRuling, Round, CompetitionEvent (+22 more)

### Community 50 - "When"
Cohesion: 0.16
Nodes (11): When, CompetitionId, Given, Group, HttpClient, HttpResponseMessage, List, ProblemDetails (+3 more)

### Community 51 - "2 SOARING (All Classes)"
Cohesion: 0.07
Nodes (30): 2.1 THERMAL SOARING, 2.2.1 General., 2.2.2 Launch apparatus shall conform to the following specifications:, 2.2 LAUNCHING, 2.3.1 Timing of the flight commences when the parachute/pennant is seen to drop from the, 2.3.2 Timing of the flight shall finish when the sailplane first touches the ground or a ground based, 2.3.3 Models already in the air and being timed at the completion of the round, may complete that, 2.3.4 If the sailplane comes into contact with a person during the flight and before the model (+22 more)

### Community 52 - "B.4 DEFINITIONS OF EXPRESSIONS"
Cohesion: 0.04
Nodes (47): B.1.1 General definition, B.1.2.1 Category F1 - Free Flight, B.1.2.2 Category F2 - Control Line Flight, B.1.2.3 Category F3 - Radio Controlled Flight, B.1.2.4 Category F4 - Scale Model Aircraft, B.1.2.5 Category F5 - Radio Control Electric Powered Aircraft, B.1.2.6 Category F7 - Radio Controlled Aerostats, B.1.2.7 Category F9 - Drone Sports (+39 more)

### Community 53 - "ScoringTeamsSteps"
Cohesion: 0.11
Nodes (19): CompetitorFinalScoreView, Contributes, StandingsSnapshot, CompetitionId, Dictionary, Given, Group, HashSet (+11 more)

### Community 54 - ".LoadCurrentAsync"
Cohesion: 0.26
Nodes (10): CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task, EntryIndexProjection (+2 more)

### Community 55 - "FinaliseDecideTests"
Cohesion: 0.13
Nodes (12): DeclaredResult, Aggregate, CompetitorRef, Placing, Promoted, DateTimeOffset, Fact, ImmutableArray (+4 more)

### Community 56 - "ScoringTeamMembership"
Cohesion: 0.13
Nodes (22): ProtectionGroupMemberAdded, ScoringTeamDefined, ScoringTeamMembershipAssigned, TeamClassificationConfigured, ProtectionGroup, Id, Name, ProtectionGroupMembership (+14 more)

### Community 57 - "Scoring Service Build Plan"
Cohesion: 0.07
Nodes (26): Dependency Graph, Design Rules Every Agent Must Uphold, File Layout, Issue Tracking, Open Issues, Overview, Parallelism Summary, Scoring Service Build Plan (+18 more)

### Community 58 - ".SeedOpenEntry"
Cohesion: 0.18
Nodes (16): CancellationToken, IClock, IEventStore, Task, OpenFlightHandler, CancellationToken, DateTimeOffset, Fact (+8 more)

### Community 59 - ".SeedScoredTeamCompetition"
Cohesion: 0.16
Nodes (17): CancellationToken, IEventStore, ImmutableArray, Task, ScoreTeamStandings, ScoreTeamStandingsHandler, TeamStandingsView, AdoptedRules (+9 more)

### Community 60 - "Work items"
Cohesion: 0.08
Nodes (25): 1. `TaskRoundState.InProgress` stays unreachable — but `TaskRoundReopened` is added, 2. Finalisation is competition-scope only this thread, Before starting — done, Out of scope — deliberately, Plan — Task-round lifecycle: `TaskRoundCompleted` / `TaskRoundReopened` / `TaskRoundAnnulled` / `Finalised`, Risks, The governing principle: the system does not order score capture, Three decisions taken up front (+17 more)

### Community 61 - "MetricDefinition"
Cohesion: 0.04
Nodes (44): MetricDefinition, DeclaredBeforeLaunch, Kind, Name, Precision, Unit, M, ImmutableArray (+36 more)

### Community 62 - ".HandleAsync"
Cohesion: 0.20
Nodes (14): CancellationToken, IClock, IEventStore, Task, BindParameterHandler, AdoptedRules, DateTimeOffset, Fact (+6 more)

### Community 63 - "NumberOrParam"
Cohesion: 0.08
Nodes (27): Bands, JsonConverter, JsonSerializerOptions, ClassDefinitionHashing, JsonSerializerOptions, Utf8JsonReader, Utf8JsonWriter, DecimalAsStringConverter (+19 more)

### Community 64 - "Plan — Catalogue-choice draws: the CD picks each round's task"
Cohesion: 0.08
Nodes (24): Acceptance, Appendix A — the deferred follow-on: per-round parameter bindings, Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — Domain, Phase B — Application (+16 more)

### Community 65 - ".BuildCompetition"
Cohesion: 0.17
Nodes (14): RoundOrdinal, TaskCode, Competitors, DateTimeOffset, Dictionary, Fact, GroupByRound, ImmutableArray (+6 more)

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
Cohesion: 0.07
Nodes (34): FieldOp, IReadOnlyCollection, OpKind, Ops, SpotBase, Op, Gen, Index (+26 more)

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
Nodes (78): ICommand, IHttpMaxRequestBodySizeFeature, IParsable, WebApplication, Commands, IReadOnlyList, WebApplication, Composition (+70 more)

### Community 78 - "PersonRegistered"
Cohesion: 0.06
Nodes (34): PeopleProjection, DateTimeOffset, Defect, Result, ClubAffiliation, ClubName, MembershipNumber, Person (+26 more)

### Community 79 - ".BuildDrawnCompetition"
Cohesion: 0.19
Nodes (7): Competitors, DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, AppendReflightGroupDecideTests

### Community 80 - "TaskRoundCompleted"
Cohesion: 0.11
Nodes (20): minRounds, minTasks, outcomes, RoundOutcome, TaskRoundCompleted, taskRefs, DateTimeOffset, Fact (+12 more)

### Community 81 - ".HandleAsync"
Cohesion: 0.15
Nodes (17): CancellationToken, IClock, IEventStore, Task, AmendMeasurementHandler, Competition, TaskResolver, CancellationToken (+9 more)

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
Cohesion: 0.20
Nodes (12): CancellationToken, IClock, IEventStore, Task, AppendReflightGroupHandler, CancellationToken, Competition, Fact (+4 more)

### Community 93 - ".New"
Cohesion: 0.14
Nodes (10): ArgumentException, Competition, DateTimeOffset, Fact, CompetitionFoldTests, Fact, Fact, InlineData (+2 more)

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
Cohesion: 0.23
Nodes (12): Kept, ReflightRows, SlotCapture, ScoresRow, HttpClient, IReadOnlyDictionary, IReadOnlyList, List (+4 more)

### Community 102 - "NormalisationEnginePropertyTests"
Cohesion: 0.22
Nodes (9): TaskResultState, NoResult, Valid, state, Fact, Gen, IReadOnlyDictionary, raw (+1 more)

### Community 103 - "F3F.1 GENERAL RULES"
Cohesion: 0.11
Nodes (19): F3F.1.10 Safety, F3F.1.11 Judging, F3F.1.12 Scoring, F3F.1.13 Classification, F3F.1.14 Team Classification, F3F.1.15 Organisation of the Contest, F3F.1.16 Changes, F3F.1.17 Weather Conditions and interruptions (+11 more)

### Community 104 - "Dispatcher"
Cohesion: 0.08
Nodes (32): CountLetters, Echo, IServiceProvider, CancellationToken, IServiceProvider, Task, Type, Dispatcher (+24 more)

### Community 105 - "Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition`"
Cohesion: 0.11
Nodes (18): Context, Dependency order, Governing documents, Out of scope (deliberately), Phase A — `Validate()` and ingestion limits, Phase B — `class_library` read model and the write path, Phase C — Api and end-to-end verification, Plan — Class-definition adoption steel thread: `Validate()` and `PublishClassDefinition` (+10 more)

### Community 106 - "Design decisions (settled here, cited from code)"
Cohesion: 0.08
Nodes (23): Before starting — resolved at scoping (2026-08-30), Cross-references checked (housekeeping rule 2), D-A1 — Aggregate-scoped Zero* acts at the task-round stage, through the existing raw-stage engine path, D-A2 — Anchoring: the Zero* record must name the task-round it zeroes, D-A3 — A Zero* record with no `TaskRound` coordinate cannot be anchored: refused at record time, refused loudly at score time, D-A4 — Mixed-effect definitions act in both stages; that is the rule, not a double-count, D-B1 — `ApplyRawPenalties` surfaces a Disqualify flag; the raw stage's return type grows, D-B2 — The flag is flag-only: no score change, OR-accumulated through the walk (+15 more)

### Community 107 - "PublishClassDefinition"
Cohesion: 0.09
Nodes (36): Hash, CancellationToken, IClock, IEventStore, Task, PublishClassDefinition, PublishClassDefinitionHandler, CancellationToken (+28 more)

### Community 108 - "Story — Entry-scoped point-deduction penalties are inert"
Cohesion: 0.10
Nodes (20): Cross-references checked (housekeeping rule 2), D1 — Stage follows recorded scope; effect picks the action within the stage, D2 — Accrual and exclusion-group semantics at the raw stage are identical to the aggregate stage, D3 — Ordering within one entry's penalty set: contribution, suppression, then zeroing dominance, D4 — Floor: a deducted HigherIsBetter raw never goes below zero, D5 — Existing fixtures and seed classes are unaffected byte-for-byte, D6 — Read-side tolerance unchanged, Decision (argued, per "to be argued in-story") (+12 more)

### Community 109 - "RecordCompetitionPenaltyDecideTests"
Cohesion: 0.13
Nodes (15): Competitor, CompetitorNumber, Id, PersonRef, RegisteredAt, WithdrawnAt, DateTimeOffset, Fact (+7 more)

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
Cohesion: 0.22
Nodes (11): Normalisation, Direction, Round, WinnerScore, Rounding, ImmutableDictionary, IReadOnlyDictionary, NormalisationEngine (+3 more)

### Community 123 - "2. Findings"
Cohesion: 0.15
Nodes (12): 1. Why, 2.1 Competition catalogue (public, easy), 2.2 What `eScoringInterface.exe` actually is, 2.3 Server API (recovered by decompiling GliderScore.exe 6.79 U5), 2.4 Download zip contents, 2.5 Caveats learned the hard way, 2. Findings, 3. Fit with the existing fixture pipeline (+4 more)

### Community 124 - ".ScoreCompetition"
Cohesion: 0.20
Nodes (16): DateTimeOffset, ReflightRuling, At, By, CompetitorRef, Reason, Selection, TaskRound (+8 more)

### Community 125 - "PrescribeDrawDecideTests"
Cohesion: 0.26
Nodes (5): DateTimeOffset, Fact, ImmutableArray, IReadOnlyList, PrescribeDrawDecideTests

### Community 126 - "Rule map — topic × class"
Cohesion: 0.12
Nodes (15): Contest shape, Contest shape, Cross-class NZ rules, Drop-worst, Flight points and landing bonus, Launch-height scoring (F5 only), Normalisation and rounding, NZ national classes (NZMAA Section 5: Soaring, March 2024) (+7 more)

### Community 127 - "SECTION A - CIAM INTERNAL REGULATIONS"
Cohesion: 0.06
Nodes (35): A.10.1 Requirements for proposals, A.10.2 Effective date of rule changes, A.10.3 Submission procedure, A.10 SUBMISSION OF PROPOSALS TO THE CIAM, A.11.1 Emergency safety rules, A.11.2 Emergency safety notices, A.11 EMERGENCY SAFETY RULES & NOTICES, A.12 AEROMODELLING FUND (+27 more)

### Community 128 - "Design decisions — settled here, do not relitigate"
Cohesion: 0.08
Nodes (23): As-built (2026-08-29), Before starting, D1 — Exact semantics of the exposed value, D2 — Placement: parallel map on `GroupResult`, not a second field on `TaskResult`, D3 — Population rules inside `NormalisationEngine.Normalise` (both branches), D4 — Fail-loud view mapping, D5 — API surface changes none, D6 — Harness grain-1 flips to HTTP where the authored class permits it (+15 more)

### Community 129 - "FakeEventStore"
Cohesion: 0.27
Nodes (9): IEventStore, CancellationToken, ExpectedVersion, Guid, IReadOnlyList, List, RecordedEvent, Task (+1 more)

### Community 130 - "F3K.11 DEFINITIONS OF TASKS"
Cohesion: 0.13
Nodes (15): F3K.11.10 Task J (Three last flights), F3K.11.11 Task K (Increasing time by 30 seconds, “Big Ladder”), F3K.11.12 Task L (One flight), F3K.11.13 Fly-off Task M (Increasing time by 2 minutes “Huge Ladder”), F3K.11.14 Task N (Best flight), F3K.11.1 Task A (Last flight), F3K.11.2 Task B (Next to last and last flight), F3K.11.3 Task C (All up, last down) (+7 more)

### Community 131 - ".LoadCurrentAsync"
Cohesion: 0.26
Nodes (10): CancellationToken, Guid, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task, CompetitionSummaryProjection (+2 more)

### Community 132 - "Story — Ship on three stores: Fisher/SQLite, Marten/PostgreSQL, Polecat/SQL Server"
Cohesion: 0.13
Nodes (14): Also done, not in the original plan, Before starting, Deliberately not done, One thing deliberately left short of the story's title, Outcome — as built, 2026-08-16, Plan, Property-based testing, Scope of this pass — Fisher/SQLite only (+6 more)

### Community 133 - "WithdrawCompetitorHandler"
Cohesion: 0.22
Nodes (11): CancellationToken, IClock, IEventStore, Task, WithdrawCompetitorHandler, DateTimeOffset, Fact, FakeEventStore (+3 more)

### Community 134 - "IProjection"
Cohesion: 0.11
Nodes (23): IJasperFxProjection, IProjection, CancellationToken, IDocumentOperations, IDocumentSession, IEvent, IReadOnlyList, Task (+15 more)

### Community 135 - "ContactDetails"
Cohesion: 0.13
Nodes (23): IQuery, IQueryHandler, RegisterPerson, CancellationToken, IEventStore, Task, GetClassDefinition, GetClassDefinitionHandler (+15 more)

### Community 136 - "1 GENERAL DEFINITIONS"
Cohesion: 0.14
Nodes (14): 1.1 DEFINITIONS, 1.2 CHARACTERISTICS, 1.3 RADIO CONTROL TRANSMITTER., 1.4.1 Unless otherwise specified in class rules, the competitor may use a maximum of two models, 1.4.2 The competitor must own the model(s) flown but is not required to have built them., 1.4.3 A model may be flown in a contest by only one competitor., 1.4 NUMBER OF MODELS, OWNERSHIP AND OPERATION., 1.5 BALLASTING (+6 more)

### Community 137 - ".AddSoarscoreInfrastructure"
Cohesion: 0.08
Nodes (21): IConfiguration, IServiceCollection, DateTimeOffset, IClock, UtcNow, DocumentStore, MartenConfig, CancellationToken (+13 more)

### Community 138 - ".Classify"
Cohesion: 0.07
Nodes (33): Member, HashSet, ImmutableArray, Result, Candidate, Member, TeamClassificationEngine, TeamClassificationResult (+25 more)

### Community 139 - "ReflightRule"
Cohesion: 0.14
Nodes (13): ReflightRule, EntitledScores, MinNewGroupSize, OthersScore, ReflightSelection, BetterOf, NotPermitted, Replacement (+5 more)

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
Cohesion: 0.24
Nodes (8): Competition, Competitors, DateTimeOffset, Fact, ImmutableArray, InlineData, Theory, RecordReflightRulingDecideTests

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
Cohesion: 0.06
Nodes (37): actual, Actual, Model, FlightModel, MeasurementModel, model, Competition, Fact (+29 more)

### Community 150 - "CLAUDE.md — Soarscore"
Cohesion: 0.17
Nodes (12): CLAUDE.md — Soarscore, Core architectural law: Competition Class model vs. core system, Domain in one screen, graphify, House-keeping rules, Key constraints, Pointers, Project status (+4 more)

### Community 151 - "fai-rule.sh"
Cohesion: 0.39
Nodes (9): cmd_check_links(), cmd_find(), cmd_show(), cmd_toc(), die(), norm_ref(), fai-rule.sh script, volume_file() (+1 more)

### Community 152 - ".SeedCompetition"
Cohesion: 0.19
Nodes (16): CancellationToken, IEventStore, Task, RecordCompetitionPenalty, RecordCompetitionPenaltyHandler, CancellationToken, DateTimeOffset, Fact (+8 more)

### Community 153 - "PrescribeDrawEventStoreTests"
Cohesion: 0.29
Nodes (9): IReadOnlyList, PrescribedGroup, PrescribedRound, CancellationToken, Fact, IReadOnlyList, Task, PrescribeDrawEventStoreTests (+1 more)

### Community 154 - "TaskDefinition"
Cohesion: 0.04
Nodes (52): TaskDefinition, Code, Flights, FlightValidWhen, Group, Metrics, Name, Normalise (+44 more)

### Community 155 - "4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS"
Cohesion: 0.06
Nodes (36): 4 F3J – RADIO CONTROLED THERMAL DURATION GLIDERS, F3J.11.2 Fly-off Working Time, F3J.11.3 Fly-off Scoring, F3J.11.4 Final Placing, F3J.11.5 Ranking for International Team Classification, F3J.11 FINAL CLASSIFICATION, F3J.12.1 for details., F3J.12 WEATHER CONDITIONS AND INTERRUPTIONS (+28 more)

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
Cohesion: 0.12
Nodes (16): Adding a fixture, Differential gate result, GliderScore fixture extraction, How the corpus is consumed, Index contract (rule 5), NZ master caveat and opt-in tolerant mode, Offline-only rule, Output shape contract (+8 more)

### Community 163 - "PublishedClassDefinition"
Cohesion: 0.14
Nodes (12): CancellationToken, IEventStore, Task, ClassDefinitionLoader, DateTimeOffset, Guid, PublishedClassDefinition, ContentHash (+4 more)

### Community 164 - "test_csvparse.py"
Cohesion: 0.10
Nodes (22): assert_record_typed_equal(), _corrupted_documents(), default_line(), document(), _download_records(), composite, given, parametrize (+14 more)

### Community 166 - "mine_catalogue.py"
Cohesion: 0.10
Nodes (31): build_range_postback(), collect_comps(), extract_form_fields(), fetch_catalogue(), find_range_select(), is_comp_id_value(), locate_comp_select(), main() (+23 more)

### Community 167 - "NZ Class M — ALES 200 (Altitude Limited Electric Soaring)"
Cohesion: 0.18
Nodes (11): 1. Pilot assignment to groups (the draw), 2. Launch (`NZ.3.12.1`), 3. Data the timer / helper collects, 4. The task (`NZ.3.12.1 f, g, m, n`), 5. Group score (`NZ.3.12.3`), 6. Round and final score, 7. Re-flights (`NZ.3.12.5 l`), 8. NDC format (`NZ.3.12.7`) — a different scoring pipeline (+3 more)

### Community 168 - "Enumerations.cs"
Cohesion: 0.08
Nodes (25): CompositionKind, ChooseFromCatalogue, FixedSequence, DropDimension, ByRound, ByTask, NormalisationDirection, HigherIsBetter (+17 more)

### Community 169 - "3.17 CLASS R: E-RES 2M (Electric Rudder Elevator Spoiler 2M Glider)"
Cohesion: 0.18
Nodes (11): 3.17.0 Contents:, 3.17.1 Introduction, 3.17.2 Model Specifications, 3.17.3 Competition Terrain, 3.17.4 Cancellation, 3.17.5 Competition Flights, 3.17.6 Launching, 3.17.7 Landing (+3 more)

### Community 170 - "BindParameterPropertyTests"
Cohesion: 0.30
Nodes (4): DateTimeOffset, Fact, Ref, BindParameterPropertyTests

### Community 171 - "DrawAcceptancePropertyTests"
Cohesion: 0.12
Nodes (16): DrawOp, DateTimeOffset, Gen, List, DrawAcceptancePropertyTests, DrawOp, Accept, Register (+8 more)

### Community 172 - "Soarscore — Users"
Cohesion: 0.18
Nodes (10): 1. Organiser, 2. Contest Director, 3. Scorer, 5. Pilot / Competitor, Direct users, Indirect users, Multiple "Hats" Rule, Purpose (+2 more)

### Community 173 - "Drop-worst"
Cohesion: 0.18
Nodes (11): 1. Configuration source (Comps table), 2. `DropScoreOption` decode and gating, 3. Staged activation — how many drops at R rounds flown, 4. Selection basis (task-driven vs round-driven), 5. Tie-breaking among equal drop candidates — deterministic, 6. Marking and exclusion, 7. Re-flights, 8. F3K-gated variant (`f3kRecord`) (+3 more)

### Community 174 - "ClassDefinitionSummary"
Cohesion: 0.20
Nodes (12): DateTimeOffset, Guid, ClassDefinitionSummary, CancellationToken, IReadOnlyList, Task, FindClassDefinitions, FindClassDefinitionsHandler (+4 more)

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

### Community 180 - "Annulment"
Cohesion: 0.50
Nodes (4): Annulment, At, By, Reason

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

### Community 189 - "test_fetch_comp.py"
Cohesion: 0.19
Nodes (34): boolean_token_csv_bytes(), check_urls(), csv_member_name(), duration_csv_bytes(), fetch(), fixture_csv_bytes(), make_client(), make_zip_bytes() (+26 more)

### Community 190 - ".RunPayoffCaptureScenarioAsync"
Cohesion: 0.46
Nodes (3): Fact, Task, EntryCaptureEventStoreTests

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
Cohesion: 0.13
Nodes (12): classify_action(), GsClient, Exception, Read-only, rate-limited, auditable client for gliderscore.com., True iff action is exactly a read-only allowlisted ACTION (case-sensitive)., Raised for any attempt outside the read-only allowlist., Wraps OS/HTTP-level transport failures (never an allowlist refusal)., Default transport: one urllib.request round trip per request dict. (+4 more)

### Community 196 - "ScoreTerm"
Cohesion: 0.14
Nodes (19): FlightSelection, ScoreTerm, ImmutableArray, IReadOnlyDictionary, FlightSelector, IReadOnlyDictionary, FlightResult, FlightResultState (+11 more)

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
Cohesion: 0.18
Nodes (10): Annulments and penalties, Competition class model, Decisions that have since been taken up, Deferred decisions, Draw, Event store, GliderScore replay harness, Score capture and corrections (+2 more)

### Community 210 - ".LoadAsync"
Cohesion: 0.19
Nodes (10): CancellationToken, Entry, IEventStore, Task, Version, EntryLoader, DateTimeOffset, Fact (+2 more)

### Community 211 - "Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC"
Cohesion: 0.20
Nodes (9): Before starting / cross-references (house rule 2), Completion note (2026-09-04), Interpretations made (no ruling requested; Pete may veto any), Plan, Related finding (out of scope here, filed in tech-debt), Rulebook defects found (left as written; NZMAA's to fix), Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC, What (+1 more)

### Community 212 - "ScoringCorpusPropertyTests"
Cohesion: 0.25
Nodes (7): DateTimeOffset, Fact, ImmutableArray, ImmutableDictionary, Name, Value, ScoringCorpusPropertyTests

### Community 213 - "Compliance check"
Cohesion: 0.25
Nodes (7): 1. Scope the check, 2. Pull the rules, 3. Verify numbers against source, 4. Check it against the architectural law, 5. Check it against the rest of the corpus, 6. Report, Compliance check

### Community 214 - "f5j-hawkes-bay-trials/ladder.py"
Cohesion: 0.42
Nodes (8): Decimal, cell_key(), dec(), die(), gs_round(), load(), main(), GS report-time re-round: RoundNumber = Int(Nbr + 0.5*10^-d), half-up.

### Community 215 - "test_mine_catalogue.py"
Cohesion: 0.15
Nodes (17): build_page(), fake_sleep(), FakeClock, FakeTransport, make_harness(), option(), given, settings (+9 more)

### Community 216 - "RC Soaring Competitions — Domain Class Diagram"
Cohesion: 0.25
Nodes (6): 1. The competition spine, 2. Competition Class — structure, 3. Competition Class — the scoring vocabulary, 4. Scoring, Modelling notes, RC Soaring Competitions — Domain Class Diagram

### Community 217 - "GroupId"
Cohesion: 0.23
Nodes (4): IFormatProvider, GroupId, Fact, IdRoundTripPropertyTests

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
Cohesion: 0.21
Nodes (8): Id, Competition, DateTimeOffset, Dictionary, Entries, Entry, ImmutableArray, FinaliseCompetitionPropertyTests

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
Cohesion: 0.13
Nodes (16): BigSecond, MaxPairwise, MaxRoundViolations, PairCount, DateTimeOffset, Dictionary, Fact, Field (+8 more)

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
Cohesion: 0.38
Nodes (3): Fact, Result, TeamClassificationEngineTests

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

### Community 243 - "CompetitionResult"
Cohesion: 0.12
Nodes (22): Memberships, Random, Row, Scenario, ImmutableArray, TieBreakDirective, CompetitionResult, PendingTieBreaks (+14 more)

### Community 244 - "RegisterPersonHandler"
Cohesion: 0.09
Nodes (28): IClassFixture, Round2GroupRef, CancellationToken, IClock, IEventStore, Task, RecordReflightRulingHandler, CancellationToken (+20 more)

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

### Community 252 - ".BuildCompetition"
Cohesion: 0.18
Nodes (12): Other, Subject, Competitors, DateTimeOffset, Dictionary, Entries, Fact, ImmutableArray (+4 more)

### Community 253 - "Story — Permitted scopes on PenaltyDefinition (adoption-time scope hardening)"
Cohesion: 0.11
Nodes (17): Before starting, D-1 — Field shape: `PenaltyScope[]?`, exactly as approved, D-2 — Check placement and precedence: scope refusal outranks payload completeness, D-3 — Adoption check 20 rejects only the empty list; no effect×scope cross-check, D-4 — Read path untouched; engine, views, handlers: zero edits, D-5 — Seeds and fixtures untouched, Design decisions (settled here, cited from code), Out of scope (+9 more)

### Community 254 - "PhaseDefinition"
Cohesion: 0.07
Nodes (28): closure, ClosureKind, ImmutableArray, PhaseDefinition, Drops, Ordinal, Promotion, Rounds (+20 more)

### Community 255 - "AssigningSpotsSteps"
Cohesion: 0.14
Nodes (12): Spots, HttpClient, HttpResponseMessage, IReadOnlyList, Task, AssigningSpotsSteps, Client, CompetitionId (+4 more)

### Community 256 - "Story — Ranking's secondary key: RawScore tie-break"
Cohesion: 0.13
Nodes (14): As built (2026-08-29), Decisions (settled during planning 2026-08-28; D1 gets Pete's sign-off at WI-0), Engine design (the entire `src/` change), Known traps (pre-answered — do not reopen inside this story), Out of scope (restated for sign-off), Story invariant for sign-off, Story — Ranking's secondary key: RawScore tie-break, What (+6 more)

### Community 257 - "TeamsEventJsonTests"
Cohesion: 0.38
Nodes (3): DateTimeOffset, Fact, TeamsEventJsonTests

### Community 258 - "Story — GliderScore webmine tool (read-only online comp acquisition)"
Cohesion: 0.20
Nodes (9): As built (2026-08-27), Before starting, Confidentiality, Open questions carried forward, Plan, Story — GliderScore webmine tool (read-only online comp acquisition), Validation of the mining approach (source cross-reference, 2026-08-26), What (+1 more)

### Community 259 - ".Apply"
Cohesion: 0.33
Nodes (4): ClassDefinitionProjection, ArgumentException, Fact, ClassDefinitionProjectionTests

### Community 260 - "RegisterCompetitorHandler"
Cohesion: 0.16
Nodes (16): CancellationToken, IClock, IEventStore, Task, RegisterCompetitorHandler, DateTimeOffset, Fact, FakeEventStore (+8 more)

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

### Community 269 - "FindEntries"
Cohesion: 0.42
Nodes (8): CancellationToken, IReadOnlyList, Task, FindEntries, FindEntriesHandler, Fact, Task, FindEntriesHandlerTests

### Community 270 - "Story — Coverage: normalisation is per group, not per round"
Cohesion: 0.33
Nodes (5): As built — notes, Deferred, Story — Coverage: normalisation is per group, not per round, What, Why it mattered

### Community 271 - "Stop storing `Entry.WorkingTime`"
Cohesion: 0.33
Nodes (6): Before starting, Decisions — user-confirmed 2026-08-21, Stop storing `Entry.WorkingTime`, What, What a removal must not break, Why it matters

### Community 272 - "IDomainEvent"
Cohesion: 0.14
Nodes (19): IDomainEvent, CancellationToken, Guid, IReadOnlyDictionary, IReadOnlyList, List, Task, FakeEventStore (+11 more)

### Community 273 - ".ComputeGroupViews"
Cohesion: 0.33
Nodes (9): Competition, Entry, ImmutableArray, IReadOnlyDictionary, EntryGapsView, FlightGapsView, GroupRecordingView, GroupSpotView (+1 more)

### Community 274 - ".SetUpAsync"
Cohesion: 0.28
Nodes (11): CancellationToken, IEventStore, Task, GetTaskRoundRecording, GetTaskRoundRecordingHandler, TaskRoundRecordingView, Fact, Group (+3 more)

### Community 275 - "OpKind"
Cohesion: 0.17
Nodes (12): OpKind, AnnulRound, EmptyList, InvalidSpot, MissingMember, RepeatedCompetitor, RepeatedSpot, UnknownCompetitor (+4 more)

### Community 276 - "AdoptedRules"
Cohesion: 0.09
Nodes (24): CancellationToken, IClock, IEventStore, Task, AssignGroupSpotsHandler, AdoptedRules, AdoptedAt, Definition (+16 more)

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

### Community 284 - "FakeEventStore"
Cohesion: 0.15
Nodes (17): DateTimeOffset, Fact, FakeEventStore, Gen, ImmutableArray, DrawPhasePropertyTests, CancellationToken, DateOnly (+9 more)

### Community 285 - "FakeClassLibraryQuery"
Cohesion: 0.44
Nodes (5): Fact, IDispatcher, Task, FindClassDefinitionsTests, FakeClassLibraryQuery

### Community 286 - ".PostCommandRawAsync"
Cohesion: 0.33
Nodes (6): HttpClient, HttpResponseMessage, JsonSerializerOptions, Task, ApiClient, Options

### Community 287 - ".Rank"
Cohesion: 0.08
Nodes (40): Cells, Disq, IList, Position, Entry, PromotionRule, CarryPenalties, Kind (+32 more)

### Community 288 - "Story — Resolve GliderScore scoring arithmetic from source"
Cohesion: 0.40
Nodes (4): Before starting, Story — Resolve GliderScore scoring arithmetic from source, What, Why it matters

### Community 289 - "Findings"
Cohesion: 0.40
Nodes (5): Divergences from FAI/NZ rules, Findings, Formula narrative (consolidated), Handoff notes, Reconciliation result

### Community 290 - "RecordEntryPenaltyDecideTests"
Cohesion: 0.14
Nodes (17): Penalty, By, CompetitorRef, InfractionType, Scope, TaskRound, PenaltyScope, Competition (+9 more)

### Community 291 - ".Apply"
Cohesion: 0.40
Nodes (4): EntryProjection, DateTimeOffset, Fact, EntryProjectionTests

### Community 292 - "Story — NZ F3K NDC seed class"
Cohesion: 0.22
Nodes (8): Before starting (residual items for the builder), Build plan (WI-1 .. WI-4), Completion note (2026-08-30), Rulebook findings (verified against the corpus this session), Rulings (2026-08-30 — Pete; these supersede the story's earlier, Story — NZ F3K NDC seed class, What, Why it matters

### Community 293 - "CompetitorId"
Cohesion: 0.25
Nodes (10): Guid, CompetitorId, Dictionary, Fact, Field, Gen, IEnumerable, ImmutableArray (+2 more)

### Community 294 - "f5j-nz-south-island/ladder.py"
Cohesion: 0.27
Nodes (11): build_notes(), decode_packed_mmss(), frac(), half_up(), height_penalty(), load(), main(), problems() (+3 more)

### Community 295 - "extract.py"
Cohesion: 0.24
Nodes (14): encode(), extract_table(), _install_tolerant_parser_patch(), load_recovered_texts(), main(), merge_recovered_texts(), Loudly warn (once per affected table.column) about degraded reads., Load and structurally verify the byte-validated Comps recovery JSON. (+6 more)

### Community 296 - "GliderScore fixture corpus index"
Cohesion: 0.40
Nodes (4): Competitions, Diversity wanted, GliderScore fixture corpus index, Standing skip reasons

### Community 297 - "SeedF3J"
Cohesion: 0.22
Nodes (7): ImmutableArray, SeedF3J, Definition, FlightMetrics, FlyoffTaskD, LandingRows, TaskD

### Community 298 - "Refined plan"
Cohesion: 0.14
Nodes (13): Before starting, Handoff notes (read this before any sub-agent task), Hunt log (WI-2A — 2026-09-03), Move 1 — f3j-international GS team-ladder oracle + comparison, Move 2 — Grow the corpus with team-bearing comps, Refined plan, Story — Team-parity fixtures: validate team results against GliderScore, What (+5 more)

### Community 299 - "Seed classes — the authoring source"
Cohesion: 0.40
Nodes (4): How the notation maps, Running it, Seed classes — the authoring source, Status of the transcription

### Community 300 - "LookupRow"
Cohesion: 0.10
Nodes (18): Builder, Rows, ConstantTerm, Value, LookupRow, LookupTerm, MetricRef, Rows (+10 more)

### Community 301 - ".SeedEntryUnderF5K"
Cohesion: 0.18
Nodes (16): CancellationToken, IEventStore, Task, RecordEntryPenalty, RecordEntryPenaltyHandler, CancellationToken, DateTimeOffset, Fact (+8 more)

### Community 302 - "C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY"
Cohesion: 0.50
Nodes (4): C.10.1 Class F - Model aircraft, C.10.2 Class S - Space models, C.10.3 General requirements, C.10 NUMBER OF MODELS ELIGIBLE FOR ENTRY

### Community 303 - "C.15.6 Classification"
Cohesion: 0.50
Nodes (4): C.15.6.1 Individual classification, C.15.6.2 National team classification, C.15.6.3 Overall classification in multiple contest categories, C.15.6 Classification

### Community 304 - "ScoringTeamId"
Cohesion: 0.32
Nodes (3): Guid, IFormatProvider, ScoringTeamId

### Community 305 - "AnnulEntryDecideTests"
Cohesion: 0.35
Nodes (5): DateTimeOffset, Fact, Gen, ImmutableArray, AnnulEntryDecideTests

### Community 306 - "F3J.10 SCORING"
Cohesion: 0.17
Nodes (12): F3J.10.10 Group Winner, F3J.10.11 Corrected Score, F3J.10.1 Flight Timing, F3J.10.2 Flight Time Recording, F3J.10.3 Overflying of the Working Time, F3J.10.4 Long Overflying, F3J.10.5 Landing Evaluation, F3J.10.6 Landing distance Measuring (+4 more)

### Community 307 - ".SearchAsync"
Cohesion: 0.25
Nodes (6): CancellationToken, DateOnly, IDocumentSessionFactory, IReadOnlyList, Task, DocumentCompetitionsQuery

### Community 308 - "5.5.11.1 General Rules"
Cohesion: 0.50
Nodes (4): 5.5.11.1.1 Definition of a Radio Controlled Glider with Electric Motor, 5.5.11.1.2 Prefabrication of the Model Aircraft, 5.5.11.1.3 Characteristics of Radio Controlled Gliders with electric motor and altimeter/motor run, 5.5.11.1 General Rules

### Community 309 - "opencode.json"
Cohesion: 0.50
Nodes (3): plugin, $schema, .opencode/plugins/graphify.js

### Community 310 - "ProtectedPair"
Cohesion: 0.14
Nodes (21): CancellationToken, Competition, IEventStore, ImmutableArray, Task, DrawProtectionDiagnosticsView, DrawProtectionViolationView, GetDrawProtectionDiagnostics (+13 more)

### Community 311 - "Soarscore.Application"
Cohesion: 0.03
Nodes (71): Soarscore.Infrastructure.Competitions, Soarscore.Infrastructure.Tests, Soarscore.Infrastructure.CompetitionClasses, Soarscore.Infrastructure.People, Soarscore.Application.Queries.People, Soarscore.Api.Commands, Soarscore.Infrastructure, Soarscore.Application (+63 more)

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

### Community 319 - ".BuildDispatcher"
Cohesion: 0.49
Nodes (6): Fact, FakeClock, FakeEventStore, IDispatcher, Task, PublishClassDefinitionTests

### Community 320 - ".ScoreGroup"
Cohesion: 0.20
Nodes (10): ImmutableDictionary, GroupResult, ImmutableArray, ImmutableDictionary, IReadOnlyDictionary, Penalty, Result, TaskRoundCoordinate (+2 more)

### Community 321 - "F5 Electric Soaring — Generally Applicable Rules"
Cohesion: 0.29
Nodes (7): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score, 4. Round & final score, 5. Re-flights, F5 Electric Soaring — Generally Applicable Rules, Source references

### Community 323 - "Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness)"
Cohesion: 0.40
Nodes (4): Before starting, Story — Source an FAI-conformant F3K fixture (seed-definition parallel-run witness), What, Why it matters

### Community 324 - ".ScoreWith"
Cohesion: 0.25
Nodes (8): DateTimeOffset, Fact, Func, Group, ImmutableArray, NormalisationGroupIsolationPropertyTests, World, World

### Community 325 - "A.6 BUREAU"
Cohesion: 0.29
Nodes (7): A.6.1 Composition, A.6.2 Term of office and election, A.6.3 Resignation, A.6.4 Suspension and Dismissal, A.6.5 Competence, A.6.6 Bureau Meeting Minutes, A.6 BUREAU

### Community 326 - "IEntryQuery"
Cohesion: 0.12
Nodes (14): CancellationToken, IEventStore, IReadOnlyDictionary, Task, EntryCollector, CancellationToken, IReadOnlyList, Task (+6 more)

### Community 327 - "TaskRound"
Cohesion: 0.11
Nodes (15): Group, Func, Round, IsCompleteOrAnnulled, IsFullyFlown, Ordinal, TaskRounds, TaskRound (+7 more)

### Community 328 - "F3B — RC Multi-Task Gliders"
Cohesion: 0.22
Nodes (9): 1. Pilot assignment to groups (the draw), 2. Data the timer / helper collects, 3. Group score — three partial scores (`F3B.2.6`), 4. Round score (`F3B.2.7`), 5. Final classification (`F3B.2.8`), 6. Re-flights (`F3B.1.5`, `F3B.1.11`), F3B — RC Multi-Task Gliders, Penalty schedule (+1 more)

### Community 330 - "RegisterCompetitorPropertyTests"
Cohesion: 0.33
Nodes (5): IsBogus, DateTimeOffset, Gen, Index, RegisterCompetitorPropertyTests

### Community 331 - ".SeedOpenEntryWithFlight"
Cohesion: 0.17
Nodes (15): CancellationToken, IClock, IEventStore, Task, CaptureMeasurementHandler, CancellationToken, DateTimeOffset, Fact (+7 more)

### Community 332 - "Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus)"
Cohesion: 0.29
Nodes (6): Blast-radius audit (all 12 `T.Piecewise` call sites), Completion note (2026-09-04), Plan, Story — Signed-width piecewise bands (the FAI F5K below-NLH bonus), What, Why it matters

### Community 333 - ".MapQueries"
Cohesion: 0.10
Nodes (24): IReadOnlyList, WebApplication, Queries, CancellationToken, Entry, IEventStore, ImmutableArray, IReadOnlyDictionary (+16 more)

### Community 334 - "f3k-southern-fling/ladder.py"
Cohesion: 0.36
Nodes (8): dec(), main(), Emulate GS's RoundNumber(v, d=1) = Int(Nbr + 0.5*10^-d) floor semantics through…, Exact-decimal half-up round to 1 dp., require(), round_gs_binary64_emulation(), round_half_up_decimal(), stop()

### Community 335 - "Story — webmine agent-skill wrapper"
Cohesion: 0.40
Nodes (4): Before starting, Story — webmine agent-skill wrapper, What, Why it matters

### Community 336 - "F3J.8 LAUNCHING"
Cohesion: 0.25
Nodes (8): F3J.8.1 Start Direction, F3J.8.2 Launching, F3J.8.3 Launching Procedure, F3J.8.4 Launching Area, F3J.8.5 Launching Device, F3J.8.6 Early Start, F3J.8.7 Towlines, F3J.8 LAUNCHING

### Community 337 - "Story — OmitFromTeamScore=true witness fixture"
Cohesion: 0.40
Nodes (4): Before starting, Story — OmitFromTeamScore=true witness fixture, What, Why it matters

### Community 338 - "Mutation"
Cohesion: 0.29
Nodes (7): Mutation, DeleteMember, DuplicateMember, MoveBetweenGroups, SplitOffSingleton, SubstituteUnregistered, WithdrawAMember

### Community 339 - "ComparisonReport"
Cohesion: 0.15
Nodes (12): Func, IEnumerable, IReadOnlyList, Placing, Ref, Score, ComparisonReport, AllGrainsExact (+4 more)

### Community 341 - "NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)"
Cohesion: 0.18
Nodes (11): 1. Pilot assignment to groups (the draw) — **and the open problem**, 2. Launch (`NZ.3.15.1 d, f, g`), 3. Data the timer / helper collects, 4. The task (`NZ.3.15.1 c`), 5. Score (`NZ.3.15.1 i`), 6. Rounds, 7. Re-flights (`NZ.3.15.1 h`), 8. A defect in the rule text — `NZ.3.15.1 j` (+3 more)

### Community 342 - "ClassDefinitionPublished"
Cohesion: 0.23
Nodes (9): IDomainEvent, DateTimeOffset, ClassDefinitionEvent, ClassDefinitionPublished, ClassDefinitionRetired, Fact, ClassDefinitionEventJsonTests, Fact (+1 more)

### Community 343 - "DocumentClassLibraryQuery"
Cohesion: 0.32
Nodes (5): CancellationToken, IDocumentSessionFactory, IReadOnlyList, Task, DocumentClassLibraryQuery

### Community 344 - "NZ ALES — Generally Applicable Rules"
Cohesion: 0.33
Nodes (6): 1. The shared shape, 2. Data the timer / helper collects, 3. What is *not* scoring data, 4. Re-flights, NZ ALES — Generally Applicable Rules, Source references

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

### Community 350 - ".TaskWithParameterisedWorkingTime"
Cohesion: 0.53
Nodes (3): Fact, ImmutableArray, ResolvedWorkingTimeTests

### Community 351 - "_documented_row"
Cohesion: 0.40
Nodes (5): _documented_row(), _picker_scenarios(), composite, One picker row built from documented parts, plus the parts themselves., Unique-value option lists, some duplicated, shuffled into a document order.

### Community 352 - "CreateCompetitionPropertyTests"
Cohesion: 0.33
Nodes (6): DurationDays, OffsetDays, Location, Gen, Name, CreateCompetitionPropertyTests

### Community 353 - "F3K.2 DEFINITION OF MODEL GLIDER"
Cohesion: 0.29
Nodes (7): F3K.2.1 Specifications, F3K.2.2 Losing a part of the model glider, F3K.2.3 Change of model glider, F3K.2.4 Retrieving of model glider, F3K.2.5 Radio frequencies, F3K.2.6 Ballast, F3K.2 DEFINITION OF MODEL GLIDER

### Community 354 - "F3K.9 DEFINITION OF A ROUND"
Cohesion: 0.29
Nodes (7): F3K.9.1 Groups and round scores, F3K.9.2 Working time, F3K.9.3 Landing window, F3K.9.4 Preparation time, F3K.9.5 Flight testing time, F3K.9.6 Re-Flights, F3K.9 DEFINITION OF A ROUND

### Community 355 - "People/TestDoubles.cs"
Cohesion: 0.14
Nodes (10): Soarscore.Application.Tests.Shared.People, Soarscore.Application.Tests.Queries.People, Soarscore.Application.Tests.Commands.People, IClock, DateTimeOffset, FakeClock, UtcNow, DateTimeOffset (+2 more)

### Community 356 - "Story — Operational tie-break resolution: record the outcome, re-rank"
Cohesion: 0.40
Nodes (4): Before starting, Story — Operational tie-break resolution: record the outcome, re-rank, What, Why it matters

### Community 357 - "NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)"
Cohesion: 0.20
Nodes (10): 1. Pilot assignment to groups (the draw), 2. Launch (`NZ.3.13.1 d, f, g`), 3. Data the timer / helper collects, 4. The task (`NZ.3.13.1 c`), 5. Score (`NZ.3.13.1 i`), 6. Rounds, 7. Re-flights (`NZ.3.13.1 h`), 8. What is not stated (+2 more)

### Community 359 - "PenaltyEnginePropertyTests"
Cohesion: 0.24
Nodes (7): PenaltyAccrual, OncePerAttempt, PerOccurrence, Fact, Gen, ImmutableArray, PenaltyEnginePropertyTests

### Community 361 - "Result"
Cohesion: 0.06
Nodes (32): CancellationToken, Task, CancellationToken, Task, CancellationToken, Task, CancellationToken, Task (+24 more)

### Community 362 - "Soarscore.SeedData"
Cohesion: 0.08
Nodes (6): Soarscore.Application.Tests.Shared.Competitions, Soarscore.SeedData, Soarscore.Application.Tests.Commands.Competitions, JsonSerializerContext, Intrinsic, ClassDefinitionContext

### Community 363 - "2.5 CONTESTS"
Cohesion: 0.67
Nodes (3): 2.5.1 Contestants Meeting., 2.5.2 Round Identification., 2.5 CONTESTS

### Community 368 - ".Publishing_then_optionally_retiring_preserves_identity_fields_and_models_RetiredAt_correctly"
Cohesion: 0.40
Nodes (4): DateTimeOffset, Fact, Gen, ClassDefinitionProjectionPropertyTests

### Community 371 - "ExpectedVersion"
Cohesion: 0.17
Nodes (12): ExpectedVersion, Any, IsAny, IsExact, IsNoStream, NoStream, Version, Kind (+4 more)

### Community 373 - "F3K.10 SCORING"
Cohesion: 0.40
Nodes (5): F3K.10.1 Final score, F3K.10.2 Resolution of a tie, F3K.10.3 Fly-off, F3K.10.4 Team Classification, F3K.10 SCORING

### Community 374 - "F3K.4 SAFETY"
Cohesion: 0.40
Nodes (5): F3K.4.1 Contact with a person, F3K.4.2 Mid air collision, F3K.4.3 Safety area, F3K.4.4 Forbidden airspace, F3K.4 SAFETY

### Community 377 - "RulesAmendment"
Cohesion: 0.40
Nodes (5): RulesAmendment, At, By, Definition, Reason

### Community 382 - "F3K.1 GENERAL"
Cohesion: 0.50
Nodes (4): F3K.1.1 Timekeepers, F3K.1.2 Helper, F3K.1.3 Transmitter Pound, F3K.1 GENERAL

### Community 385 - "RateTerm"
Cohesion: 0.22
Nodes (11): CapScope, PerFlight, PerTask, RateTerm, Cap, CapScope, Rate, IReadOnlyDictionary (+3 more)

## Knowledge Gaps
- **2244 isolated node(s):** `context7`, `rider`, `$schema`, `.opencode/plugins/graphify.js`, `$(SoarscoreTargetFramework)` (+2239 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ClassDefinition` connect `ClassDefinition` to `FakeEntryQuery`, `ReflightingForAMissedRoundSteps`, `SystemClock`, `CatalogueDrawPropertyTests`, `.CheckLimits`, `ScoringTeamCommandHandlerTests`, `.New`, `ResolvedTask`, `EntryId`, `IStoreFixture`, `.All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state`, `IDispatcher`, `Corpus.cs`, `MeasuredValue`, `ClosingACompetitionSteps`, `.New`, `GliderscoreFixture`, `FlightOpened`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `Then`, `.Of`, `PenaltyDefinition`, `ReflightRole`, `PhaseDrawn`, `When`, `ScoringTeamsSteps`, `FinaliseDecideTests`, `ScoringTeamMembership`, `.SeedOpenEntry`, `MetricDefinition`, `.HandleAsync`, `.BuildCompetition`, `CompetitionReplaceTaskRoundPropertyTests`, `GroupSpotsPropertyTests`, `.CompetitionAdopting`, `CompetitionId`, `.BuildDrawnCompetition`, `.HandleAsync`, `ScoringServicePropertyTests`, `.Validate`, `.DrawnCompetitionAsync`, `.New`, `DrawAcceptanceDecideTests`, `PublishClassDefinition`, `RecordCompetitionPenaltyDecideTests`, `.ScoreCompetition`, `PrescribeDrawDecideTests`, `WithdrawCompetitorHandler`, `ContactDetails`, `ReflightRule`, `SeedF3K`, `.BuildDrawnCompetition`, `EntryModelBasedFoldTests`, `.SeedCompetition`, `PrescribeDrawEventStoreTests`, `TaskDefinition`, `PrescribeDrawPropertyTests`, `PublishedClassDefinition`, `BindParameterPropertyTests`, `DrawAcceptancePropertyTests`, `.RunPayoffCaptureScenarioAsync`, `ClassDefinitionValidationPropertyTests`, `.Apply`, `.Seed`, `SeeingWhatIsRecordedSteps`, `RegisterPersonHandler`, `SeedF5kNdc`, `.BuildCompetition`, `PhaseDefinition`, `.Apply`, `RegisterCompetitorHandler`, `.SetUpAsync`, `AdoptedRules`, `FakeEventStore`, `.Rank`, `SeedF3J`, `LookupRow`, `.SeedEntryUnderF5K`, `.BuildDispatcher`, `.ScoreGroup`, `RegisterCompetitorPropertyTests`, `.SeedOpenEntryWithFlight`, `.MapQueries`, `ClassDefinitionPublished`, `CreateCompetitionPropertyTests`, `.Publishing_then_optionally_retiring_preserves_identity_fields_and_models_RetiredAt_correctly`, `RulesAmendment`?**
  _High betweenness centrality (0.133) - this node is a cross-community bridge._
- **Why does `CompetitorId` connect `CompetitorId` to `Competition`, `Soarscore.Domain.PublishedClassDefinition`, `FakeEntryQuery`, `ReflightingForAMissedRoundSteps`, `SystemClock`, `CatalogueDrawPropertyTests`, `ScoringTeamCommandHandlerTests`, `PrescribingADrawSteps`, `DropPolicy`, `.New`, `.SeedWired`, `EntryId`, `IStoreFixture`, `.All_seven_team_events_round_trip_through_the_real_store_and_replay_to_the_expected_state`, `IDispatcher`, `EntryCapturePropertyTests`, `ClosingACompetitionSteps`, `.New`, `Entry`, `Comparator`, `FlightOpened`, `ReflightingAGroupSteps`, `AcceptingTheDrawSteps`, `Then`, `AmendMeasurementDecideTests`, `.Exact`, `.Of`, `TaskRoundRecordingPropertyTests`, `ReflightRole`, `PhaseDrawn`, `When`, `ScoringTeamsSteps`, `FinaliseDecideTests`, `ScoringTeamMembership`, `.SeedScoredTeamCompetition`, `.BuildCompetition`, `GroupSpotsPropertyTests`, `TeamsDecideTests`, `CompetitionId`, `.BuildDrawnCompetition`, `.ComputeEntries`, `ScoringServicePropertyTests`, `.DrawnCompetitionAsync`, `DrawAcceptanceDecideTests`, `ReplayDriver`, `PublishClassDefinition`, `RecordCompetitionPenaltyDecideTests`, `.ScoreCompetition`, `PrescribeDrawDecideTests`, `WithdrawCompetitorHandler`, `ContactDetails`, `.Classify`, `ReflightRule`, `.BuildDrawnCompetition`, `.SeedCompetition`, `PrescribeDrawEventStoreTests`, `PrescribeDrawPropertyTests`, `OpenFlightDecideTests`, `.RunPayoffCaptureScenarioAsync`, `GroupId`, `DrawProtectionPropertyTests`, `SeeingWhatIsRecordedSteps`, `TeamClassificationEngineTests`, `CompetitionResult`, `RegisterPersonHandler`, `.BuildCompetition`, `AssigningSpotsSteps`, `RegisterCompetitorHandler`, `.BuildDrawnCompetition`, `FindEntries`, `IDomainEvent`, `.ComputeGroupViews`, `.SetUpAsync`, `AdoptedRules`, `.BuildGroups`, `FakeEventStore`, `RecordEntryPenaltyDecideTests`, `AnnulEntryDecideTests`, `ProtectedPair`, `.ScoreWith`, `IEntryQuery`, `.MapQueries`, `ComparisonReport`, `EntryEventJsonTests`?**
  _High betweenness centrality (0.077) - this node is a cross-community bridge._
- **Why does `CompetitionId` connect `CompetitionId` to `Competition`, `Soarscore.Domain.PublishedClassDefinition`, `FakeEntryQuery`, `.LoadCurrentAsync`, `SystemClock`, `RegisterCompetitorHandler`, `WithdrawCompetitorHandler`, `ScoringTeamCommandHandlerTests`, `FindEntries`, `.New`, `.SeedWired`, `IDomainEvent`, `IStoreFixture`, `.SetUpAsync`, `AdoptedRules`, `IDispatcher`, `.SeedCompetition`, `PrescribeDrawEventStoreTests`, `EntryCapturePropertyTests`, `FakeEventStore`, `Entry`, `Comparator`, `FlightOpened`, `RecordEntryPenaltyDecideTests`, `CompetitorId`, `OpenFlightDecideTests`, `AmendMeasurementDecideTests`, `.Exact`, `.Of`, `AnnulEntryDecideTests`, `ProtectedPair`, `.SeedOpenEntry`, `.SeedScoredTeamCompetition`, `.HandleAsync`, `.RunPayoffCaptureScenarioAsync`, `IEntryQuery`, `CompetitionSummary`, `RegisterCompetitorPropertyTests`, `.SeedOpenEntryWithFlight`, `.MapQueries`, `.HandleAsync`, `GroupId`, `.DrawnCompetitionAsync`, `.New`, `EntryEventJsonTests`, `.Seed`, `ReplayDriver`, `PublishClassDefinition`, `RegisterPersonHandler`?**
  _High betweenness centrality (0.043) - this node is a cross-community bridge._
- **What connects `context7`, `rider`, `$schema` to the rest of the system?**
  _2244 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Competition` be split into smaller, more focused modules?**
  _Cohesion score 0.04537114261884904 - nodes in this community are weakly interconnected._
- **Should `Soarscore.Domain.PublishedClassDefinition` be split into smaller, more focused modules?**
  _Cohesion score 0.0745274914089347 - nodes in this community are weakly interconnected._
- **Should `ReflightingForAMissedRoundSteps` be split into smaller, more focused modules?**
  _Cohesion score 0.12775842044134728 - nodes in this community are weakly interconnected._