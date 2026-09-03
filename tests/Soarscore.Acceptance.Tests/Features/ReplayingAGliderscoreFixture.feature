@gliderscore
Feature: Replaying a GliderScore fixture
  The golden-path test (kanban/in-progress/gliderscore-replay-and-compare-harness.md):
  a real completed GliderScore competition is replayed into Soarscore through
  the public command surface only — publish the authored class definition,
  create, register, prescribe the realised draw, accept, open entries/flights,
  capture measurements, complete task rounds, finalise — and its persisted
  scores are   compared against Soarscore's at three grains with EXACT decimal
  equality, no tolerance: raw flight score (in-process, pre-normalisation),
  per-round normalised score, and the final ranking. A per-fixture divergence
  ledger lists accepted differences after human triage; the comparator
  subtracts them and fails on the remainder.
  WI-5 adds the harness's own self-checks on top of that machinery:
  replay determinism, score conservation, and ledger strictness.

  Scenario: The ales-sample-comp fixture reproduces GliderScore exactly at all three grains
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "ales-sample-comp"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And the fixture carries no ledgered divergences

  Scenario: The f3j-international-flyoff fixture reproduces GliderScore exactly at all three grains
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f3j-international-flyoff"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And the fixture carries no ledgered divergences

  Scenario: The f3j-international fixture reproduces GliderScore exactly at all three grains modulo its ledgered phantom-group cells
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f3j-international"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And the derived team standings match the fixture's team semantics exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And every ledgered divergence cites an arithmetic-story divergence ID
    And the fixture ledger records exactly 2 accepted divergences

  Scenario: The f3k-sample-comp fixture reproduces GliderScore exactly at all three grains across its per-round task schedule
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f3k-sample-comp"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And every ledgered divergence cites an arithmetic-story divergence ID
    And the fixture ledger records exactly 1 accepted divergences

  Scenario: The jerilderie-2010 fixture reproduces GliderScore exactly at all three grains
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "jerilderie-2010"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And every ledgered divergence cites an arithmetic-story divergence ID
    And the fixture ledger records exactly 1 accepted divergences

  Scenario: The f3k-june-2020 fixture reproduces GliderScore exactly at all three grains modulo its ledgered cancelled re-draw cells
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f3k-june-2020"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And every ledgered divergence cites an arithmetic-story divergence ID
    And the fixture ledger records exactly 11 accepted divergences

  Scenario: The f3k-southern-fling fixture reproduces GliderScore exactly at all three grains across its twelve-task catalogue modulo its ledgered retired-pilot slots
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f3k-southern-fling"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And every ledgered divergence cites an arithmetic-story divergence ID
    And the fixture ledger records exactly 14 accepted divergences

  Scenario: The f5j-christchurch-2019 fixture reproduces GliderScore exactly at all three grains with its float32 persist-cast witness
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f5j-christchurch-2019"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And the fixture carries no ledgered divergences
    And the fixture's float32 persist-cast witness property holds over its scored normalised cells

  Scenario: The f5j-hawkes-bay-trials fixture reproduces GliderScore exactly at all three grains with its make-up flights aggregated into their destination rounds
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f5j-hawkes-bay-trials"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And every ledgered divergence cites an arithmetic-story divergence ID
    And the fixture ledger records exactly 0 accepted divergences

  Scenario: The f5j-nz-south-island fixture reproduces GliderScore exactly at all three grains
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "f5j-nz-south-island"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And kept normalised cells minus dropped cells and aggregate penalties conserve into every final score
    And the fixture carries no ledgered divergences

  Scenario: Replaying the ales-sample-comp fixture twice within one run issues identical command counts and compares exact twice
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "ales-sample-comp" twice within this scenario
    Then both replays ran against fresh competitions in the shared store
    And both replays issued identical command counts
    And both replays compare exact at all three grains modulo the fixture ledger

  Scenario: A seeded fake mismatch fails unledgered and passes only under exactly its own ledger entry
    Given a synthetic comparison carrying one normalised-grain mismatch for pilot 7 in round 2 group 1, ours 505.0 versus oracle 495.0
    When the comparator subtracts an empty ledger
    Then the report fails with a diff table naming the seeded mismatch
    When the comparator subtracts a ledger entry covering exactly that cell
    Then the report compares exact
    When the comparator subtracts a ledger entry naming a different pilot instead
    Then the report fails with a diff table naming the seeded mismatch
