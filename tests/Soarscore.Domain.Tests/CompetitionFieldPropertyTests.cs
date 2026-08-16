using AwesomeAssertions;
using CsCheck;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Property-based tests for invariant 1 from
/// kanban/completed/register-competitor-steel-thread-plan.md ("one registration per
/// PersonId per competition"), driving the real
/// <see cref="Competition.RegisterCompetitor"/> / <see cref="Competition.WithdrawCompetitor"/>
/// decide functions themselves — unlike CompetitionModelBasedFoldTests, which
/// drives Apply and so never exercises the checks inside RegisterCompetitor.
/// The PersonId pool is deliberately small (1..8) relative to the attempt
/// sequence (0..30) and drawn from *with replacement*: a small pool over a
/// long sequence makes duplicate registration attempts frequent rather than a
/// rare edge case the generator might never hit.
/// </summary>
public class CompetitionFieldPropertyTests
{
    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    private static Competition SampleCompetition()
    {
        var at = DateTimeOffset.UtcNow;
        var adoptedRules = new AdoptedRules
        {
            Definition = SampleDefinition,
            SourceClassId = "content-hash-abc123",
            SourceVersion = SampleDefinition.Version,
            AdoptedAt = at,
        };
        var created = new CompetitionCreated(
            CompetitionId.New(), "Field Property Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, at);

        return Competition.Create(created);
    }

    private static readonly Gen<int> PoolSize = Gen.Int[1, 8];

    [Fact]
    public void Registration_only_sequences_never_duplicate_a_PersonRef_and_number_the_field_1_to_N_in_order()
    {
        (from poolSize in PoolSize
         from attempts in Gen.Int[0, poolSize - 1].Array[0, 30]
         select (poolSize, attempts))
        .Sample(t =>
        {
            var pool = Enumerable.Range(0, t.poolSize).Select(_ => PersonId.New()).ToArray();
            var competition = SampleCompetition();

            foreach (var index in t.attempts)
            {
                var personRef = pool[index];
                var alreadyPresent = competition.Competitors.Any(c => c.PersonRef == personRef);

                var result = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);

                if (result.IsSuccess)
                {
                    competition = competition.Apply(result.Value);
                }
                else
                {
                    // The only way RegisterCompetitor can fail here: Phases is
                    // always empty (no PhaseDrawn in this test), so
                    // field.frozen is unreachable, leaving alreadyRegistered
                    // as the sole possible code.
                    result.Code.Should().Be("competition.competitor.alreadyRegistered");
                    alreadyPresent.Should().BeTrue();
                }
            }

            var competitors = competition.Competitors;
            competitors.Select(c => c.PersonRef).Distinct().Count().Should().Be(competitors.Length);
            competitors.Length.Should().BeLessThanOrEqualTo(t.poolSize);
            competitors.Select(c => c.CompetitorNumber).Should().Equal(Enumerable.Range(1, competitors.Length));
        });
    }

    private enum FieldOp { Register, Withdraw }

    private static readonly Gen<(FieldOp Op, int Index)> FieldOperation =
        from op in Gen.OneOfConst(FieldOp.Register, FieldOp.Withdraw)
        from index in Gen.Int[0, 999]
        select (op, index);

    [Fact]
    public void Withdrawal_interleaved_with_registration_never_shrinks_the_field_never_renumbers_and_never_reopens_a_withdrawn_PersonRef()
    {
        (from poolSize in PoolSize
         from ops in FieldOperation.Array[0, 30]
         select (poolSize, ops))
        .Sample(t =>
        {
            var pool = Enumerable.Range(0, t.poolSize).Select(_ => PersonId.New()).ToArray();
            var competition = SampleCompetition();
            var withdrawnPersonRefs = new HashSet<PersonId>();

            foreach (var (op, index) in t.ops)
            {
                var before = competition.Competitors;

                if (op == FieldOp.Register)
                {
                    var personRef = pool[index % t.poolSize];
                    var result = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);
                    if (result.IsSuccess)
                    {
                        competition = competition.Apply(result.Value);
                    }
                }
                else if (before.Length > 0)
                {
                    var target = before[index % before.Length];
                    var result = competition.WithdrawCompetitor(target.Id, DateTimeOffset.UtcNow);
                    if (result.IsSuccess)
                    {
                        competition = competition.Apply(result.Value);
                        withdrawnPersonRefs.Add(target.PersonRef);
                    }
                }

                var after = competition.Competitors;

                // Never shrinks.
                after.Length.Should().BeGreaterThanOrEqualTo(before.Length);

                // Every competitor present before keeps its number.
                foreach (var competitorBefore in before)
                {
                    var competitorAfter = after.Single(c => c.Id == competitorBefore.Id);
                    competitorAfter.CompetitorNumber.Should().Be(competitorBefore.CompetitorNumber);
                }
            }

            // A withdrawn person's PersonRef never becomes registrable again.
            foreach (var personRef in withdrawnPersonRefs)
            {
                var result = competition.RegisterCompetitor(CompetitorId.New(), personRef, DateTimeOffset.UtcNow);
                result.IsFailure.Should().BeTrue();
                result.Code.Should().Be("competition.competitor.alreadyRegistered");
            }
        });
    }
}
