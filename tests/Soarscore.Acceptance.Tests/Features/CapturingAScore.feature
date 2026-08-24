Feature: Capturing a score
  A scorer at the flight line records what a competitor flew, against the
  rulebook the competition adopted.

  Scenario: A scorer captures a flight time for a drawn competitor
    Given a published F5J class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 3 in round 1, group 1
    And the scorer opens a flight
    And the scorer captures flightTime of 412 seconds
    Then the entry holds one flight with a flightTime of 412
    And the entry appears in the index for round 1, group 1

  Scenario: A working time that the rulebook leaves open-ended
    Given a published NZ Class M ALES 200 class definition
    And a competition adopting it with 6 registered competitors
    And groupSize bound to 6 by the contest director
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 1 in round 1, group 1
    Then the task leaves the working time open-ended

  Scenario: A false start is recorded, not refused
    Given a published F3K class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase with these tasks
      | round | task |
      | 1     | D    |
      | 2     | A    |
      | 3     | B    |
      | 4     | C    |
    When the scorer opens an entry for competitor 2 in round 1, group 1
    And the scorer opens a flight
    And the scorer records that the launch was outside the working time
    And the scorer captures flightTime of 62 seconds
    Then the flight is recorded with both the false start and the flight time

  Scenario: A mistyped flight time is corrected without annulling the entry
    Given a published F5J class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 1 round
    When the scorer records a full F5J flight for competitor 1 with a flight time of 4120 seconds
    And the scorer corrects the flight time to 412 seconds
    And the scorer records a full F5J flight for competitor 2 with a flight time of 500 seconds
    Then the winner of the group is competitor 2
    And the corrected competitor scores 824, the mistyped 4120 having been replaced
    And the entry still holds the other metrics captured alongside the flight time
    And the original 4120 is still readable next to the correction

  Scenario: An entry is annulled by a recorded ruling
    Given a published F5J class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 3 in round 1, group 1
    And the scorer opens a flight
    And the scorer captures flightTime of 412 seconds
    And the jury annuls the entry for a recorded reason
    Then the entry still holds the flight time and carries the recorded annulment
    And a further capture against the annulled entry is refused

  Scenario: An undeclared infraction type is refused
    Given a published F5J class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase of 4 rounds
    When the scorer opens an entry for competitor 3 in round 1, group 1
    And the scorer records an entry penalty with an undeclared infraction type
    Then the penalty is refused as an undeclared infraction type

  Scenario: Flights recorded out of order score identically
    Given a published F3K class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase with these tasks
      | round | task |
      | 1     | D    |
      | 2     | A    |
      | 3     | B    |
      | 4     | C    |
    When the scorer opens an entry for competitor 1 in round 1, group 1
    And the scorer opens flight 2
    And the scorer records a valid 120 second flight on flight 2
    And the scorer opens flight 1
    And the scorer records a valid 100 second flight on flight 1
    And the scorer opens an entry for competitor 2 in round 1, group 1
    And the scorer opens flight 1
    And the scorer records a valid 100 second flight on flight 1
    And the scorer opens flight 2
    And the scorer records a valid 120 second flight on flight 2
    Then both competitors score identically in the group result

  Scenario: Only the last launch is scored, however the card was typed
    Given a published F3K class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase with these tasks
      | round | task |
      | 1     | A    |
      | 2     | D    |
      | 3     | B    |
      | 4     | C    |
    When the scorer opens an entry for competitor 1 in round 1, group 1
    And the scorer opens flight 2
    And the scorer records a valid 240 second flight on flight 2
    And the scorer opens flight 1
    And the scorer records a valid 120 second flight on flight 1
    And the scorer opens an entry for competitor 2 in round 1, group 1
    And the scorer opens a flight
    And the scorer records a valid 120 second flight on flight 1
    Then the winner of the group is competitor 1
    And competitor 2 scores 500 against that last-launch flight

  Scenario: A duplicated launch is refused
    Given a published F3K class definition
    And a competition adopting it with 6 registered competitors
    And a drawn preliminary phase with these tasks
      | round | task |
      | 1     | D    |
      | 2     | A    |
      | 3     | B    |
      | 4     | C    |
    When the scorer opens an entry for competitor 3 in round 1, group 1
    And the scorer opens flight 2
    And the scorer opens flight 2 again
    Then the second open is refused as a duplicated launch
