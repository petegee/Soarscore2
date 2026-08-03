# Seed classes — the authoring source

The eleven Competition Class definitions, authored as C# records per ADR-0002 §2
and emitting the canonical JSON in `seed-data/json/` at run time.

**This project never ships.** It is one of the two review surfaces ADR-0002 §7
distinguishes, and they are not interchangeable:

| Surface | Checked in | Ships | Answers |
|---|---|---|---|
| this project, `#` citations carried across as comments | yes | no | *Does this match the rulebook?* |
| `seed-data/json/` | yes | yes | *Is this what a user would POST, and does it still say what it said?* |

Rule references stop at the repository boundary (ADR-0002 §7). They are an
authoring-side property and do not enter the model, the wire format, the stored
definition or `AdoptedRules`.

## Running it

```
dotnet run --project tools/Soarscore.SeedData
```

Emits `seed-data/json/*.json` and checks four things: the round trip
JSON → records → JSON is byte-identical, the source-generated context agrees with
reflection in both directions, the deepest path stays inside the ingestion depth
limit, and each definition's content hash is printed (ADR-0002 §5 — not a
version; it is what makes a replay provable and drift detectable).

Byte comparison, not record comparison: `ImmutableArray<T>.Equals` is
reference-based, so `definition.Equals(reread)` is false for every definition in
the corpus even when the JSON matches exactly.

## How the notation maps

Nothing here is a notation construct — notation §7.1's sugar expands before
adoption, so the model only ever holds the expanded instance.

| Notation | Here |
|---|---|
| `task X "…" like Y` + overrides | `TaskY with { … }` |
| `metricSet` / `use` | a shared `ImmutableArray<MetricDefinition>` property |
| `rows` / `bands` + `use` | a shared `ImmutableArray<LookupRow>` / `<Band>` property |
| `param(<name>)` | `NumberOrParam.Param(…)` / `FlagOrParam.Param(…)` |
| `score` / `score normalised` | `TaskDefinition.Score` / `.ScoreNormalised` |

`with` **is** `like`, including notation §7.2's edge: a restated block that omits
a keyword takes the *default*, not the parent's value — which is exactly what
constructing a fresh value object and leaving a member unset does.

## Status of the transcription

ADR-0002 §6 requires each class to be reviewed **against the rule refs**, class by
class, not against the `.class` file — re-deriving from the source is what catches
an error the `.class` file and the C# now share. That review has **not** happened.
Until it has, treat the eleven definitions as untranscribed-but-plausible, and
check F3K Tasks E and H against the worked examples in the rule text
(`F3K.11.5` → 142 s, `F3K.11.8` → 569 s) rather than against each other — the
failure there is silent in both directions.
