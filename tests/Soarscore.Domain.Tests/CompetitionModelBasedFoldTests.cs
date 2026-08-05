using CsCheck;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Model-based property test for Competition's fold (LADR-0003: CsCheck's
/// SampleModelBased) — complements CompetitionFoldTests's single, hand-written
/// event stream and CompetitionReplaceTaskRoundPropertyTests's single-event
/// navigation check by driving long, randomly-interleaved sequences of
/// CompetitorRegistered / CompetitorWithdrawn plus the four flat append-only
/// events (PenaltyRecorded, RulesAmended, ParameterBound, Finalised) against
/// the real <see cref="Competition"/> fold and a plain mutable reference
/// model in lockstep. Phase/Round/TaskRound mutation is deliberately left out
/// here — that navigation already has its own dedicated, more targeted
/// property test — so this one stays focused on the field (register/withdraw
/// addressing across many competitors) and the flat append lists.
/// </summary>
public class CompetitionModelBasedFoldTests
{
    private sealed class CompetitorModel
    {
        public required Guid Id { get; init; }

        public required int CompetitorNumber { get; init; }

        public bool Withdrawn { get; set; }
    }

    private sealed class Model
    {
        public List<CompetitorModel> Competitors { get; } = [];

        public int PenaltyCount { get; set; }

        public int RulesAmendmentCount { get; set; }

        public int ParameterBindingCount { get; set; }

        public int FinalisationCount { get; set; }
    }

    private sealed class Actual
    {
        public required Competition Value { get; set; }
    }

    private static readonly ClassDefinition SampleDefinition = Corpus.All[0].Definition;

    // A wide raw index, reduced modulo the *current* live competitor count
    // inside the operation — the count isn't known when the Gen is built,
    // only when the operation actually runs against whatever state came before it.
    private static readonly Gen<int> Pick = Gen.Int[0, 999];

    private static readonly GenOperation<Actual, Model> RegisterCompetitor =
        Gen.Guid.Operation<Actual, Model>(
            id => $"RegisterCompetitor({id})",
            (actual, id) =>
            {
                var competitor = new Competitor
                {
                    Id = new CompetitorId(id),
                    PersonRef = PersonId.New(),
                    CompetitorNumber = actual.Value.Competitors.Length + 1,
                    RegisteredAt = DateTimeOffset.UtcNow,
                };
                actual.Value = actual.Value.Apply(new CompetitorRegistered(competitor, DateTimeOffset.UtcNow));
            },
            (model, id) => model.Competitors.Add(new CompetitorModel
            {
                Id = id,
                CompetitorNumber = model.Competitors.Count + 1,
                Withdrawn = false,
            }));

    private static readonly GenOperation<Actual, Model> WithdrawCompetitor =
        Pick.Operation<Actual, Model>(
            p => $"WithdrawCompetitor(#{p})",
            (actual, p) =>
            {
                if (actual.Value.Competitors.Length == 0)
                {
                    return;
                }

                var id = actual.Value.Competitors[p % actual.Value.Competitors.Length].Id;
                actual.Value = actual.Value.Apply(new CompetitorWithdrawn(id, DateTimeOffset.UtcNow));
            },
            (model, p) =>
            {
                if (model.Competitors.Count == 0)
                {
                    return;
                }

                model.Competitors[p % model.Competitors.Count].Withdrawn = true;
            });

    private static readonly GenOperation<Actual, Model> RecordPenalty =
        Gen.Operation<Actual, Model>(
            "RecordPenalty",
            actual => actual.Value = actual.Value.Apply(new PenaltyRecorded(new Penalty { InfractionType = "test", Scope = PenaltyScope.Competition })),
            model => model.PenaltyCount++);

    private static readonly GenOperation<Actual, Model> RulesAmendedOp =
        Gen.Operation<Actual, Model>(
            "RulesAmended",
            actual => actual.Value = actual.Value.Apply(new RulesAmended(new RulesAmendment
            {
                Definition = SampleDefinition,
                Reason = "property-test amendment",
                By = "PBT",
                At = DateTimeOffset.UtcNow,
            })),
            model => model.RulesAmendmentCount++);

    private static readonly GenOperation<Actual, Model> ParameterBoundOp =
        Gen.Operation<Actual, Model>(
            "ParameterBound",
            actual => actual.Value = actual.Value.Apply(new ParameterBound(new ParameterBinding
            {
                ParameterName = "workingTime",
                BoundValue = MeasuredValue.Of(600m),
                By = "PBT",
                At = DateTimeOffset.UtcNow,
            })),
            model => model.ParameterBindingCount++);

    private static readonly GenOperation<Actual, Model> FinalisedOp =
        Gen.Operation<Actual, Model>(
            "Finalised",
            actual => actual.Value = actual.Value.Apply(new Finalised(new Finalisation
            {
                Scope = FinalisationScope.Phase,
                Revision = actual.Value.Finalisations.Length + 1,
                By = "PBT",
                At = DateTimeOffset.UtcNow,
                DeclaredResults = [new DeclaredResult { CompetitorRef = CompetitorId.New(), Aggregate = 100m, Placing = 1, Promoted = true }],
            })),
            model => model.FinalisationCount++);

    private static Competition BuildInitialCompetition()
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
            CompetitionId.New(), "Model Test Comp", "Nowhere",
            new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 15),
            "1.0.0", adoptedRules, at);

        return Competition.Create(created);
    }

    private static readonly Gen<(Actual actual, Model model)> Initial =
        Gen.Int[0, 0].Select(_ => (new Actual { Value = BuildInitialCompetition() }, new Model()));

    [Fact]
    public void Random_event_sequences_fold_to_the_structurally_matching_reference_model()
    {
        Check.SampleModelBased(
            Initial,
            [RegisterCompetitor, WithdrawCompetitor, RecordPenalty, RulesAmendedOp, ParameterBoundOp, FinalisedOp],
            StructurallyEqual);
    }

    private static bool StructurallyEqual(Actual actual, Model model)
    {
        var competitors = actual.Value.Competitors;
        if (competitors.Length != model.Competitors.Count)
        {
            return false;
        }

        for (var i = 0; i < competitors.Length; i++)
        {
            var competitor = competitors[i];
            var competitorModel = model.Competitors[i];
            if (competitor.Id.Value != competitorModel.Id
                || competitor.CompetitorNumber != competitorModel.CompetitorNumber
                || (competitor.WithdrawnAt is not null) != competitorModel.Withdrawn)
            {
                return false;
            }
        }

        return actual.Value.Penalties.Length == model.PenaltyCount
            && actual.Value.RulesAmendments.Length == model.RulesAmendmentCount
            && actual.Value.ParameterBindings.Length == model.ParameterBindingCount
            && actual.Value.Finalisations.Length == model.FinalisationCount;
    }
}
