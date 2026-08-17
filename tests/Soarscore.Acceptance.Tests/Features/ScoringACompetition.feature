Feature: Scoring a competition
  Once a group has flown, the scoring pipeline turns raw measurements into a
  normalised group result. Once enough rounds have been captured, it turns
  every group's results into a ranked, provisional leaderboard — provisional
  because it is built only from the rounds actually flown so far.

  Scenario: A group's scores are read out after it lands
    Given the F5J class is published
    And a competition is created adopting it, with 6 registered competitors
    And the preliminary phase is drawn for 1 round
    And round 1 is drawn as a single group holding all 6 competitors
    When every competitor in that group flies with a distinct flight time
    Then the group's result holds a normalised score for each of its 6 competitors
    And the competitor with the longest flight time in the group is that group's winner, scoring the class's normalisation target of 1000

  Scenario: Each group in a round is normalised against its own winner
    Given the F5J class is published
    And a competition is created adopting it, with 12 registered competitors
    And the preliminary phase is drawn for 1 round
    And round 1 is drawn as 2 groups of 6 competitors
    When every competitor flies, one group flying markedly longer times than the other
    Then each competitor's score is their flight time relative to their own group's winner
    And exactly one competitor in each group scores the class's normalisation target of 1000
    And nobody is normalised against the best flight time in the other group

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
