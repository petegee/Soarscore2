Feature: Capturing a score
  A scorer at the flight line records what a competitor flew, against the
  rulebook the competition adopted.

  Scenario: A scorer captures a flight time for a drawn competitor
    Given a published F5J class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 3 in round 1, group 1
    And the scorer opens a flight launched at 10:03:12
    And the scorer captures flightTime of 412 seconds
    Then the entry holds one flight with a flightTime of 412
    And the entry appears in the index for round 1, group 1

  Scenario: A working time that the rulebook leaves open-ended
    Given a published NZ Class M ALES 200 class definition
    And a competition adopting it with 6 registered competitors
    And groupSize bound to 6 by the contest director
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 1 in round 1, group 1
    Then the entry's working time has no end

  Scenario: A launch before the working time is recorded, not refused
    Given a published F3K class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 2 in round 1, group 1
    And the scorer opens a flight launched 5 minutes before the working time begins
    Then the flight is recorded with its launch time unchanged
