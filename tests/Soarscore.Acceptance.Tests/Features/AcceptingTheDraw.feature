Feature: Accepting the draw
  A draw is produced at the start of the competition from the registered
  competitors, and can be accepted or rejected and redrawn. Once accepted,
  the competition can begin (docs/soaring-domain-glossary.md). This feature is
  that sentence end to end — kanban/in-progress/draw-acceptance-redraw.md WI-7.

  Scenario: A CD reviews the drawn groups and accepts the draw
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn for review
    When the contest director accepts the draw
    Then the competition reads as having an accepted draw

  Scenario: A rejected draw is redrawn after a latecomer registers, then accepted
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn for review
    When the contest director rejects the draw because "a late entry arrived"
    And a latecomer registers while no draw stands
    And the contest director redraws the preliminary phase
    Then the redrawn field holds 7 competitors
    When the contest director accepts the draw
    Then the competition reads as having an accepted draw

  Scenario: A draw cannot be rejected once flights are recorded against it
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn and accepted
    And an entry has been opened and a flight recorded for competitor 1 in round 1, group 1
    When the contest director tries to reject the draw because "changed my mind"
    Then the rejection is refused because entries exist against the phase

  Scenario: An entry cannot be opened before the draw is accepted
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn but not accepted
    When the scorer tries to open an entry for competitor 2 in round 1, group 1
    Then the open is refused because the draw is not yet accepted
    When the contest director accepts the draw
    And the scorer now opens an entry for competitor 2 in round 1, group 1
    Then that entry appears in the index for round 1, group 1

  Scenario: Withdrawing a competitor after acceptance leaves the draw intact
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn for review
    When the contest director accepts the draw
    And competitor 2 withdraws from the competition
    Then the competition still reads as having an accepted draw
