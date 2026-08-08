Feature: Scoring a competition
  Once a group has flown, the scoring pipeline turns raw measurements into a
  normalised group result. Once enough rounds have been captured, it turns
  every group's results into a ranked, provisional leaderboard — provisional
  because it is built only from the rounds actually flown so far.

  Scenario: A group's scores are read out after it lands
    Given the F5J class is published
    And a competition is created adopting it, with 6 registered competitors
    And the preliminary phase is drawn for 1 round
    When every competitor in round 1 flies with a distinct flight time
    Then the task-round result for round 1 holds a normalised score for all 6 competitors
    And the competitor with the longest flight time is the sole winner with the class's normalisation target of 1000

  Scenario: The leaderboard drops a competitor's worst round
    Given the F5J class is published
    And a competition is created adopting it, with 6 registered competitors
    And the preliminary phase is drawn for 5 rounds
    When every competitor flies every round, competitor 1 flying a deliberately short flight time in round 3
    Then the competition leaderboard excludes competitor 1's round 3 score from their final aggregate

  Scenario: A competitor who did not fly a round is absent from it, not zeroed in it
    Given the F5J class is published
    And a competition is created adopting it, with 6 registered competitors
    And the preliminary phase is drawn for 3 rounds
    When every competitor flies rounds 1 and 2, and nobody flies round 3
    Then the competition leaderboard scores every competitor as the sum of rounds 1 and 2 only
