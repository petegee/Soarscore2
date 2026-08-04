// The closed enumerations of docs/soaring-domain-class-diagram.md §2 and §3.
//
// Three enumerations the diagram once carried are deliberately absent, and each
// went for the same reason — a subtype and a tag naming that subtype are two
// records of one fact, and only one of them can be wrong:
//
//   ScoreTermKind  -> the five ScoreTerm subtypes
//   SelectionKind  -> the five FlightSelection subtypes
//   ScoreStage     -> the two Task term lists (see TaskDefinition)
//
// WorkingTimeKind and PromotionKind stayed, because in both cases splitting the
// holder into subtypes was considered and declined and the enum is still doing
// the discriminating work.

namespace Soarscore.Domain.PublishedClassDefinition;

public enum MeasuredKind { Number, Flag }

public enum ParameterBindingPoint { CompetitionSetup, BeforeFlying, PerRound }

public enum PhaseType { Preliminary, Flyoff }

public enum CompositionKind { FixedSequence, ChooseFromCatalogue }

public enum DropDimension { ByRound, ByTask }

public enum PromotionKind { TopN, TopPercent }

public enum FinalRankingKind { SinglePhase, LastPhaseReplaces, SplitByPromotion }

/// <summary>
/// NotPermitted (F26) is a rule that DEFINITELY grants no re-flight —
/// NZ.3.13.1 h, NZ.3.15.1 h. UndefinedRequiresRuling asserts the rulebook is
/// silent and a CD must decide. Conflating them would put a ruling in front of
/// the CD that the rules have already made.
/// </summary>
public enum ReflightSelection { Replacement, BetterOf, NotPermitted, UndefinedRequiresRuling }

public enum PenaltyEffect { DeductPoints, ZeroFlight, ZeroRound, ZeroTask, Disqualify }

public enum PenaltyAccrual { OncePerAttempt, PerOccurrence }

public enum TargetAssignment { None, AnyOrder, InOrder }

public enum CapScope { PerFlight, PerTask }

public enum Comparator { LessThan, LessOrEqual, GreaterThan, GreaterOrEqual, EqualTo }

public enum WorkingTimeKind { Fixed, UntilAllFlightsComplete }

public enum NormalisationDirection { HigherIsBetter, LowerIsBetter }

public enum RoundingMode { Truncate, HalfUp, Ceiling }
