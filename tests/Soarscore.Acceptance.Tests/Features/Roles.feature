Feature: Roles
  Roles are granted and revoked by organisers — organiser-granted authority,
  never a token claim (D2). The v1 set is Competitor and Organiser, and a
  principal's roles are resolved from the read model per request, so a revoke
  bites on the very next call with no token-refresh dance.

  Scenario: An organiser grants and revokes the Competitor role
    Given FieldRig has signed in
    When Pete grants FieldRig the Competitor role
    Then /who-am-i shows FieldRig holding the Competitor role
    When Pete revokes FieldRig's Competitor role
    Then /who-am-i shows FieldRig holding no roles

  Scenario: A competitor cannot grant roles
    Given FieldRig has signed in
    When Tama attempts to grant FieldRig the Competitor role
    Then the response is 403 refusing with auth.forbidden

  Scenario: A competitor cannot read the roster
    Given Tama has signed in
    When Tama reads /people
    Then the response is 403 refusing with auth.forbidden

  Scenario: Revoking the last organiser is refused
    Given Nova has signed in
    When Pete revokes Nova's Organiser role
    When Pete attempts to revoke his own Organiser role
    Then the response is 409 refusing with person.lastOrganiser

  Scenario: A revoked competitor keeps the self-half and loses the organiser-half
    Given Tama has signed in
    And FieldRig has signed in
    When Pete grants FieldRig the Competitor role
    And FieldRig renames herself
    Then the rename is accepted
    When Pete revokes FieldRig's Competitor role
    And FieldRig renames herself again
    Then the rename is accepted
    When FieldRig renames Tama
    Then the response is 403 refusing with auth.forbidden
