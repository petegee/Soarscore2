// The spike. Emits each transcribed class as canonical JSON, reads it back,
// re-emits it, and asserts the two are byte-identical — ADR-0002 §6's
// round-trip test. Then measures the things the open question in LADR-0003
// actually turns on: how much of the artefact is discriminator, how deep the
// nesting gets against MaxDepth, and whether the closed vocabulary survives
// deserialisation of hostile input.

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Soarscore.Spike.ClassModel;

// Next to the source, not under bin/ — the emitted JSON is the artefact this
// spike exists to be read against, and ADR-0002 §6 wants it as a visible diff.
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "out"));
Directory.CreateDirectory(outDir);

var failures = new List<string>();
void Check(bool ok, string what)
{
    Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {what}");
    if (!ok) failures.Add(what);
}

foreach (var (id, definition) in new (string, ClassDefinition)[]
         {
             ("f3k", SeedF3K.Definition),
             ("f5k", SeedF5K.Definition),
         })
{
    Console.WriteLine($"\n=== {id.ToUpperInvariant()} =====================================");

    // ---- round trip -------------------------------------------------------
    var json = JsonSerializer.Serialize(definition, SoarscoreJson.Canonical);
    ClassDefinition reread;
    try
    {
        reread = JsonSerializer.Deserialize<ClassDefinition>(json, SoarscoreJson.Canonical)!;
    }
    catch (JsonException e)
    {
        Check(false, $"deserialises: {e.Message}");
        continue;
    }

    var json2 = JsonSerializer.Serialize(reread, SoarscoreJson.Canonical);
    Check(json == json2, "JSON -> records -> JSON is byte-identical");

    // Record structural equality is NOT a substitute for the byte comparison:
    // ImmutableArray<T>.Equals compares the underlying array by reference, so
    // every collection-valued property fails. Recorded, not worked around —
    // ADR-0002 §5 already makes the content hash the identity, and the hash is
    // over the canonical bytes.
    Check(!definition.Equals(reread),
          "record equality is reference-based on collections (so the byte compare is the test)");

    File.WriteAllText(Path.Combine(outDir, $"{id}.json"), json);

    // ---- shape of the artefact --------------------------------------------
    var verbose = JsonSerializer.Serialize(definition, SoarscoreJson.Verbose);
    var hashable = JsonSerializer.Serialize(definition, SoarscoreJson.Hashable);
    File.WriteAllText(Path.Combine(outDir, $"{id}.verbose.json"), verbose);

    using var doc = JsonDocument.Parse(json);
    var depth = MaxDepth(doc.RootElement);
    var discriminators = CountDiscriminators(doc.RootElement);
    var terms = definition.Phases.Sum(p => p.Tasks.Sum(t => CountTerms(t.Score)));

    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashable)))[..16];

    Console.WriteLine($"  tasks (expanded)        {definition.Phases.Sum(p => p.Tasks.Length)}");
    Console.WriteLine($"  score terms (expanded)  {terms}");
    Console.WriteLine($"  canonical               {json.Length,7:N0} bytes, {json.Split('\n').Length,5:N0} lines");
    Console.WriteLine($"  minified                {hashable.Length,7:N0} bytes   sha256 {hash}...");
    Console.WriteLine($"  unions as $type instead {verbose.Length,7:N0} bytes   (+{(verbose.Length - json.Length) * 100.0 / json.Length:F1}%)");
    Console.WriteLine($"  \"$kind\" discriminators   {discriminators}");
    Console.WriteLine($"  max nesting depth       {depth}  (MaxDepth {SoarscoreJson.Canonical.MaxDepth})");

    Check(depth < SoarscoreJson.Canonical.MaxDepth, "nesting is inside MaxDepth with room to spare");

    // The verbose form must round-trip too — it is the same corpus, and the
    // choice between the two must not be a correctness question.
    var verboseBack = JsonSerializer.Serialize(
        JsonSerializer.Deserialize<ClassDefinition>(verbose, SoarscoreJson.Verbose)!, SoarscoreJson.Verbose);
    Check(verbose == verboseBack, "the $type form round-trips identically (the choice is cosmetic)");

    // ---- source generation ------------------------------------------------
    // LADR-0003 chooses source generation; it must agree with reflection to the
    // byte, in both directions, or the two cannot be swapped.
    var sg = JsonSerializer.Serialize(definition, SoarscoreJson.SourceGenerated);
    Check(sg == json, "source-generated output is byte-identical to reflection");
    var sgBack = JsonSerializer.Serialize(
        JsonSerializer.Deserialize<ClassDefinition>(json, SoarscoreJson.SourceGenerated)!,
        SoarscoreJson.SourceGenerated);
    Check(sgBack == json, "source-generated deserialisation round-trips");

    // ---- properties a hand-edited or third-party document may have ---------
    var minified = JsonSerializer.Serialize(definition, SoarscoreJson.Hashable);
    var reordered = MoveDiscriminatorsLast(minified);
    Check(!RoundTrips(reordered, SoarscoreJson.Canonical),
          "a document whose \"$kind\" is not first FAILS by default (the finding)");
    Check(RoundTrips(reordered, SoarscoreJson.Lenient),
          "...and binds with AllowOutOfOrderMetadataProperties");
    Check(JsonSerializer.Serialize(
              JsonSerializer.Deserialize<ClassDefinition>(reordered, SoarscoreJson.Lenient)!,
              SoarscoreJson.Canonical) == json,
          "...to exactly the same definition, so canonical order is recoverable");
}

// ---- closed vocabulary, against hostile input -----------------------------
// ADR-0002 §4: deserialisation into the sealed hierarchy is what enforces the
// closed vocabulary on the user path. These are the cases that must fail.

Console.WriteLine("\n=== untrusted input =========================================");

var wellFormed = JsonSerializer.Serialize(SeedF5K.Definition, SoarscoreJson.Hashable);

Check(Rejects(wellFormed.Replace("\"$kind\":\"rate\"", "\"kind\":\"poker\"")),
      "an unknown ScoreTerm discriminator is rejected");

Check(Rejects(wellFormed.Replace("\"$kind\":\"comparison\"", "\"kind\":\"anyOf\"")),
      "an unknown Predicate discriminator is rejected");

Check(Rejects(wellFormed.Replace("\"$kind\":\"bestN\"", "\"kind\":\"everyOtherFlight\"")),
      "an unknown FlightSelection discriminator is rejected");

Check(Rejects("""{"name":"X","version":"1"}"""),
      "a definition missing required members is rejected");

Check(Rejects(wellFormed.Replace("{\"$kind\":\"rate\",", "{")),
      "a score term with no discriminator at all is rejected");

Check(Rejects(Nest(200)), "a payload nested past MaxDepth is rejected");

Check(Rejects(wellFormed.Replace("\"cap\":599", "\"cap\":{\"paramX\":\"nlh\"}")),
      "a malformed parameter reference is rejected");

// A ParameterRef in a slot the model does not admit is not a deserialisation
// question wherever the slot is typed `decimal` — it simply fails to bind. A
// ref in a NumberOrParam slot that is not one of the thirteen would be a
// type-correct document, and that check belongs to Validate().
Check(Rejects(wellFormed.Replace("\"points\":300", "\"points\":{\"param\":\"x\"}")),
      "a parameter reference in a plain-decimal slot is rejected by the type");

Check(Hazards.CollisionIsSilentOnWriteAndFatalOnRead(),
      "a discriminator shadowing a real property writes a DUPLICATE key, silently, and cannot be read back");
Check(Hazards.PrefixedDiscriminatorRoundTrips(),
      "...and naming it \"$kind\" is the whole of the fix");

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine($"All checks passed. Artefacts in {outDir}");
    return 0;
}

Console.WriteLine($"{failures.Count} check(s) failed:");
foreach (var f in failures) Console.WriteLine($"  - {f}");
return 1;

static bool Rejects(string json)
{
    try
    {
        JsonSerializer.Deserialize<ClassDefinition>(json, SoarscoreJson.Canonical);
        return false;
    }
    catch (JsonException)
    {
        return true;
    }
    catch (NotSupportedException)
    {
        // STJ wraps some binding failures; still a rejection.
        return true;
    }
}

static bool RoundTrips(string json, JsonSerializerOptions options)
{
    try
    {
        return JsonSerializer.Deserialize<ClassDefinition>(json, options) is not null;
    }
    catch (JsonException)
    {
        return false;
    }
    catch (NotSupportedException)
    {
        // How STJ reports a missing or out-of-order type discriminator.
        return false;
    }
}

/// <summary>
/// Rewrites every object so its "kind" property comes last. STJ buffers ahead
/// to find an out-of-order discriminator, so this must still bind — it is what
/// a document round-tripped through a tool that sorts keys would look like.
/// </summary>
static string MoveDiscriminatorsLast(string json)
{
    var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
    return Rewrite(node).ToJsonString();

    static System.Text.Json.Nodes.JsonNode Rewrite(System.Text.Json.Nodes.JsonNode node)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
            {
                var rebuilt = new System.Text.Json.Nodes.JsonObject();
                foreach (var (k, v) in obj.ToList().Where(p => p.Key != "$kind"))
                    rebuilt[k] = v is null ? null : Rewrite(v.DeepClone());
                if (obj.TryGetPropertyValue("$kind", out var kind) && kind is not null)
                    rebuilt["$kind"] = kind.DeepClone();
                return rebuilt;
            }
            case System.Text.Json.Nodes.JsonArray arr:
            {
                var rebuilt = new System.Text.Json.Nodes.JsonArray();
                foreach (var item in arr.ToList())
                    rebuilt.Add(item is null ? null : Rewrite(item.DeepClone()));
                return rebuilt;
            }
            default:
                return node.DeepClone();
        }
    }
}

static string Nest(int depth) =>
    new StringBuilder()
        .Append(string.Concat(Enumerable.Repeat("{\"a\":", depth)))
        .Append('1')
        .Append(new string('}', depth))
        .ToString();

static int MaxDepth(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.Object => 1 + e.EnumerateObject().Select(p => MaxDepth(p.Value)).DefaultIfEmpty(0).Max(),
    JsonValueKind.Array => 1 + e.EnumerateArray().Select(MaxDepth).DefaultIfEmpty(0).Max(),
    _ => 0,
};

static int CountDiscriminators(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.Object => e.EnumerateObject().Sum(p => (p.Name == "$kind" ? 1 : 0) + CountDiscriminators(p.Value)),
    JsonValueKind.Array => e.EnumerateArray().Sum(CountDiscriminators),
    _ => 0,
};

static int CountTerms(ImmutableArray<ScoreTerm> terms) =>
    terms.Sum(t => 1 + t switch
    {
        ConditionalTerm c => CountTerms([c.Then]) + (c.Else is null ? 0 : CountTerms([c.Else])),
        _ => 0,
    });
