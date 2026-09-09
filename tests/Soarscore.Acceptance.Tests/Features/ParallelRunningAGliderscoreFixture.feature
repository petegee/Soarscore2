@gliderscore
Feature: Parallel-running a GliderScore fixture under a seed competition class
  The parallel-run test (kanban/in-progress/seed-definition-parallel-run.md):
  a real completed GliderScore competition is replayed under a REAL seed
  competition class from tools/Soarscore.SeedData/json instead of its
  fixture-authored GS-mirrored definition — GliderScore as the authoritative
  system, Soarscore as a parallel run "by the book" — and the test reports
  where the two would have split on the day. The claim's shape differs from
  parity, so the test shape does too: a per-pair parallel-run ledger asserts
  "the differences are exactly the triaged set", never exact match as the goal
  (exactness is a possible outcome; on a ledgered pair it is a ledger/state
  contradiction demanding re-triage, never a quiet pass). Every triaged entry
  names its difference in words with a triage kind and a rulebook or
  local-practice citation; an untriaged difference escalates for human triage
  and is never resolved by editing the ledger to fit.

  Scenario: The ales-sample-comp parallel run under NZ ALES 200 reports exactly the triaged differences
    Given the fixture corpus manifest
    When the harness parallel-runs the GliderScore fixture "ales-sample-comp" under the seed class "80-nz-m-ales200"
    Then the parallel-run verdict is exactly the triaged differences
    And the final placings match the GliderScore oracle exactly
    And every ledgered difference is a triaged rulebook-vs-local-practice difference with a citation

  Scenario: The f5j-christchurch-2019 parallel run under canonical F5J reports exactly the triaged differences
    Given the fixture corpus manifest
    When the harness parallel-runs the GliderScore fixture "f5j-christchurch-2019" under the seed class "30-f5j"
    Then the parallel-run verdict is exactly the triaged differences
    And the raw grain is exact against the GliderScore oracle
    And the final placings split from the GliderScore oracle exactly as the ledger triages
    And every ledgered difference is a triaged rulebook-vs-local-practice difference with a citation
