Feature: Prescribing a draw
  A draw can also be set explicitly — which competitors in which group, listed
  in flying order — instead of drawn fresh. This is how an imported competition
  reproduces its realised draw (kanban/in-progress/prescribed-draw-import.md):
  prescribing emits the very same PhaseDrawn event as drawing, so everything
  downstream — acceptance, rejection, replacement — behaves exactly as it does
  on the generated path.

  Scenario: A CD sets the groups explicitly and accepts the prescribed draw
    Given a published F5J rulebook for prescribed drawing
    And a prescribed-draw competition with 12 registered competitors
    When the contest director prescribes the preliminary phase setting these groups
      | group | flying order        |
      | 1     | 4, 2, 6, 1, 3, 5    |
      | 2     | 12, 7, 9, 11, 8, 10 |
    Then the competition reads these groups in these flying orders
      | group | flying order        |
      | 1     | 4, 2, 6, 1, 3, 5    |
      | 2     | 12, 7, 9, 11, 8, 10 |
    When the contest director accepts the prescribed draw
    Then the competition reads as having its prescribed draw accepted

  Scenario: A prescription that leaves a registered pilot unplaced is refused
    Given a published F5J rulebook for prescribed drawing
    And a prescribed-draw competition with 6 registered competitors
    When the contest director tries to prescribe the preliminary phase setting these groups
      | group | flying order  |
      | 1     | 5, 3, 1, 2, 4 |
    Then the prescription is refused because pilot 6 is left unplaced
    And the competition reads as having nothing drawn

  Scenario: A rejected prescription is replaced by a corrected one, then accepted
    Given a published F5J rulebook for prescribed drawing
    And a prescribed-draw competition with 6 registered competitors
    When the contest director prescribes the preliminary phase setting these groups
      | group | flying order     |
      | 1     | 1, 2, 3, 4, 5, 6 |
    And the contest director rejects the prescribed draw because "the imported flying order was reversed"
    And the contest director prescribes the corrected schedule
      | group | flying order     |
      | 1     | 6, 5, 4, 3, 2, 1 |
    And the contest director accepts the prescribed draw
    Then the competition reads these groups in these flying orders
      | group | flying order     |
      | 1     | 6, 5, 4, 3, 2, 1 |
    And the competition reads as having its prescribed draw accepted

  Scenario: A catalogue-choice phase is prescribed with a named task per round
    Given a published F3K rulebook for prescribed drawing
    And a prescribed-draw competition with 6 registered competitors
    When the contest director prescribes the preliminary phase naming these tasks and groups
      | round | task | flying order     |
      | 1     | A    | 2, 4, 6, 1, 3, 5 |
      | 2     | B    | 5, 1, 4, 2, 6, 3 |
    Then each prescribed round carries its named task with the field placed exactly once
