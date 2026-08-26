@gliderscore
Feature: Replaying a GliderScore fixture
  The golden-path test (kanban/in-progress/gliderscore-replay-and-compare-harness.md):
  a real completed GliderScore competition is replayed into Soarscore through
  the public command surface only — publish the authored class definition,
  create, register, prescribe the realised draw, accept, open entries/flights,
  capture measurements, complete task rounds, finalise — and its persisted
  scores are compared against Soarscore's at three grains with EXACT decimal
  equality, no tolerance: raw flight score (in-process, pre-normalisation),
  per-round normalised score, and the final ranking. A per-fixture divergence
  ledger lists accepted differences after human triage; the comparator
  subtracts them and fails on the remainder.

  Scenario: The ales-sample-comp fixture reproduces GliderScore exactly at all three grains
    Given the fixture corpus manifest
    When the harness replays the GliderScore fixture "ales-sample-comp"
    Then every raw flight score matches the fixture oracle exactly
    And every normalised round score matches the fixture oracle exactly
    And the final ranking matches the fixture oracle exactly
    And the fixture carries no ledgered divergences
