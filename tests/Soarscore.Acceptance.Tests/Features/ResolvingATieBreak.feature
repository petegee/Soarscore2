Feature: Resolving a tie-break
  Some classes state an operational tie-break rung the engine cannot resolve
  itself — a flown fly-off (F3K.10.2: "a separate fly-off ... will be flown to
  achieve a ranking") or, where the rulebook states classification and stops, a
  CD ruling (F5L 5.5.12.12). The ranking halts honestly with the tie intact,
  sharing the places and surfacing the requirement as data — and the CD's
  outcome, once flown or ruled in the real world, is recorded against that
  halted group and re-ranks it. Recording never gates and is never gated: a
  pending tie sits shared forever, and an outcome recorded against no halted
  group is inert.

  F5L (60-f5l) throughout the ruling scenarios — undefinedRequiresRuling on
  both phases, so ANY Score tie pends with no fly-off needed. F3K (10-f3k)
  for the flown scenario — tiebreakFlyoff after bestDroppedScore, a
  single-drop class where two competitors tying on Score AND dropped cell
  reach the fly-off rung.

  Scenario: The pending tie surfaces
    Given an F5L tie-break competition of 4 competitors drawn for one round
    And the round 1 field flies with the first two tied on top
    When the pending tie-breaks are requested
    Then one tie pends over the tied pair sharing place 1 under a ruling directive

  Scenario: Recording the fly-off outcome re-ranks
    Given an F3K tie-break competition of 5 competitors drawn for six catalogue rounds
    And every round is flown with the first two tied on top
    When the pending tie-breaks are requested
    Then one tie pends over the tied pair sharing place 1 under a fly-off directive
    When the CD records the fly-off ordering with the first tied competitor ahead
    Then the leaderboard places the tied pair first and second in the recorded order
    And no tie pends any longer
    And every outsider keeps their place

  Scenario: A changed mind follows the most recently recorded outcome
    Given an F5L tie-break competition of 4 competitors drawn for one round
    And the round 1 field flies with the first two tied on top
    When the CD records a ruling putting the first tied competitor ahead
    Then the leaderboard places the tied pair first and second in the recorded order
    When the CD records a ruling putting the second tied competitor ahead
    Then the recorded order is reversed on the leaderboard

  Scenario: A malformed record is refused
    Given an F5L tie-break competition of 4 competitors drawn for one round
    And the round 1 field flies with the first two tied on top
    When the CD attempts to record a tie-break outcome for an unregistered competitor
    Then the tie-break outcome is refused with recordTieBreakOutcome.competitorNotFound
    When the CD attempts to record a tie-break outcome with gapped placings
    Then the tie-break outcome is refused with recordTieBreakOutcome.placingsMalformed
    When the CD attempts to record a fly-off outcome where the class states only a ruling
    Then the tie-break outcome is refused with recordTieBreakOutcome.directiveNotStated
    Then the leaderboard is unchanged and the tie still pends

  Scenario: Nothing is gated while a tie pends
    Given an F5L tie-break competition of 4 competitors drawn for four rounds
    And rounds 1 to 3 are flown with the first two tied on top
    When the pending tie-breaks are requested
    Then one tie pends over the tied pair sharing place 1 under a ruling directive
    When round 4 is flown normally with the tie still pending
    Then the tie-break leaderboard computes
    And the tie still pends over the same pair
    When all four rounds are closed
    And the CD finalises the competition
    Then finalisation succeeds declaring the tied pair shared at place 1
