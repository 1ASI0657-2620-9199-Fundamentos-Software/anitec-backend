Feature: User authentication
  As a registered user of AniTec
  I want to sign in with my credentials
  So that I can access the capabilities of my role (US-053)

  Background:
    Given the following registered users
      | username    | password  | role         |
      | ganadero    | anitec123 | Rancher      |
      | veterinaria | anitec123 | Veterinarian |

  Scenario: Successful sign in returns a token
    When I sign in with username "ganadero" and password "anitec123"
    Then the sign in succeeds
    And a token is issued for the user "ganadero"

  Scenario: A wrong password is rejected
    When I sign in with username "ganadero" and password "incorrecta"
    Then the sign in fails with the error "InvalidCredentials"
    And no token is issued

  Scenario: An unknown user is rejected with the same error
    When I sign in with username "fantasma" and password "anitec123"
    Then the sign in fails with the error "InvalidCredentials"
    And no token is issued

  Scenario: A new user can register and then sign in
    Given I sign up as "maria" with password "anitec123", full name "Maria Quispe" and role "Rancher"
    When I sign in with username "maria" and password "anitec123"
    Then the sign in succeeds
    And a token is issued for the user "maria"

  Scenario: A username that is already taken cannot be registered again
    When I sign up as "ganadero" with password "otra" and full name "Otro" and role "Rancher"
    Then the sign up fails with the error "UsernameAlreadyTaken"

  Scenario Outline: Only the supported roles can be registered
    When I sign up as "nuevo" with password "anitec123" and full name "Nuevo" and role "<role>"
    Then the sign up <outcome>

    Examples:
      | role         | outcome                                |
      | Rancher      | succeeds                               |
      | Veterinarian | succeeds                               |
      | Admin        | fails with the error "InvalidRole"     |
