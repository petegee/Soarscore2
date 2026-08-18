Feature: Closing a competition
  A contest director closes a task-round when its scores are in and settled,
  annuls one that produced no result, reopens either when a late score or a
  re-ruling arrives, and finally declares the competition's results. None of
  it is inferred: the system never decides on the CD's behalf that a round is
  finished, and it never dictates the order in which scores reach it (NFR-4).

  Scenario: Scores are captured out of round order, and every one is accepted
    Given an F5J competition is under way, with 6 competitors and 4 drawn rounds
    When every competitor flies rounds 1 and 2, and round 2's scores are all entered before round 1's
    Then every score is accepted, and the leaderboard counts both rounds for everyone

  Scenario: A round is closed and no further scores can be captured for it
    Given an F5J competition is under way, with 6 competitors and 4 drawn rounds
    And every competitor but one has flown round 1
    When the contest director closes round 1
    Then the last competitor's round 1 score is refused because the round is closed

  Scenario: A late score is entered after its round was closed, by reopening it
    Given an F5J competition is under way, with 6 competitors and 4 drawn rounds
    And every competitor but one has flown round 1
    And the contest director closes round 1
    When the contest director reopens round 1 and the late score is entered
    Then the late score is accepted and counts towards the leaderboard

  Scenario: An annulled round is excluded from the leaderboard
    Given an F5J competition is under way, with 6 competitors and 4 drawn rounds
    And every competitor has flown rounds 1 and 2
    When the contest director annuls round 2
    Then the leaderboard scores every competitor on round 1 alone

  Scenario: A competition cannot be finalised before its class's minimum rounds are flown
    Given an F5J competition is under way, with 6 competitors and 4 drawn rounds
    And every competitor has flown all 4 rounds
    And the contest director closes rounds 1, 2 and 3
    When the contest director tries to finalise the competition
    Then finalisation is refused because the class requires more rounds flown to a result

  Scenario: A finalised competition declares the same results the leaderboard shows
    Given an F5J competition is under way, with 6 competitors and 4 drawn rounds
    And every competitor has flown all 4 rounds
    And the contest director closes all 4 rounds
    When the contest director finalises the competition
    Then the declared results match the leaderboard, competitor for competitor
    And the competition is listed as finalised
