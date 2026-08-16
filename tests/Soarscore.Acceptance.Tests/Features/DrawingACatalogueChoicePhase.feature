Feature: Drawing a catalogue-choice phase
  Some classes (F3K, F5K) leave the task for each round to be chosen from
  the class's published catalogue, rather than fixing one task for every
  round in advance (F3K.11 preamble: "the tasks to be flown for the day
  must be announced by the organiser before the start of the contest").
  The CD names the task for every round as part of the draw itself.

  Scenario: The CD names a task for every round and the draw honours it
    Given the F3K class is published and adopted by a competition with 6 registered competitors
    When the CD draws the preliminary phase naming these tasks
      | round | task |
      | 1     | A    |
      | 2     | B    |
      | 3     | C    |
      | 4     | D    |
      | 5     | E    |
    Then each round is scheduled with its named task

  Scenario: Naming the same task twice where the rules require distinct tasks is refused
    Given the F3K class is published and adopted by a competition with 6 registered competitors
    When the CD attempts to draw the preliminary phase naming these tasks
      | round | task |
      | 1     | A    |
      | 2     | A    |
    Then the draw is refused because the tasks are not distinct
