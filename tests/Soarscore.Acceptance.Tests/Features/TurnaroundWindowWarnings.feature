Feature: Turn-around cap and window-sum plausibility warnings
  A contest director reading out a group sees a flag beside a physically
  impossible card — two long flights inside one working window — while every
  score in the group stands exactly as scored (warn-through only).

  Scenario: A card summing to the working time is flagged but still scores
    Given the F3K class is published
    And a competition adopting the F3K class is created with 6 registered competitors
    And the preliminary phase is drawn with these tasks
      | round | task |
      | 1     | D    |
    When competitor 1 flies two flights of 300 and 300 seconds in round 1, group 1
    And competitor 2 flies two flights of 280 and 270 seconds in round 1, group 1
    And competitor 3 flies two flights of 250 and 200 seconds in round 1, group 1
    And competitor 4 flies two flights of 200 and 150 seconds in round 1, group 1
    And competitor 5 flies two flights of 100 and 100 seconds in round 1, group 1
    And competitor 6 flies two flights of 50 and 50 seconds in round 1, group 1
    Then the task-round result is available for round 1
    And competitor 1's row carries score.windowSumExceeded
    And every other row carries no warnings
    And the group's pre-normalisation scores are
      | competitor | score |
      | 1          | 600   |
      | 2          | 550   |
      | 3          | 450   |
      | 4          | 350   |
      | 5          | 200   |
      | 6          | 100   |
    And the group's normalised scores are
      | competitor | score |
      | 1          | 1000  |
      | 2          | 916.7 |
      | 3          | 750   |
      | 4          | 583.3 |
      | 5          | 333.3 |
      | 6          | 166.7 |

  Scenario: A card over the turn-around cap but under the window is flagged cap-only
    Given the F3K class is published
    And a competition adopting the F3K class is created with 6 registered competitors
    And the preliminary phase is drawn with these tasks
      | round | task |
      | 1     | D    |
    When competitor 1 flies two flights of 299.5 and 299 seconds in round 1, group 1
    And competitor 2 flies two flights of 280 and 270 seconds in round 1, group 1
    And competitor 3 flies two flights of 250 and 200 seconds in round 1, group 1
    And competitor 4 flies two flights of 200 and 150 seconds in round 1, group 1
    And competitor 5 flies two flights of 100 and 100 seconds in round 1, group 1
    And competitor 6 flies two flights of 50 and 50 seconds in round 1, group 1
    Then the task-round result is available for round 1
    And competitor 1's row carries score.turnaroundCapExceeded
    And every other row carries no warnings
    And the group's pre-normalisation scores are
      | competitor | score |
      | 1          | 598.5 |
      | 2          | 550   |
      | 3          | 450   |
      | 4          | 350   |
      | 5          | 200   |
      | 6          | 100   |
    And the group's normalised scores are
      | competitor | score |
      | 1          | 1000  |
      | 2          | 919.0 |
      | 3          | 751.9 |
      | 4          | 584.8 |
      | 5          | 334.2 |
      | 6          | 167.1 |
