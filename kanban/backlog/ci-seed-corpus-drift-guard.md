# Story — CI authoring-drift guard for the seed corpus

**Status:** Planned · **Raised:** 2026-09-11 · **Planned:** 2026-09-13

## What

Make authoring drift between the seed tool's C# (`tools/Soarscore.SeedData/Seed*.cs` —
the authoring source) and the shipped canonical JSON (`tools/Soarscore.SeedData/json/` —
what the Docker image ships and the startup seeder publishes,
kanban/completed/seed-class-corpus-at-startup.md) a **red build** instead of a
quietly out-of-date catalogue. Three parts:

1. Check the emitted corpus in (it is deployment data today but is gitignored —
   see *Consistency findings* below).
2. Harden the emitter so the committed bytes are reproducible on any OS and so
   files the tool no longer emits disappear instead of lingering as orphans.
3. A CI step that regenerates the corpus and fails when the working tree is no
   longer byte-identical to what is committed.

## Why it matters

The deployed corpus is the JSON: `Dockerfile:28` copies `tools/Soarscore.SeedData/json/`
to `/app/seed` and `ClassCorpusSeederHost` publishes it at startup. Today nothing ties
that JSON to the C# someone just edited — a local image build ships whatever was last
regenerated, and a fresh clone cannot build the image at all (the directory is
gitignored; CI papers over this by regenerating in both jobs). Checking the corpus in
makes every PR diff show exactly what would ship, and the CI gate forces regeneration
to be part of the same commit as the C# change.

## Consistency findings (settled 2026-09-13, user decision)

Cross-reference per house rule 2 surfaced three stale facts; the first was a real
contradiction resolved with the user:

- **`json/` is gitignored but is load-bearing.** `.gitignore:7-8` ignores
  `tools/Soarscore.SeedData/json/` (0 files tracked) and the tool README says
  "not checked in" — yet `Dockerfile:28` COPYs it and CI must regenerate it before
  both test and deploy or the build breaks. Resolution: **check the corpus in**
  (of the three options offered, "check in the JSON" was chosen over
  "Dockerfile regenerates" and "diff head vs base"). This makes the completed
  corpus story's "frozen" claim literally true and fixes fresh-clone image builds.
- **The story's named workflow does not exist.** There is no
  `.github/workflows/fly-deploy.yml`; the deploy-on-push job lives in
  `.github/workflows/build-and-test.yml`. The guard goes in that file's
  `build-and-test` job (which the `deploy` job already gates via `needs:`).
- **The tool README is stale twice over**: "eleven Competition Class definitions"
  (Corpus is 16 + 3 tapes, `Corpus.cs:26`, `TapeCorpus.cs:33`) and "not checked in".
  Updated in WI-3.

Nothing in `docs/` changes; no glossary concept is touched; NFR-4 is not engaged
(this gates authoring commits, not score capture). No property-test work item:
the invariant — *emitted corpus == committed corpus, byte for byte, including the
file set* — is enforced mechanically by the git-status gate, and the tool's own
gates (round trip, source-gen agreement, depth, counts, tape integrity) already
cover the corpus content itself.

## Plan

### WI-1 — Emitter hardening (`tools/Soarscore.SeedData/Program.cs`)

Two changes, nothing else; all existing gates stay exactly as they are.

1. **Fixed line endings.** `Program.cs:57` and `Program.cs:113` append
   `Environment.NewLine`. Replace with `"\n"` so the canonical bytes are
   OS-independent: a Windows author regenerating must produce byte-identical
   files to CI. (On Linux this is a byte-for-byte no-op, so the corpus checked
   in by WI-2 is already the final bytes.)
2. **Clean slate before emit.** Before the class loop, delete the `*.json` files
   the tool owns — top level of `json/` only (`SearchOption.TopDirectoryOnly`),
   and the same in `json/tapes/` before the tape loop. Both directories are
   already tool-created and tool-owned. Rationale: today a definition removed
   from `Corpus.All` leaves its JSON behind forever; committed, that orphan would
   ship in the image and be seeded at startup while nothing authors it. Deleting
   before emit makes the guard catch that case too (a committed file the tool no
   longer emits shows as deleted in `git status`).

### WI-2 — Check the corpus in

- Delete `.gitignore` lines 7–8 (the comment and `tools/Soarscore.SeedData/json/`).
- Create `.gitattributes` (none exists) containing:
  `tools/Soarscore.SeedData/json/** eol=lf`
  so checkouts are byte-stable regardless of `core.autocrlf` (diff stays textual).
- Run the hardened tool and commit the regenerated corpus: 16 class files plus
  `json/tapes/`'s 3 tape files — the complete `git status` of
  `tools/Soarscore.SeedData/json` goes into this story's commit.
- `.dockerignore` needs no change: it already re-includes `json/**` and excludes
  `json/tapes/**`.

### WI-3 — Tool README (`tools/Soarscore.SeedData/README.md`)

Fix the three stale statements so the docs match the tree:

- "the canonical JSON is generated on demand and is not checked in" → checked in,
  byte-exact, and CI-enforced (WI-4); the C# remains the sole authoring source.
- "Generates `json/*.json` (gitignored)" → regenerates the committed corpus; also
  documents the new clean-slate behaviour (WI-1.2) and fixed `\n` endings (WI-1.1).
- "eleven Competition Class definitions" → sixteen, plus the three-tape catalogue.
- Add the local drift-check recipe mirroring the CI step:
  `dotnet run --project tools/Soarscore.SeedData && git status --porcelain tools/Soarscore.SeedData/json`
  (empty output = no drift).

### WI-4 — The CI guard (`.github/workflows/build-and-test.yml`)

In the **`build-and-test` job**, insert between "Emit seed corpus JSON" and "Test":

```yaml
      - name: Seed corpus drift guard
        run: |
          drift="$(git status --porcelain -- tools/Soarscore.SeedData/json)"
          if [ -n "$drift" ]; then
            echo "::error::Regenerated seed corpus differs from the committed corpus:"
            printf '%s\n' "$drift"
            echo "Run 'dotnet run --project tools/Soarscore.SeedData' and commit tools/Soarscore.SeedData/json/."
            exit 1
          fi
```

Decisions embedded here — do not "simplify" away from them:

- **`git status --porcelain`, not the story's original `git diff --exit-code`.**
  `git diff` alone misses untracked files (a *new* class emits a *new* JSON file —
  untracked, invisible to `git diff`) and the WI-1.2 deletion path. A CI checkout
  is clean, so *any* status line for that path is drift: added, modified, or
  deleted. The pathspec covers `json/tapes/` too.
- **The emit step stays and runs first** — it is what produces the bytes the guard
  inspects, and it runs the tool's internal gates (round trip, source-gen
  agreement, depth, counts, tape integrity), so corpus corruption fails the job
  even with no drift. The tool exits 1 on those failures (`Program.cs:149-155` —
  verified), which fails the step before the guard runs.
- **Remove the "Emit seed corpus JSON" step from the `deploy` job.** The checkout
  now supplies the corpus; the commit was already drift-gated by
  `needs: build-and-test`, so re-emitting there is dead weight. `flyctl deploy
  --remote-only` ships the working tree, `json/` included.

### WI-5 — Verification

Local, in order (no store, no Docker needed):

1. **Idempotence:** run the tool twice; after the second run
   `git status --porcelain -- tools/Soarscore.SeedData/json` is empty.
2. **Orphan path:** `git add -f` a temporary `json/zz-orphan.json`, run the tool —
   it deletes the file and `git status --porcelain` reports ` D` (the guard's
   failure shape). Clean up.
3. **Drift path:** hand-edit a committed `json/*.json` (e.g. change one number),
   run the tool — `git status --porcelain` reports ` M`; paste the WI-4 script
   into a shell and confirm it exits 1 with the file listed. Revert with `git
   checkout -- tools/Soarscore.SeedData/json`.
4. **Suites:** full local loop green with the checked-in corpus — Domain,
   Application (incl. the 7 `ClassCorpusSeederTests`), Architecture,
   Infrastructure (fast loop), and acceptance on both backends
   (`SOARSCORE_TEST_STORE=postgres` and `=sqlite`). The acceptance suite reads the
   repo corpus via `SeedDefinitionLoader`, which now finds it straight from the
   checkout.
5. **CI:** the PR's `build-and-test` run goes green with the guard step in place;
   the deploy job runs without its emit step and still deploys (corpus present in
   the flyctl build context). A throwaway branch with a hand-edited committed
   JSON must red at *Seed corpus drift guard* — delete the branch after.

## House-keeping

- No new technical debt expected; if the `.gitattributes` eol pin proves
  insufficient on a Windows author's machine, record it in `kanban/tech-debt.md`.
- Keep this filename stable; implementation cites work items as
  `kanban/backlog/ci-seed-corpus-drift-guard.md` WI-n.
- Move to `in-progress/` (with `git mv`) before writing code; to `completed/`
  with the plan updated *as built* on completion.
