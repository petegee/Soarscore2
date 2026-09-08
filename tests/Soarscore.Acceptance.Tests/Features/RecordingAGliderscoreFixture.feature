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

  Scenario: F3K sample comp — ten pilots, nine rounds, six tasks, live drop and penalties
    Given the GliderScore fixture "f3k-sample-comp" is loaded for literal recording
    And its class definition is published and a competition created
    And its 10 pilots are registered under their fixture names
    And the draw is prescribed as 9 rounds of one group in flying order and accepted
    When David ZZPratley enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 2:00   | 1:45   | 1:58   | 1:43   | 1:22   | —      | —      | —       |
      | 2     | A(1)  | 4:53   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:45   | 2:56   | 2:54   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | 1:45   | —      | —       |
      | 5     | C(3)  | 0:03   | 0:03   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Jim ZZHoudalakis enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 0:45   | 0:56   | 1:18   | 1:56   | 0:43   | —      | —      | —       |
      | 2     | A(1)  | 5:00   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:25   | 2:45   | 3:12   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:01   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Chris ZZBarrenger enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:32   | 1:44   | 1:46   | 1:46   | 1:59   | —      | —      | —       |
      | 2     | A(1)  | 4:32   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 2:31   | 2:59   | 2:21   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | —      | —      | —      | —      | —       |
      | 5     | C(3)  | 0:02   | 0:01   | 0:01   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Theo ZZArvanitakis enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 0:29   | 0:31   | 0:48   | 0:50   | 1:02   | —      | —      | —       |
      | 2     | A(1)  | 2:11   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 2:55   | 2:12   | 2:12   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | 1:45   | 2:00   | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Jeff ZZIrvin enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:23   | 1:43   | 1:43   | 2:00   | 0:22   | —      | —      | —       |
      | 2     | A(1)  | 3:12   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 2:20   | 3:00   | 2:12   | —      | —      | —      | —      | 100     |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | —      | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Mike ZZO'Reilly enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 2:01   | 1:55   | 1:34   | 1:20   | 1:10   | —      | —      | —       |
      | 2     | A(1)  | 1:56   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:22   | 2:11   | 2:10   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:02   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Ken ZZFox enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:20   | 0:02   | 1:54   | 0:58   | 1:19   | —      | —      | —       |
      | 2     | A(1)  | 4:23   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 3:00   | 1:30   | 2:20   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | —      | —      | —      | —      | —       |
      | 5     | C(3)  | 0:03   | 0:01   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Jamie ZZNancarrow enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 1:32   | 1:35   | 1:43   | 1:48   | 2:00   | —      | —      | —       |
      | 2     | A(1)  | 0:12   | —      | —      | —      | —      | —      | —      | 100     |
      | 3     | F     | 2:43   | 1:54   | 2:33   | —      | —      | —      | —      | 100     |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | 1:30   | 1:45   | —      | —       |
      | 5     | C(3)  | 0:03   | 0:03   | 0:03   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Carl ZZStrautins enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 0:45   | 0:55   | 1:05   | 1:15   | 1:25   | —      | —      | —       |
      | 2     | A(1)  | 2:18   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 3:03   | 2:34   | 2:16   | —      | —      | —      | —      | —       |
      | 4     | D     | 0:30   | 0:45   | 1:00   | 1:15   | —      | —      | —      | —       |
      | 5     | C(3)  | 0:02   | 0:03   | 0:02   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    And Greg ZZPotter enters his scores
      | Round | Task  | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 | Slot 6 | Slot 7 | Penalty |
      | 1     | G     | 2:00   | 2:00   | 2:00   | 2:00   | 1:50   | —      | —      | 100     |
      | 2     | A(1)  | 4:18   | —      | —      | —      | —      | —      | —      | —       |
      | 3     | F     | 1:56   | 0:50   | 1:12   | —      | —      | —      | —      | —       |
      | 4     | D     | zero (unrecorded) | — | —   | —      | —      | —      | —      | —       |
      | 5     | C(3)  | 0:02   | 0:02   | 0:01   | 0:02   | 0:02   | —      | —      | —       |
      | 6     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 7     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 8     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
      | 9     | X     | no flight | —   | —      | —      | —      | —      | —      | —       |
    Then the entered tables match the fixture's scores-raw exactly, cell for cell
    And round 1 is completed and scored
    And round 2 is completed and scored
    And round 3 is completed and scored
    And round 4 is completed and scored
    And round 5 is completed and scored
    And round 6 is completed and scored
    And round 7 is completed and scored
    And round 8 is completed and scored
    And round 9 is completed and scored
    And the competition is finalised
    And the final placings are
      | Place | Name               | Score | Dropped | Penalty |
      | 1     | David ZZPratley    | 4609  | Rnd9    | 0       |
      | 2     | Jim ZZHoudalakis   | 3749  | Rnd9    | 0       |
      | 3     | Chris ZZBarrenger  | 3672  | Rnd9    | 0       |
      | 4     | Theo ZZArvanitakis | 3588  | Rnd9    | 0       |
      | 5     | Jeff ZZIrvin       | 3477  | Rnd9    | 100     |
      | 6     | Mike ZZO'Reilly    | 3421  | Rnd9    | 0       |
      | 7     | Ken ZZFox          | 3414  | Rnd9    | 0       |
      | 8     | Jamie ZZNancarrow  | 3402  | Rnd9    | 200     |
      | 9     | Carl ZZStrautins   | 3255  | Rnd9    | 0       |
      | 10    | Greg ZZPotter      | 2957  | Rnd9    | 100     |
    And the three-grain oracle comparison over the same competition still runs exact
