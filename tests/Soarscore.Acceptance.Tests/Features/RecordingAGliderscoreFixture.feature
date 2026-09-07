@gliderscore
Feature: Recording a GliderScore fixture as a literal record
  A second kind of GliderScore replay scenario, beside
  ReplayingAGliderscoreFixture's JSON-driven ones (kanban backlog story
  literal-record-replay-scenarios): the competition itself is visible in the
  feature file. One "<pilot> enters his scores" block per pilot carries the
  whole draw — real flights as Time/Landing rows, unflown slots as explicit
  "no flight" markers — hand-authored, and the scenario closes with
  completion/finalise steps and a literal placings table in GliderScore's own
  notation ("=n" tie strings). Blocks are ordered by final placing, winner
  first; entering out of draw order, across all rounds before any round
  completes, demonstrates NFR-4's no-imposed-ordering on score capture.
  The JSON harness scenario for this same fixture stays the referee: its
  three-grain exact comparison runs at the end of this scenario too.

  Scenario: ALES sample comp — ten pilots, three rounds, one flown
    Given the GliderScore fixture "ales-sample-comp" is loaded for literal recording
    And its class definition is published and a competition created
    And its 10 pilots are registered under their fixture names
    And the draw is prescribed as 3 rounds of one group in flying order and accepted
    When Ken ZZFox enters his scores
      | Round | Time      | Landing |
      | 1     | 5:00      | 5       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Chris ZZBarrenger enters his scores
      | Round | Time      | Landing |
      | 1     | 4:00      | 4       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Theo ZZArvanitakis enters his scores
      | Round | Time      | Landing |
      | 1     | 2:00      | 3       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Jim ZZHoudalakis enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Carl ZZStrautins enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Mike ZZO'Reilly enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And David ZZPratley enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Greg ZZPotter enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Jamie ZZNancarrow enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    And Jeff ZZIrvin enters his scores
      | Round | Time      | Landing |
      | 1     | no flight | —       |
      | 2     | no flight | —       |
      | 3     | no flight | —       |
    Then the entered tables match the fixture's scores-raw exactly, cell for cell
    And round 1 is completed and scored
    And round 2 is completed and scored
    And round 3 is completed and scored
    And the competition is finalised
    And the final placings are
      | Place | Name               | Score | Dropped |
      | 1     | Ken ZZFox          | 1030  | —       |
      | 2     | Chris ZZBarrenger  | 835   | —       |
      | 3     | Theo ZZArvanitakis | 440   | —       |
      | =4    | Jim ZZHoudalakis   | 0     | —       |
      | =4    | Carl ZZStrautins   | 0     | —       |
      | =4    | Mike ZZO'Reilly    | 0     | —       |
      | =4    | David ZZPratley    | 0     | —       |
      | =4    | Greg ZZPotter      | 0     | —       |
      | =4    | Jamie ZZNancarrow  | 0     | —       |
      | =4    | Jeff ZZIrvin       | 0     | —       |
    And the three-grain oracle comparison over the same competition still runs exact
