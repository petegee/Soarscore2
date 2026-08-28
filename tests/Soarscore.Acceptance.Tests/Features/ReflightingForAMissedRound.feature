Feature: Reflighting for a missed round
  A competitor hindered in one round can be allocated a re-flight flown in a
  later round's group that counts for the round they missed: the entry records
  the counts-for round, its score normalises within the group that hosted it,
  and it aggregates into the missed round's ladder slot rather than the round
  it was flown in. An Original entry always counts for its own round; the
  write side refuses the conflict shapes no witness justifies; and an ordinary
  re-flight naming no counts-for round scores exactly as it always did.

  Scenario: A make-up flight counts for the missed round, not the round it was flown in
    Given an F5J competition of 6 competitors is under way with 3 drawn rounds
    And every competitor but the make-up pilot has flown round 1
    And every competitor has flown round 2
    And every competitor has flown round 3
    When the make-up pilot flies a make-up in round 2's group counting for round 1
    Then the make-up pilot's missed round is scored at their make-up's normalised score, not a zero
    And a competitor who flew every round keeps the sum of their three round scores

  Scenario: Two make-ups flown in one round fill two missed rounds' slots
    Given an F5J competition of 6 competitors is under way with 3 drawn rounds
    And every competitor but the make-up pilot has flown round 1
    And every competitor but the make-up pilot has flown round 2
    And every competitor has flown round 3
    When the make-up pilot flies two make-ups in round 3's group, counting for rounds 1 and 2
    Then the make-up pilot's two missed rounds are scored at their make-ups' normalised scores, not zeros
    And a competitor who flew every round keeps the sum of their three round scores

  Scenario: A second make-up counting for the same missed round is refused
    Given an F5J competition of 6 competitors is under way with 2 drawn rounds
    And every competitor but the make-up pilot has flown round 1
    And every competitor has flown round 2
    And the make-up pilot has flown a make-up in round 2's group counting for round 1
    When the make-up pilot attempts a make-up in round 2's group counting for round 1
    Then the attempt is refused with openEntry.reflightAlreadyOpen

  Scenario: Write-side refusals guard a make-up's counts-for round
    Given an F3K competition of 6 competitors has 3 rounds drawn for tasks A, B and D
    When the CD attempts to open an original entry in round 2 counting for round 1
    Then the attempt is refused with openEntry.destinationOnOriginalRole
    When the CD attempts to open a make-up in round 2 counting for round 3
    Then the attempt is refused with openEntry.destinationNotEarlier
    When the CD attempts to open a make-up in round 2 counting for round 5
    Then the attempt is refused with openEntry.destinationNotFound
    When the CD attempts to open a make-up in round 2 counting for round 1 without a reason
    Then the attempt is refused with openEntry.reasonRequired

  Scenario: An original entry still requires the competitor drawn into the group
    Given an F5J competition of 12 competitors is under way with 1 drawn round
    When the CD attempts to open an original entry for the drawn pilot into the other group
    Then the attempt is refused with openEntry.competitorNotDrawn
    And the same pilot's original entry into their own drawn group is accepted

  Scenario: A make-up for a round the competitor also flew is refused
    Given an F5J competition of 6 competitors is under way with 2 drawn rounds
    And every competitor has flown round 1
    When the make-up pilot attempts a make-up in round 2's group counting for round 1
    Then the attempt is refused with openEntry.reflightDestinationTaken

  Scenario: An ordinary re-flight without a counts-for round scores exactly as before
    Given an F3K competition of 6 competitors has a single round drawn for task A
    And the whole field flies the drawn group with distinct flight times
    When the group's winner re-flies with the same group, flying a shorter time
    Then the re-flying competitor is scored on their re-flight's normalised score
    And a competitor who did not re-fly keeps their original normalised score
