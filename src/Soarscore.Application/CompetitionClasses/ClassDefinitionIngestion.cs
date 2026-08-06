// Ingestion input limits — docs/plans/class-definition-adoption-steel-thread-plan.md
// WI-1, LADR-0002 §4: "payload size, nesting depth, band/row/term/parameter/task
// counts... the absence of them is the obvious denial-of-service surface." The
// input to this pipeline is untrusted (LADR-0002 §1), so these limits run
// BEFORE ClassDefinitionValidation.Validate — they bound how much work the
// sixteen adoption checks themselves have to do on adversarial input.
//
// Options mirrors tools/Soarscore.SeedData/Json.cs's SoarscoreJson.Ingestion —
// same camelCase/WhenWritingNull/lenient-discriminator-order settings, same
// NumberOrParam/FlagOrParam converters — but is Application's own copy rather
// than a reference to the seed tool: Application must not depend on
// Soarscore.SeedData, and IngestionMaxDepth's value (24) is copied from
// SoarscoreJson.IngestionMaxDepth, a spike finding (the corpus's deepest path
// is 11) rather than a value derived at runtime.
//
// Deliberately NOT DecimalAsStringConverter (SoarscoreEventJson.Options): a
// POSTed ClassDefinition is not yet an event payload, and adding it here would
// silently disagree with what the seed corpus's own ingestion path (and this
// same definition once ClassDefinitionHashing computes its hash) both expect —
// plain JSON numbers.
//
// Five ceilings only, matching LADR-0002 §4's list item for item: bands, rows,
// terms (per task, raw + normalised combined), parameters and tasks (per
// phase). Each is set well above the corpus's actual maximum (the seed tool's
// Program.cs console report — `dotnet run --project tools/Soarscore.SeedData`
// — prints tasks/terms per class and was used to find these actuals): 27 tasks
// in one phase (F3K), 9 parameters (F3K), a handful of bands/rows per term.

using System.Text.Json;
using System.Text.Json.Serialization;
using Soarscore.Domain;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Application.CompetitionClasses;

public static class ClassDefinitionIngestion
{
    /// <summary>ADR-0002 §4: the input is untrusted, so nesting depth is bounded. Copied from SoarscoreJson.IngestionMaxDepth.</summary>
    public const int MaxDepth = 24;

    /// <summary>Enforced at the Api/model-binding layer (WI-6), not by this file's CheckLimits — a byte cap applies before any JSON is parsed at all.</summary>
    public const long MaxPayloadBytes = 262_144;

    public const int MaxParametersPerDefinition = 64;

    public const int MaxTasksPerPhase = 64;

    public const int MaxScoreTermsPerTask = 64;

    public const int MaxBandsPerTerm = 16;

    public const int MaxRowsPerTerm = 64;

    /// <summary>What a `POST /publish-class-definition` body binds through.</summary>
    public static readonly JsonSerializerOptions Options = Build();

    /// <summary>
    /// Count-ceiling defects only — payload size and nesting depth are caught
    /// before a ClassDefinition exists to call this on (a byte cap ahead of
    /// parsing; MaxDepth as part of <see cref="Options"/> itself, which throws
    /// during deserialisation rather than producing a Defect).
    /// </summary>
    public static IReadOnlyList<Defect> CheckLimits(ClassDefinition definition)
    {
        var defects = new List<Defect>();

        CheckCount(definition.Parameters.Length, MaxParametersPerDefinition, "$.parameters", "parameters", defects);

        for (var p = 0; p < definition.Phases.Length; p++)
        {
            var phase = definition.Phases[p];
            var phasePath = $"$.phases[{p}]";
            CheckCount(phase.Tasks.Length, MaxTasksPerPhase, $"{phasePath}.tasks", "tasks", defects);

            for (var t = 0; t < phase.Tasks.Length; t++)
            {
                var task = phase.Tasks[t];
                var taskPath = $"{phasePath}.tasks[{t}]";
                CheckCount(task.Score.Length + task.ScoreNormalised.Length, MaxScoreTermsPerTask, $"{taskPath}.score", "score terms", defects);

                foreach (var term in task.Score.Concat(task.ScoreNormalised))
                {
                    CheckTermLimits(term, taskPath, defects);
                }
            }
        }

        return defects;
    }

    private static void CheckTermLimits(ScoreTerm term, string path, List<Defect> defects)
    {
        switch (term)
        {
            case PiecewiseTerm piecewise:
                CheckCount(piecewise.Bands.Length, MaxBandsPerTerm, $"{path}.bands", "bands", defects);
                break;

            case LookupTerm lookup:
                CheckCount(lookup.Rows.Length, MaxRowsPerTerm, $"{path}.rows", "rows", defects);
                break;

            case ConditionalTerm conditional:
                CheckTermLimits(conditional.Then, $"{path}.then", defects);
                if (conditional.Else is not null)
                {
                    CheckTermLimits(conditional.Else, $"{path}.else", defects);
                }

                break;
        }
    }

    private static void CheckCount(int actual, int max, string path, string what, List<Defect> defects)
    {
        if (actual > max)
        {
            defects.Add(new Defect(
                $"class-definition.ingestion.too-many-{what.Replace(' ', '-')}",
                path,
                $"Too many {what}: {actual} exceeds the ingestion limit of {max}."));
        }
    }

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = MaxDepth,
            AllowOutOfOrderMetadataProperties = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new NumberOrParamConverter());
        options.Converters.Add(new FlagOrParamConverter());
        return options;
    }
}
