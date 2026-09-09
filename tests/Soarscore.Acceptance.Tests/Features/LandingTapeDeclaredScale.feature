Feature: Landing tape as a declared reading scale
  WI-5 of kanban/backlog/tape-points-landing-seeds.md, on the unchanged
  canonical F3J and F5J classes. A competition may declare no instrument at
  all, and a competition that declares one may still record distances for the
  spots it had no tape for — both forms may mix freely within one group.

  Scenario: No instrument declared — distances only, existing behaviour
    Given the canonical F3J class is published for the landing proof
    And a landing proof competition adopting it with 6 registered competitors
    And its preliminary phase is drawn for 1 round
    When each pilot flies with a 500 second flight time and these landing distances
      | pilot | landing |
      | 1     | 0.0     |
      | 2     | 0.5     |
      | 3     | 2.5     |
      | 4     | 5.5     |
      | 5     | 12.5    |
      | 6     | 100     |
    Then the group's pre-normalisation scores are flight time plus the rulebook landing award
    And the group's normalised scores follow the class target of 1000
    And the landing recording reports no gaps
    And the leaderboard totals the group's normalised scores

  Scenario: Not enough tapes — readings and tape-measure distances in one group
    Given the canonical F5J class is published for the landing proof
    And a landing proof competition adopting it with 6 registered competitors
    And its preliminary phase is drawn for 1 round
    And the CD declares the NZ F3J-side tape for landingDistance
    When three pilots read their landings off the declared tape and two tape-measure theirs
      | pilot | form     | landing |
      | 1     | reading  | 98      |
      | 2     | reading  | 91      |
      | 3     | reading  | 0       |
      | 4     | distance | 0.5     |
      | 5     | distance | 2.5     |
    And the sixth pilot's entry is opened with a flight but no landing
    Then an off-scale reading is refused and an undeclared instrument is refused
    When the sixth pilot tape-measures 12.0 metres
    Then the landing recording reports no gaps
    And each distance landing scores flight time plus the rulebook award
    And each reading is held with its instrument for re-scoring
    And a reading and a tape-measure of the same landing agree
    When the scorer amends a reading and a measurement's instrument
    And the scorer touches down and then corrects the touch flag
    And the CD corrects the declaration record
    Then the re-score is visible and the audit history is retained
    And no landing contributes twice
