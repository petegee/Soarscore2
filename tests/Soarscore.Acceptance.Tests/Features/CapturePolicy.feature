Feature: Capture policy
  Who may enter which scores is per-competition configuration (D10): the
  organiser can always enter, the default keeps capture with the organisers,
  and a competition may widen it to named capturers. The policy reads
  configuration and the principal, never what has been captured (NFR-4) —
  reconfiguration mid-contest changes who may enter, never what was entered.

  Background:
    Given Pete has signed in
    And an F5J competition Pete created with 2 registered competitors
    And the organiser drew a preliminary phase of 2 rounds and accepted it
    And an entry opened for competitor 1 in round 1, group 1 with its first flight open

  Scenario: The default policy keeps capture with the organisers
    When Tama captures flightTime of 412 seconds on the entry
    Then the response is 403 refusing with auth.capturePolicy.denied
    When Pete captures flightTime of 412 seconds on the entry
    Then the capture is accepted

  Scenario: An allow-listed competitor captures; an unlisted person does not
    When Pete allow-lists Tama for capture
    And Tama captures flightTime of 412 seconds on the entry
    Then the capture is accepted
    When FieldRig captures flightTime of 300 seconds on the entry
    Then the response is 403 refusing with auth.capturePolicy.denied

  Scenario: A late capture is recorded, never gated on round currency
    When Pete allow-lists Tama for capture
    And Tama opens flight 2 on the entry
    And Tama captures flightTime of 380 seconds on flight 2 of the entry
    And Tama captures flightTime of 412 seconds on the entry
    Then the entry holds two flights with flightTimes 412 and 380
