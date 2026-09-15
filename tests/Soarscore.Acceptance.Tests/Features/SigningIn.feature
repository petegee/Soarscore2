Feature: Signing in
  A first sign-in is how a caller becomes a person in Soarscore (D5's
  get-or-create): identity link first, email second, creation last. Nobody
  types a password — the bearer token stands for the validated identity, and
  who that identity is to Soarscore is resolved from the event store.

  Scenario: An anonymous caller is refused before anything else
    When an anonymous caller posts to /link-sign-in
    Then the response is 401 refusing with auth.notAuthenticated

  Scenario: A first sign-in creates the person and links the identity
    When Aroha signs in
    Then the sign-in creates the person
    And /who-am-i resolves the newcomer to that person holding no roles

  Scenario: Repeating a sign-in is idempotent
    Given Aroha has signed in
    When Aroha signs in again
    Then the sign-in does not create a person
    And returns the same person

  Scenario: A second provider with the same email links to the existing person
    Given Aroha has signed in
    When the same email signs in through google-oauth2
    Then the sign-in does not create a person
    And returns the same person

  Scenario: A bootstrap-listed email lands with the Organiser role
    When Nova signs in
    Then the sign-in does not create a person
    And /who-am-i shows Nova holding the Organiser role
