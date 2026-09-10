# Story — CI authoring-drift guard for the seed corpus

**Status:** Stub · **Raised:** 2026-09-11

## What

A CI step that runs `dotnet run --project tools/Soarscore.SeedData` and then
`git diff --exit-code tools/Soarscore.SeedData/json`, failing the build when
the emitted canonical JSON differs from what is checked in.

## Why it matters

The shipped/deployed corpus is the JSON (the Docker image copies
`json/` to `/app/seed` and the startup seeder publishes it —
kanban/completed/seed-class-corpus-at-startup.md). The C# in
`tools/Soarscore.SeedData/Seed*.cs` is the authoring source. Nothing currently
stops an edit to the C# from shipping a stale, unregenerated corpus: the seed
tool verifies the JSON it *finds*, not that the JSON matches the C# someone
just changed. The diff gate makes authoring drift a red build instead of a
quietly out-of-date catalogue.

## Before starting

- [ ] Pick the home for the step: the repo already has
      `.github/workflows/fly-deploy.yml` (deploy-on-push) — add a build/test
      job there or a sibling workflow.
- [ ] Confirm the seed tool exits non-zero on its own internal gates (it does —
      Program.cs returns 1 on failures) so the CI step can also catch corpus
      corruption, not just drift.
