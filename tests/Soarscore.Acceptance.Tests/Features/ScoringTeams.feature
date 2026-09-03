Feature: Scoring teams through a live contest
  A competition with scoring teams, a protection group and a team
  classification runs its whole life while the field's scores trickle in —
  and they trickle in out of order across rounds, because nobody waits for
  the scoreboard (NFR-4). Team standings must be readable and correct from
  the first capture onwards, built from whatever scores are present, and
  finalisation must freeze exactly the standings that stood at that moment.

  Competitor 1 is the fixture's defending champion: drawn into the Harriers
  alongside their team mates but entered without contributing, so however
  well they fly — and they fly fastest — their result never counts toward
  the Harriers' total.

  Scenario: Team standings stay correct while scores trickle in out of order across rounds
    Given an F5J competition with 6 registered competitors
    And the scoring teams are defined with these memberships
      | team     | competitor | contributes |
      | Falcons  | 3          | yes         |
      | Falcons  | 4          | yes         |
      | Falcons  | 5          | yes         |
      | Harriers | 1          | no          |
      | Harriers | 2          | yes         |
      | Harriers | 6          | yes         |
    And team classification is enabled with the bestThreeScoreSum method
    And a protection group pairs competitors 2 and 5
    And the preliminary phase is drawn for 4 rounds and accepted
    Then the protection diagnostics name the paired competitors in all 4 rounds
    When the first six scores trickle in, round 2's before round 1's
    Then the team standings derive correctly from those partial scores
    When nine more trickle in, still skipping across rounds
    Then the team standings derive correctly from those partial scores
    And every standings read so far has matched the scores present at its moment
    When the last nine trickle in, completing the field
    Then the finished standings carry the full evidence, contributors and tie-breaks included
    And every standings read after every capture has matched the scores present at its moment
    When the contest director closes the flown rounds and finalises the competition
    Then the declared team results equal the derived standings at that moment
