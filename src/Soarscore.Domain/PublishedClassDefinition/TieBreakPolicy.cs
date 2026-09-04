// Tie-break policy — kanban/in-progress/tie-break-policy-in-class-definition.md
// (D2), retiring docs/soaring-domain-class-diagram.md's closing note
// "Tie-breaking is not yet modelled".
//
// Seven kinds, one closed hierarchy, and the kind IS the type — exactly the
// FlightSelection idiom (ScoringVocabulary.cs): a subtype and a tag naming it
// are two records of one fact, and only one of them can be wrong. No tag enum
// (the ScoreTermKind/SelectionKind precedent).
//
// Three families. Comparators are engine-evaluable: each names a figure and
// narrows the tie group. Operational directives are never engine-resolved —
// reaching one with the group still tied halts evaluation, shares the places
// and surfaces the requirement to contest flow as data (PendingTieBreak). An
// ordered list of comparators alone cannot express "fly more", which is the
// whole reason this is a discriminated union, not a sort-key list.
//
// EqualPlaces is the stated settlement — "ties are never broken" — and it was
// deliberately withheld while no rulebook context stated one: the model admits
// a construct only when a rule requires it (the F11 / no-anyOf precedents),
// the corpus was silent, not negative, and UndefinedRequiresRuling was that
// silence made stateable (the F12 philosophy: grep a definition and find the
// silence). Pete's 2026-09-04 ruling stated one for the NZ classes, and the
// analogue landed the same day.

using System.Text.Json.Serialization;

namespace Soarscore.Domain.PublishedClassDefinition;

/// <summary>
/// One rung of a phase's tie-break ladder, after the core-owned rung 1 (Score
/// DESC). Ordered on <see cref="PhaseDefinition.TieBreaks"/>; the ladder
/// semantics — absent keeps the display ladder, stated supersedes rung 2, the
/// first operational/undefined rung halts — are on that field's doc comment
/// (story D3).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(BestDroppedScore), "bestDroppedScore")]
[JsonDerivedType(typeof(QualifyingPosition), "qualifyingPosition")]
[JsonDerivedType(typeof(AdditionalFullRound), "additionalFullRound")]
[JsonDerivedType(typeof(TieBreakFlyoff), "tiebreakFlyoff")]
[JsonDerivedType(typeof(ClassificationRounds), "classificationRounds")]
[JsonDerivedType(typeof(EqualPlaces), "equalPlaces")]
[JsonDerivedType(typeof(UndefinedRequiresRuling), "undefinedRequiresRuling")]
public abstract record TieBreakDirective
{
    private protected TieBreakDirective() { }
}

// ------------------------------------------------------------------ comparators
// Engine-evaluable: each compares a figure and narrows the tie group.

/// <summary>
/// The best (max) dropped score, higher wins — F3K.10.2 / 5.5.10.17: "the best
/// dropped score defines the ranking". The max dropped CELL, not the sum (D4):
/// <see cref="Scoring.PhaseScores.BestDroppedAggregate"/> is the figure, and
/// it diverges from the PreDropScore countback exactly where drops are plural.
/// No penalty adjustment — the dropped cell is a round-level figure and
/// aggregate penalties deduct once from the final score.
/// </summary>
public sealed record BestDroppedScore : TieBreakDirective;

/// <summary>
/// The competitor's placing in the ranking of the earlier phase named by
/// <see cref="SourcePhaseOrdinal"/>; the better prior placing (the lower
/// number) wins — F3J.11.4 / 5.5.11.13 h: "their respective position in the
/// qualifying rounds". Adoption check 17 requires a strictly lower ordinal, so
/// the rung is unwritable on phase 1 — which is what makes the figure
/// supplyable in the single-phase world unreachable today (story D9).
/// </summary>
public sealed record QualifyingPosition : TieBreakDirective
{
    public required int SourcePhaseOrdinal { get; init; }
}

// ------------------------------------------------------------------ operational
// Never engine-resolved; surfacing is their whole effect (story D5): reaching
// one with the tie intact halts evaluation, shares the places, and puts a
// PendingTieBreak on the result for contest flow to act on.

/// <summary>The tied competitors fly one additional full round — all the class's tasks (F3B.2.8).</summary>
public sealed record AdditionalFullRound : TieBreakDirective;

/// <summary>
/// A separate fly-off for the tied competitors; the CD defines one task
/// (F3K.10.2 / 5.5.10.17). Distinct from a class's regular fly-off phase —
/// "separate" presupposes the regular fly-off has not absorbed the tied
/// pilots, which is why the F3K/F5K clauses read as preliminary-scoped (D10).
/// </summary>
public sealed record TieBreakFlyoff : TieBreakDirective;

/// <summary>
/// More rounds of the class's task are flown until the ties break (F3F.1.13).
/// F3F.1.13's "concerning the five best scores" scoping is deliberately NOT
/// modelled (D7) — contest-flow guidance, readmitted as a scope field the day
/// a second class needs one.
/// </summary>
public sealed record ClassificationRounds : TieBreakDirective;

// --------------------------------------------------------- stated settlement
// Engine-resolvable in the degenerate sense: its resolution IS the shared
// place, already assigned by the skip-ahead loop. It settles the group and
// surfaces nothing.

/// <summary>
/// Ties are never broken: the tie group keeps the shared place — "1st equal"
/// — at EVERY placing, and nothing is pending, because the outcome is stated
/// rather than awaited (no PendingTieBreak; contrast
/// <see cref="UndefinedRequiresRuling"/>, whose whole point is that a ruling
/// is required). Pete's 2026-09-04 ruling for the NZ classes: the NZ rules
/// state no tie-break anywhere (docs/rules/nz/00-nz-general-rules.md:117),
/// and the treatment that silence leaves open is equal places, announced at
/// every placing. Never mixed with other rungs: "ties
/// stand equal" beside any rung that could separate is a self-contradiction,
/// and rungs after it would be dormant anyway — it halts evaluation settled
/// (adoption check 21).
/// </summary>
public sealed record EqualPlaces : TieBreakDirective;

/// <summary>
/// The rulebook is silent: the tie stands (shared places) and a CD ruling is
/// required. F5L's 5.5.12.12 states classification and stops; the NZ rules
/// state no tie-break anywhere (docs/rules/nz/00-nz-general-rules.md); FAI
/// General C.15.6.1 likewise states none. Silence, not a stated negative —
/// where a context STATES the no-tie-break treatment, that is
/// <see cref="EqualPlaces"/> (Pete's 2026-09-04 NZ ruling) — and never mixed
/// with stated rungs in one list (adoption check 18): mixing "the rulebook is
/// silent" with stated rungs is a self-contradiction.
/// Behind it the display-ladder PreDropScore countback does not apply (D8):
/// nothing has ruled, and applying the countback would be the software
/// deciding what the CD has not.
/// </summary>
public sealed record UndefinedRequiresRuling : TieBreakDirective;
