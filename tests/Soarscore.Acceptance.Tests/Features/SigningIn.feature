Feature: Signing in
  A first sign-in is how a caller becomes a person in Soarscore (D5's
  get-or-create): identity link first, email second — but only onto a person
  with no sign-in yet (secure-automatic-identity-linking.md) — creation last.
  Nobody types a password — the bearer token stands for the validated
  identity, and who that identity is to Soarscore is resolved from the event
  store.

  Scenario: An anonymous caller is refused before anything else
    When an anonymous caller posts to /link-sign-in
    Then the response is 401 refusing with auth.notAuthenticated

  Scenario: A first sign-in creates the person and links the identity
    When Aroha signs in
    Then the sign-in returns the person
    And /who-am-i resolves the newcomer to that person holding no roles

  Scenario: Repeating a sign-in is idempotent
    Given Aroha has signed in
    When Aroha signs in again
    Then the sign-in returns the person
    And returns the same person

  Scenario: A first sign-in links to an organiser pre-registered person
    Given an organiser has pre-registered a person under Aroha's email
    When Aroha signs in
    Then the sign-in returns the pre-registered person
    And /who-am-i resolves the newcomer to that person holding no roles

  Scenario: A second provider is refused when the person already has a sign-in
    Given Aroha has signed in
    When the same email signs in through google-oauth2
    Then the response is 409 refusing with auth.signIn.explicitLinkRequired
    And /who-am-i resolves the refused identity to no person

  Scenario: A contact-email change cannot claim the bootstrap organiser's address
    Given Aroha has signed in
    When Aroha sets their contact email to the bootstrap organiser's address
    Then the response is 403 refusing with auth.contact.emailOwnership
    When Nova signs in
    Then /who-am-i shows Nova holding the Organiser role
    And /who-am-i resolves the newcomer to that person holding no roles

  Scenario: A bootstrap-listed email lands with the Organiser role
    When Nova signs in
    Then the sign-in returns the person
    And /who-am-i shows Nova holding the Organiser role

  Scenario: An unverified email claim cannot sign in
    When a caller whose token email is not verified signs in
    Then the response is 403 refusing with auth.signIn.emailNotVerified
