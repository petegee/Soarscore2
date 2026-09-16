// authentication-and-authorisation.md WI-10. The auth features share seeded
// personas and one store, so two features racing a link-sign-in for the same
// persona can double-create and hit the (Provider, Subject) unique index —
// the D5 arbiter — with a tolerated-but-noisy 409. The none-mode features are
// self-contained per scenario and tolerate parallelism, but the assembly has
// no per-feature seam to serialize just the auth ones, so the whole BDD suite
// runs sequentially: xunit.v3 runs classes (features) in parallel by default,
// and this switch turns that off. Suite runtime is minutes; determinism wins.
//
// CapturePolicySteps' step phrasings were still written to be distinct from
// CapturingAScoreSteps' (no duplicate regexes), so a future per-collection
// serialization can build on this file if the suite grows.

using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
