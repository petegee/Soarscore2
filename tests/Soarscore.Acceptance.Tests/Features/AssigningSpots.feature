Feature: Assigning spots
  A group's field spots are explicit data, never implied by the draw's
  sequence position: the CD assigns — or re-assigns — the complete mapping
  for one group, the capture-time recording view reads it back spot-ordered,
  the assignment dies with the rejected draw, and score capture works exactly
  as before on a group with none (kanban/in-progress/lane-assignment.md WI-8;
  glossary "Spot"). The Givens are the shared draw-acceptance steps — the
  same composition AcceptingTheDraw.feature runs, ending in its accept.

  Scenario: A CD assigns field spots to a drawn group and the recording view shows them
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn and accepted
    When the contest director assigns the group's field spots 3, 1, 4, 2, 6, 5
    Then the recording view shows the group's field spots 1, 2, 3, 4, 5, 6 held by competitors 2, 4, 1, 3, 6, 5 in spot order

  Scenario: Re-assigning a group's spots replaces the previous assignment
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn and accepted
    When the contest director assigns the group's field spots 1, 2, 3, 4, 5, 6
    And the contest director re-assigns the group's field spots 60, 50, 40, 30, 20, 10
    Then the recording view shows the group's field spots 10, 20, 30, 40, 50, 60 held by competitors 6, 5, 4, 3, 2, 1 in spot order

  Scenario: Rejecting a draw discards its spot assignments; the redraw starts unassigned
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn for review
    When the contest director assigns the group's field spots 1, 2, 3, 4, 5, 6
    And the contest director rejects the draw because "the field changed"
    And the contest director redraws the preliminary phase
    Then the recording view shows a fresh, unassigned group where the assignment was
    And the recording view shows the group with no field spots assigned
    When the contest director assigns the group's field spots 10, 20, 30, 40, 50, 60
    Then the recording view shows the group's field spots 10, 20, 30, 40, 50, 60 held by competitors 1, 2, 3, 4, 5, 6 in spot order

  Scenario: A spot cannot be assigned to a withdrawn competitor
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn for review
    When competitor 2 withdraws from the competition
    And the contest director tries to assign the group's field spots 1, 2, 3, 4, 5, 6
    Then the assignment is refused because a withdrawn competitor is not a live member of the group

  Scenario: Score capture works on a group with no spots assigned
    Given a published F5J rulebook for draw acceptance
    And a draw-acceptance competition with 6 registered competitors
    And its preliminary phase has been drawn and accepted
    When the scorer opens an entry for competitor 3 in the drawn group
    And the scorer opens the entry's flight
    And the scorer captures a flight time of 412 seconds
    Then the captured flight reads back with a flight time of 412 seconds
    And the recording view shows competitor 3 as recorded
    And the recording view shows the group with no field spots assigned
