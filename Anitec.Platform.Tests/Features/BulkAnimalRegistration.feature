Feature: Bulk animal registration in a corral
  As a rancher
  I want to register several animals of the same corral without repeating the form for each one
  So that I can organize my livestock quickly (US-082)

  Background:
    Given a corral named "Corral 1" exists in herd 1

  Scenario: Several animals are registered with sequential tags
    When I register 3 animals in that corral
    Then 3 animals are stored
    And their tags are "Corral1-001, Corral1-002, Corral1-003"

  Scenario: The tag sequence continues when the corral already has animals
    Given 2 animals were already registered in that corral
    When I register 2 animals in that corral
    Then their tags are "Corral1-003, Corral1-004"

  Scenario: The whole batch is saved in a single transaction
    When I register 500 animals in that corral
    Then 500 animals are stored
    And the changes were saved in 1 transaction

  Scenario Outline: A quantity outside the allowed range is rejected
    When I register <quantity> animals in that corral
    Then the registration fails
    And no animals are stored

    Examples:
      | quantity |
      | 0        |
      | -1       |
      | 501      |

  Scenario: A corral that does not exist is rejected
    When I register 3 animals in the corral with id 99
    Then the registration fails with the error "CorralNotFound"
    And no animals are stored

  Scenario: Several animals can be marked as sold at once
    Given 3 animals were already registered in that corral
    When I mark the animals 1, 2 and 3 as "Vendido"
    Then all 3 animals have the status "Vendido"
