// Emits the canonical JSON for the sixteen seed definitions and the tape
// catalogue, and checks the things ADR-0002 §6 asks for before a
// transcription can be trusted, plus the tape story's own gates
// (tape-points-landing-seeds.md WI-2 — kanban lane moves; the filename is the
// stable citation):
//
//   1. SNAPSHOT      — the canonical JSON is written to json/. From
//                      here on, any change to a definition or to the emitter is a
//                      visible diff and the transcription is frozen.
//   2. ROUND TRIP    — JSON -> records -> JSON is BYTE-IDENTICAL. It must be a
//                      byte comparison and not a record comparison:
//                      ImmutableArray<T>.Equals is reference-based, so
//                      definition.Equals(reread) is false for every definition in
//                      the corpus even when the JSON matches exactly.
//   3. SOURCE GEN    — the source-generated context agrees with reflection in
//                      both directions, which is what LADR-0003 asserts.
//   4. DEPTH         — the deepest path stays inside the ingestion depth limit.
//   5. COUNTS        — Corpus.All still holds ExpectedCount classes (no delta
//                      from the tape story) and TapeCorpus.All holds
//                      ExpectedCount tapes (the catalogue is counted separately
//                      and its count is pinned).
//   6. TAPE INTEGRITY — each tape passes TapeIntegrity.Check: ascending
//                      bounded marks, distinct readings, an off-tape reading
//                      distinct from every mark (or an honest null).
//
// It also prints the content hash of each canonical form (ADR-0002 §5): not a
// version, but what makes a replay provable and drift detectable.
//
//   dotnet run --project tools/Soarscore.SeedData [-- <repo-root>]

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.SeedData;
using ScoreTerm = Soarscore.Domain.PublishedClassDefinition.ScoreTerm;

var repoRoot = args.Length > 0 ? args[0] : FindRepoRoot();
var outputDirectory = Path.Combine(repoRoot, "tools", "Soarscore.SeedData", "json");
Directory.CreateDirectory(outputDirectory);

var failures = new List<string>();
var maxDepthSeen = 0;

if (Corpus.All.Length != Corpus.ExpectedCount)
    failures.Add($"class corpus holds {Corpus.All.Length} definitions, expected {Corpus.ExpectedCount} — no delta allowed from the tape story.");
if (TapeCorpus.All.Length != TapeCorpus.ExpectedCount)
    failures.Add($"tape catalogue holds {TapeCorpus.All.Length} tapes, expected {TapeCorpus.ExpectedCount}.");

Console.WriteLine($"Emitting {Corpus.All.Length} definitions and {TapeCorpus.All.Length} tapes to {outputDirectory}");
Console.WriteLine();
Console.WriteLine($"{"class",-18} {"tasks",5} {"terms",6} {"depth",5} {"bytes",8}  content hash");

foreach (var (fileName, definition) in Corpus.All)
{
    var canonical = JsonSerializer.Serialize(definition, SoarscoreJson.Canonical);
    File.WriteAllText(Path.Combine(outputDirectory, fileName + ".json"), canonical + Environment.NewLine);

    // 2. Round trip, read back through the INGESTION options — the ones a POSTed
    //    definition would meet — and re-emitted canonically.
    var reread = JsonSerializer.Deserialize<ClassDefinition>(canonical, SoarscoreJson.Ingestion)
                 ?? throw new InvalidOperationException($"{fileName}: deserialised to null.");
    var reemitted = JsonSerializer.Serialize(reread, SoarscoreJson.Canonical);
    if (!string.Equals(canonical, reemitted, StringComparison.Ordinal))
        failures.Add($"{fileName}: round trip is not byte-identical.");

    // 3. Source generation, both directions.
    var generated = JsonSerializer.Serialize(definition, SoarscoreJson.SourceGenerated);
    if (!string.Equals(canonical, generated, StringComparison.Ordinal))
        failures.Add($"{fileName}: source-generated output differs from reflection.");

    var generatedRead = JsonSerializer.Deserialize<ClassDefinition>(canonical, SoarscoreJson.SourceGenerated)
                        ?? throw new InvalidOperationException($"{fileName}: source-generated read returned null.");
    if (!string.Equals(canonical, JsonSerializer.Serialize(generatedRead, SoarscoreJson.Canonical), StringComparison.Ordinal))
        failures.Add($"{fileName}: source-generated read differs from reflection.");

    // 4. Depth, against the ADR-0002 §4 input limit.
    using var document = JsonDocument.Parse(canonical);
    var depth = Depth(document.RootElement);
    maxDepthSeen = Math.Max(maxDepthSeen, depth);
    if (depth >= SoarscoreJson.IngestionMaxDepth)
        failures.Add($"{fileName}: nesting depth {depth} reaches the ingestion limit of {SoarscoreJson.IngestionMaxDepth}.");

    var hashable = JsonSerializer.Serialize(definition, SoarscoreJson.Hashable);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashable)))[..16].ToLowerInvariant();

    var tasks = definition.Phases.Sum(p => p.Tasks.Length);
    var terms = definition.Phases.Sum(p => p.Tasks.Sum(t => t.Score.Sum(Count) + t.ScoreNormalised.Sum(Count)));

    Console.WriteLine($"{fileName,-18} {tasks,5} {terms,6} {depth,5} {hashable.Length,8}  {hash}");
}

Console.WriteLine();
Console.WriteLine($"deepest path {maxDepthSeen} against an ingestion limit of {SoarscoreJson.IngestionMaxDepth}");

// The tape catalogue emits to its own subdirectory: two readers enumerate the
// class directory flat expecting every file to be a ClassDefinition
// (SeedCorpusIngestionTests, CatalogueDrawPropertyTests — both GetFiles with a
// non-recursive "*.json"), and a tape is not a class. Tapes live beside the
// classes, counted separately, never mixed into their enumeration.
var tapeDirectory = Path.Combine(outputDirectory, "tapes");
Directory.CreateDirectory(tapeDirectory);

Console.WriteLine();
Console.WriteLine($"{"tape",-18} {"marks",5} {"depth",5} {"bytes",8}  content hash");

foreach (var (fileName, tape) in TapeCorpus.All)
{
    foreach (var defect in TapeIntegrity.Check(fileName, tape))
        failures.Add(defect);

    var canonical = JsonSerializer.Serialize(tape, SoarscoreJson.Canonical);
    File.WriteAllText(Path.Combine(tapeDirectory, fileName + ".json"), canonical + Environment.NewLine);

    // 2. Round trip, read back through the INGESTION options.
    var reread = JsonSerializer.Deserialize<TapeDefinition>(canonical, SoarscoreJson.Ingestion)
                 ?? throw new InvalidOperationException($"{fileName}: deserialised to null.");
    var reemitted = JsonSerializer.Serialize(reread, SoarscoreJson.Canonical);
    if (!string.Equals(canonical, reemitted, StringComparison.Ordinal))
        failures.Add($"{fileName}: round trip is not byte-identical.");

    // 3. Source generation, both directions.
    var generated = JsonSerializer.Serialize(tape, SoarscoreJson.SourceGenerated);
    if (!string.Equals(canonical, generated, StringComparison.Ordinal))
        failures.Add($"{fileName}: source-generated output differs from reflection.");

    var generatedRead = JsonSerializer.Deserialize<TapeDefinition>(canonical, SoarscoreJson.SourceGenerated)
                        ?? throw new InvalidOperationException($"{fileName}: source-generated read returned null.");
    if (!string.Equals(canonical, JsonSerializer.Serialize(generatedRead, SoarscoreJson.Canonical), StringComparison.Ordinal))
        failures.Add($"{fileName}: source-generated read differs from reflection.");

    // 4. Depth, against the ADR-0002 §4 input limit.
    using var document = JsonDocument.Parse(canonical);
    var depth = Depth(document.RootElement);
    maxDepthSeen = Math.Max(maxDepthSeen, depth);
    if (depth >= SoarscoreJson.IngestionMaxDepth)
        failures.Add($"{fileName}: nesting depth {depth} reaches the ingestion limit of {SoarscoreJson.IngestionMaxDepth}.");

    var hashable = JsonSerializer.Serialize(tape, SoarscoreJson.Hashable);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashable)))[..16].ToLowerInvariant();

    Console.WriteLine($"{fileName,-18} {tape.Marks.Length,5} {depth,5} {hashable.Length,8}  {hash}");
}

Console.WriteLine();
Console.WriteLine($"deepest path {maxDepthSeen} against an ingestion limit of {SoarscoreJson.IngestionMaxDepth}");
Console.WriteLine($"class corpus {Corpus.All.Length} (expected {Corpus.ExpectedCount}), tape catalogue {TapeCorpus.All.Length} (expected {TapeCorpus.ExpectedCount})");

if (failures.Count > 0)
{
    Console.WriteLine();
    foreach (var failure in failures)
        Console.WriteLine("FAIL  " + failure);
    return 1;
}

Console.WriteLine("all checks passed: round trip byte-identical, source generation agrees, depth within limit, counts pinned, tapes integral");
return 0;

static int Count(ScoreTerm term) => term switch
{
    ConditionalTerm conditional => 1 + Count(conditional.Then) + (conditional.Else is null ? 0 : Count(conditional.Else)),
    _ => 1,
};

static int Depth(JsonElement element) => element.ValueKind switch
{
    JsonValueKind.Object => 1 + element.EnumerateObject().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
    JsonValueKind.Array => 1 + element.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
    _ => 0,
};

static string FindRepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        directory = directory.Parent;
    return directory?.FullName
           ?? throw new InvalidOperationException("Could not find the repository root; pass it as the first argument.");
}
