// docs/plans/class-definition-adoption-steel-thread-plan.md WI-3. A fold
// invariants property, complementing ClassDefinitionProjectionTests.cs's
// fixed examples: across a randomised Published[-> Retired] event pair, the
// identity fields Published set must survive untouched, and RetiredAt must
// track exactly the Retired event applied (or stay null if none was).

using CsCheck;
using Soarscore.Application.Queries.CompetitionClasses;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using Xunit;

using Soarscore.Application.Shared.CompetitionClasses;
namespace Soarscore.Application.Tests.Queries.CompetitionClasses;

public class ClassDefinitionProjectionPropertyTests
{
    private static readonly Gen<ClassDefinition> AnyCorpusDefinition =
        Gen.OneOfConst(Corpus.All.Select(c => c.Definition).ToArray());

    /// <summary>A syntactically hash-shaped string (64 hex chars) — ClassDefinitionStreamId.From needs at least 16 bytes to derive a Guid from.</summary>
    private static readonly Gen<string> AnyHash =
        Gen.Byte.Array[32].Select(bytes => Convert.ToHexString(bytes).ToLowerInvariant());

    private static readonly Gen<DateTimeOffset> AnyInstant =
        Gen.Int[0, 3650].Select(days => new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days));

    [Fact]
    public void Publishing_then_optionally_retiring_preserves_identity_fields_and_models_RetiredAt_correctly()
    {
        (from definition in AnyCorpusDefinition
         from hash in AnyHash
         from publishedAt in AnyInstant
         from isRetired in Gen.Bool
         from retiredAfterDays in Gen.Int[1, 1000]
         select (definition, hash, publishedAt, isRetired, retiredAfterDays))
        .Sample(t =>
        {
            var afterPublish = ClassDefinitionProjection.Apply(null, new ClassDefinitionPublished(t.hash, t.definition, t.publishedAt));

            if (afterPublish is null
                || afterPublish.Id != ClassDefinitionStreamId.From(t.hash)
                || afterPublish.ContentHash != t.hash
                || afterPublish.Name != t.definition.Name
                || afterPublish.FaiDesignation != t.definition.FaiDesignation
                || afterPublish.Version != t.definition.Version
                || afterPublish.PublishedAt != t.publishedAt
                || afterPublish.RetiredAt is not null)
            {
                return false;
            }

            if (!t.isRetired)
            {
                return true;
            }

            var retiredAt = t.publishedAt.AddDays(t.retiredAfterDays);
            var afterRetire = ClassDefinitionProjection.Apply(afterPublish, new ClassDefinitionRetired("fuzz", retiredAt));

            return afterRetire is not null
                && afterRetire.RetiredAt == retiredAt
                && afterRetire.Id == afterPublish.Id
                && afterRetire.ContentHash == afterPublish.ContentHash
                && afterRetire.Name == afterPublish.Name
                && afterRetire.FaiDesignation == afterPublish.FaiDesignation
                && afterRetire.Version == afterPublish.Version
                && afterRetire.PublishedAt == afterPublish.PublishedAt;
        });
    }
}
