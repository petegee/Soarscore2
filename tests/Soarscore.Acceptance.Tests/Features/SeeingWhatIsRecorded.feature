Feature: Seeing what is recorded
  Before closing a task-round the contest director can ask what has been
  recorded for it: who was drawn and has not entered, whose entry holds no
  flight, which flights are missing a metric the task declares. The answer is
  a factual count — "18/20 recorded" — never a verdict: only the CD decides
  when a round's scores are in and settled, so nothing here may gate or
  complete anything (NFR-4).

  Scenario: A fully recorded task-round shows every competitor recorded, with no gaps
    Given an F5J competition under way with 6 competitors and 4 drawn rounds
    And all six competitors have flown round 1
    When the contest director asks what is recorded for round 1
    Then all six competitors are shown as recorded with no metric gaps

  Scenario: Competitors without scores are named, and an unflown entry is shown as such
    Given an F5J competition under way with 6 competitors and 4 drawn rounds
    And every competitor but two has flown round 1
    And one of those two opened an entry without flying it
    When the contest director asks what is recorded for round 1
    Then the competitor who never entered is named as not recorded
    And the entry without a flight is shown as recorded but unflown

  Scenario: A partially captured flight is named with its missing metrics
    Given an F5J competition under way with 6 competitors and 4 drawn rounds
    And every competitor has flown round 1 except the last, whose flight was captured with its flight time alone
    When the contest director asks what is recorded for round 1
    Then that flight is shown missing its five other metrics in the task's declared order
