Feature: Machine actors
  A client-credentials token makes the client application the actor (D12):
  a sub without | resolves to provider client-credentials, subject the client
  id — one identity row per client, the machine just a person ("Field clock
  rig") the organiser binds. Authority is competition configuration — roles
  and the capture-policy allow-list — never token scopes.

  Background:
    Given Pete has signed in
    And an F5J competition Pete created with 2 registered competitors
    And the organiser drew a preliminary phase of 2 rounds and accepted it
    And an entry opened for competitor 1 in round 1, group 1 with its first flight open

  Scenario: A machine actor is an authenticated principal with no person
    When the field-clock-rig client reads /who-am-i
    Then the machine is authenticated but linked to nobody and holds no roles

  Scenario: A machine client captures once an organiser binds and allow-lists it
    When the field-clock-rig client captures flightTime of 412 seconds on the entry
    Then the response is 403 refusing with auth.capturePolicy.denied
    When Pete registers a person named Field clock rig and binds the machine identity to it
    And Pete allow-lists the machine person for capture
    And the field-clock-rig client captures flightTime of 412 seconds on the entry
    Then the capture is accepted

  Scenario: A bound machine client is still refused where it is not allow-listed
    When Pete registers a person named Field clock rig and binds the machine identity to it
    And the field-clock-rig client captures flightTime of 412 seconds on the entry
    Then the response is 403 refusing with auth.capturePolicy.denied
