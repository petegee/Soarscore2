// Scoring teams, protection groups and team classification — the domain
// records behind teams-mvp.md (Option 2 MVP). All inside the Competition
// aggregate: a scoring team is competition-scoped (never inferred from a
// person's club or nationality), a protection group is a draw-only concept,
// and the classification policy is competition-level configuration.
//
// A competitor holds at most ONE scoring-team membership but ANY number of
// protection-group memberships — the F5J junior who sits in a national-team
// protection group and holds a protected helper pair is the test case
// (teams-mvp.md, owner decision 2026-09-02). The two kinds are deliberately
// separate types: the draw engine never sees scoring teams, and scoring never
// sees protection groups.

namespace Soarscore.Domain.Competitions;

/// <summary>
/// An entity inside this aggregate — one scoring team.
/// <see cref="IParsable{TSelf}"/> so ASP.NET's Minimal API parameter binding
/// can bind this straight from a query-string value — mirrors CompetitionId above.
/// </summary>
public readonly record struct ScoringTeamId(Guid Value) : IParsable<ScoringTeamId>
{
    public static ScoringTeamId New() => new(Guid.CreateVersion7());

    public static ScoringTeamId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out ScoringTeamId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new ScoringTeamId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// An entity inside this aggregate — one protection group.
/// <see cref="IParsable{TSelf}"/> so ASP.NET's Minimal API parameter binding
/// can bind this straight from a query-string value — mirrors CompetitionId above.
/// </summary>
public readonly record struct ProtectionGroupId(Guid Value) : IParsable<ProtectionGroupId>
{
    public static ProtectionGroupId New() => new(Guid.CreateVersion7());

    public static ProtectionGroupId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out ProtectionGroupId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new ProtectionGroupId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// A competition-scoped named team whose members' individual results may
/// contribute to that competition's team classification. Never inferred from a
/// person's club, nationality, or any other person-level fact
/// (teams-mvp.md §Glossary candidates, owner-approved 2026-09-02).
/// </summary>
public sealed record ScoringTeam
{
    public required ScoringTeamId Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>
/// A competition-scoped named set of competitors kept apart — no two in the
/// same group — by a generated draw. Draw-only meaning: it never affects
/// scores, normalisation, or classification (teams-mvp.md §Glossary candidates).
/// </summary>
public sealed record ProtectionGroup
{
    public required ProtectionGroupId Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>
/// At most one per competitor (0..1 scoring team per competitor) — the fold
/// REPLACES any existing record for that competitor, and the decide function
/// refuses an assignment naming a different team while one exists.
/// <see cref="Contributes"/> is <see cref="TeamClassificationConfiguration"/>'s
/// per-member contribution eligibility: false for the defending-champion-style
/// member who competes alongside their team without contributing to it.
/// </summary>
public sealed record ScoringTeamMembership
{
    public required CompetitorId CompetitorRef { get; init; }

    public required ScoringTeamId TeamRef { get; init; }

    public required bool Contributes { get; init; }
}

/// <summary>
/// Many per competitor (0..* groups) — multi-group membership is allowed and
/// expected (owner decision 3, many-to-many protection groups).
/// </summary>
public sealed record ProtectionGroupMembership
{
    public required CompetitorId CompetitorRef { get; init; }

    public required ProtectionGroupId GroupRef { get; init; }
}

/// <summary>
/// The one classification policy, declared as competition-level configuration
/// (owner decision 7). <see cref="Method"/> is the closed vocabulary; exactly
/// one MVP member, the literal <c>bestThreeScoreSum</c>. An unknown token
/// reaching the classification engine is a defect
/// (<c>teamClassification.unknownMethod</c>) — the forward-compat guard.
/// </summary>
public sealed record TeamClassificationConfiguration
{
    public required bool Enabled { get; init; }

    public required string Method { get; init; }
}

/// <summary>
/// Canonicalised unordered pair of <see cref="CompetitorId"/>s — the draw
/// engine's entire view of protection; <see cref="PhaseDraw"/> learns nothing
/// about scoring teams or protection groups, pairs only (teams-mvp.md,
/// §PhaseDraw "draw-engine discipline"). Construction canonicalises: A always
/// holds the smaller Guid, so two pairs naming the same two competitors are
/// equal regardless of the order they were named in — dedup by plain record
/// equality (HashSet/Distinct) is correct with no custom comparer.
/// </summary>
public sealed record ProtectedPair
{
    public ProtectedPair(CompetitorId first, CompetitorId second)
    {
        (A, B) = first.Value.CompareTo(second.Value) <= 0
            ? (first, second)
            : (second, first);
    }

    public CompetitorId A { get; init; }

    public CompetitorId B { get; init; }

    public void Deconstruct(out CompetitorId a, out CompetitorId b) => (a, b) = (A, B);
}
