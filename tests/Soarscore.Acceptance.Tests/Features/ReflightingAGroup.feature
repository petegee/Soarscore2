Feature: Reflighting a group
  When a mid-air, a timing failure or a launch equipment fault moots a whole
  group's attempt, the CD appends a reflight group (F3K.9.6 b) and the pilots
  it names fly again. The entitled competitor's re-flight is their official
  score even if worse; every other pilot in the group (a filler) takes the
  better of their two attempts. A competitor in no reflight group keeps their
  score, and a task-round that has been read out can still gain a reflight
  group (the protest shape).

  Scenario: A reflight group scopes an entitled re-flight and fillers
    Given an F3K competition of 6 competitors has a preliminary round drawn for task A
    And every competitor flies the original group with a distinct flight time
    And the CD appends a reflight group holding the entitled competitor and 3 fillers
    And the entitled competitor's re-flight is worse than their original, and the fillers fly again
    Then the entitled competitor is scored on their re-flight, not their better original
    And the filler is scored on the better of their two normalised scores
    And a competitor outside the reflight group keeps their original score

  Scenario: An entitled competitor re-flies with the original group
    Given an F3K competition of 6 competitors has a preliminary round drawn for task A
    And every competitor flies the original group with a distinct flight time
    When the entitled competitor opens an entitled re-flight into the original group, flying worse
    Then the entitled competitor's leaderboard score equals their re-flight's normalised score

  Scenario: A reflight group below the class minimum is refused
    Given an F3K competition of 6 competitors has a preliminary round drawn for task A
    When the CD attempts to append a reflight group of only 3 members
    Then the append is refused because the group is below the class's minimum of 4

  Scenario: A completed task-round can still gain a reflight group
    Given an F3K competition of 6 competitors has a preliminary round drawn for task A
    And every competitor flies the original group with a distinct flight time
    And the contest director completes the task-round
    When the contest director appends a reflight group of the entitled competitor and 3 fillers
    Then the competition shows the appended reflight group