Feature: Authorization of users and profiles
  As a rancher or a veterinarian
  I want to see only myself and the users I have a relationship with, and only the profiles I created
  So that nobody lists the identity and contact data of every user by calling the API directly (QAS-08)

  Background:
    Given the user "Ana" with id 1 and role "Rancher"
    And the user "Luis" with id 2 and role "Rancher"
    And the user "Pedro" with id 10 and role "Veterinarian"
    And the user "Rosa" with id 11 and role "Veterinarian"

  Scenario: A user without relationships only sees itself
    When user 1 with role "Rancher" lists the users
    Then the users returned are "Ana"

  Scenario Outline: A pending or accepted relationship makes both users visible to each other
    Given veterinarian 10 has the "<status>" connection with rancher 1
    When user 1 with role "Rancher" lists the users
    Then the users returned are "Ana, Pedro"
    When user 10 with role "Veterinarian" lists the users
    Then the users returned are "Ana, Pedro"

    Examples:
      | status   |
      | Pending  |
      | Accepted |

  Scenario: A rejected relationship does not expose the other user
    Given veterinarian 10 has the "Rejected" connection with rancher 1
    When user 1 with role "Rancher" lists the users
    Then the users returned are "Ana"
    When user 10 with role "Veterinarian" lists the users
    Then the users returned are "Pedro"

  Scenario: A relationship does not expose the other users of the counterpart
    Given veterinarian 10 has the "Accepted" connection with rancher 1
    And veterinarian 11 has the "Accepted" connection with rancher 2
    When user 10 with role "Veterinarian" lists the users
    Then the users returned are "Ana, Pedro"

  Scenario Outline: The detail of a user is visible only for oneself or a related user
    Given veterinarian 10 has the "Accepted" connection with rancher 1
    When user <caller> with role "<role>" asks for the user <target>
    Then the user is <result>

    Examples:
      | caller | role         | target | result    |
      | 1      | Rancher      | 1      | found     |
      | 1      | Rancher      | 10     | found     |
      | 1      | Rancher      | 2      | not found |
      | 1      | Rancher      | 11     | not found |
      | 10     | Veterinarian | 1      | found     |
      | 10     | Veterinarian | 2      | not found |
      | 11     | Veterinarian | 1      | not found |

  Scenario: A user only sees the profiles it created
    Given user 1 created the profile "Ana Perez"
    And user 2 created the profile "Luis Rojas"
    When user 1 with role "Rancher" lists the profiles
    Then the profiles returned are "Ana Perez"

  Scenario: A user cannot read the profile of another user by id
    Given user 2 created the profile "Luis Rojas"
    When user 1 with role "Rancher" asks for the profile "Luis Rojas"
    Then the profile is not found

  Scenario: A user can read its own profile by id
    Given user 1 created the profile "Ana Perez"
    When user 1 with role "Rancher" asks for the profile "Ana Perez"
    Then the profile is found

  Scenario: A profile without an owner is visible to nobody
    Given a profile "Legacy Profile" exists without an owner
    When user 1 with role "Rancher" lists the profiles
    Then the profiles returned are ""
