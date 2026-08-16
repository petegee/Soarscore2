// Content-hash computation — ADR-0002 §5.
//
// MUST agree byte-for-byte with tools/Soarscore.SeedData/Json.cs's
// `SoarscoreJson.Hashable` options (unindented canonical form) — the same
// definition must hash identically whether it is being authored as seed data
// or published through this application's ingestion path, or drift detection
// (ADR-0002 §5) is broken on day one. Kept as a small, separate duplicate
// rather than a shared reference: the seed tool's options additionally carry
// Ingestion/SourceGenerated variants specific to the authoring pipeline that
// have no reason to be visible from Application.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.Commands.CompetitionClasses;

public static class ClassDefinitionHashing
{
    /// <summary>
    /// Canonical, unindented, camelCase, null-omitting — the exact form ADR-0002
    /// §5's hash is taken over. Deliberately NOT SoarscoreEventJson.Options: that
    /// adds a decimal-as-string converter for event-log safety (LADR-0001 §4.6)
    /// which would change the hash of otherwise-identical content and break
    /// drift detection against the seed corpus's published hashes.
    /// </summary>
    public static readonly JsonSerializerOptions HashableOptions = Build();

    /// <summary>
    /// The full SHA-256 hex digest, lowercase. ADR-0002 §5 asks for a content
    /// hash and does not ask for a shortened one; tools/Soarscore.SeedData's
    /// console report truncates to 16 characters for human readability only —
    /// that truncation stays local to the report, not the identity.
    /// </summary>
    public static string ComputeContentHash(ClassDefinition definition)
    {
        var canonical = JsonSerializer.Serialize(definition, HashableOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new NumberOrParamConverter());
        options.Converters.Add(new FlagOrParamConverter());
        return options;
    }
}
