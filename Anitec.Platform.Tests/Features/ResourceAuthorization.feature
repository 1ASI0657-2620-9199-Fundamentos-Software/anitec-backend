Feature: Authorization by resource ownership
  As a rancher or a veterinarian
  I want the server to return only the data that belongs to me or that I was authorized to see
  So that nobody can read the information of another owner by calling the API directly (QAS-08, US-013, US-014, US-073)

  Background:
    Given the rancher "Ana" with id 1 owns the herd "Los Alamos" with 2 animals
    And the rancher "Luis" with id 2 owns the herd "El Molino" with 3 animals

  Scenario: A rancher only sees the animals of its own herds
    When user 1 with role "Rancher" lists the animals
    Then 2 animals are returned

  Scenario: A rancher cannot read the detail of an animal of another rancher
    When user 1 with role "Rancher" asks for an animal of rancher 2
    Then the animal is not found

  Scenario: A veterinarian without a relationship sees no animals
    When user 10 with role "Veterinarian" lists the animals
    Then 0 animals are returned

  Scenario Outline: A relationship that is not accepted gives no access
    Given veterinarian 10 has a "<status>" relationship with rancher 1
    When user 10 with role "Veterinarian" lists the animals
    Then 0 animals are returned

    Examples:
      | status   |
      | Pending  |
      | Rejected |

  Scenario: An accepted relationship gives access only to the animals of that rancher
    Given veterinarian 10 has a "Accepted" relationship with rancher 1
    When user 10 with role "Veterinarian" lists the animals
    Then 2 animals are returned

  Scenario: The owner keeps seeing the health events registered by the veterinarian
    Given veterinarian 10 has a "Accepted" relationship with rancher 1
    And a health event exists for an animal of rancher 1
    Then user 1 with role "Rancher" sees 1 health events
    And user 10 with role "Veterinarian" sees 1 health events
    And user 2 with role "Rancher" sees 0 health events

  Scenario: A veterinarian loses access to the health events when the relationship is not accepted
    Given veterinarian 10 has a "Pending" relationship with rancher 1
    And a health event exists for an animal of rancher 1
    Then user 10 with role "Veterinarian" sees 0 health events

  Scenario: Financial records are private to the rancher that owns them
    Given a financial record exists for rancher 1
    Then user 1 with role "Rancher" sees 1 financial records
    And user 2 with role "Rancher" sees 0 financial records
    And user 10 with role "Veterinarian" sees 0 financial records

  Scenario: A rancher cannot register animals in the herd of another rancher
    Then user 1 with role "Rancher" does not own the herd of rancher 2
    And user 1 with role "Rancher" owns its own herd
