Feature: Recording a reflight ruling
  Some classes' rulebooks grant a re-flight but say nothing about which of a
  competitor's two attempts counts (F3B Task C, F5L 5.5.12.9, NZ.3.12.5 l).
  Scoring refuses such a task-round honestly with score.reflightRequiresRuling
  rather than assume — which strands a real contest unless the Contest Director
  can settle it. A recorded ruling is that settlement: auditable, attributed,
  supersede-able, and consumed by scoring only where the class rules are silent.

  NZ Class M ALES 200 throughout — the corpus class whose rulebook is silent on
  both slots (NZ.3.12.5 l grants the re-flight and stops).

  Scenario: The silent rulebook blocks the leaderboard until a ruling is recorded
    Given an NZ Class M ALES 200 competition of 4 competitors drawn for one round
    And every competitor flies the original group at their seeded pace
    And a reflight group holds the round winner and one filler, and both fly again with the winner worse
    When the leaderboard is requested
    Then the leaderboard request is refused with score.reflightRequiresRuling
    When the CD rules the winner's re-flight counts outright and the filler takes the better of their two attempts
    Then the leaderboard computes
    And the round winner scores exactly 750

  Scenario: A ruled filler takes the better of their two attempts
    Given an NZ Class M ALES 200 competition of 4 competitors drawn for one round
    And every competitor flies the original group at their seeded pace
    And a reflight group holds the round winner and one filler, and both fly again with the winner better
    When the CD rules both re-flights by role: Replacement for the winner, BetterOf for the filler
    Then the leaderboard computes
    And the filler scores exactly 900

  Scenario: A changed mind follows the most recently logged ruling
    Given an NZ Class M ALES 200 competition of 4 competitors drawn for one round
    And every competitor flies the original group at their seeded pace
    And a reflight group holds the round winner and one filler, and both fly again with the winner worse
    When the CD records a BetterOf ruling for the round winner and the filler
    And the leaderboard is requested
    Then the leaderboard computes
    And the round winner scores exactly 1000
    When the CD supersedes the winner's ruling with Replacement
    And the leaderboard is requested
    Then the leaderboard computes
    And the round winner scores exactly 750

  Scenario: Rulings that decide nothing are refused
    Given an NZ Class M ALES 200 competition of 4 competitors drawn for one round
    When the CD attempts to record a NotPermitted selection as a ruling
    Then the ruling is refused with recordReflightRuling.selectionNotAResolution
    When the CD attempts to record a ruling for an unregistered competitor
    Then the ruling is refused with recordReflightRuling.competitorNotFound
    When the CD attempts to record a ruling with a blank reason
    Then the ruling is refused with recordReflightRuling.reasonRequired
