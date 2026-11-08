Feature: Subscription plans
  As a rancher or a veterinarian
  I want to see the available plans with their prices
  So that I can choose the one that fits my work (US-060)

  Scenario Outline: A signed-in user sees the plans with their prices
    Given "Ana" is signed in as a "<role>"
    When "Ana" lists the subscription plans
    Then the response status is 200
    And the plan "Ganadero Basico" costs 49
    And the plan "Ganadero Pro" costs 99
    And the plan "Veterinario Profesional" costs 129

    Examples:
      | role         |
      | Rancher      |
      | Veterinarian |

  Scenario: A visitor without a token cannot list the plans
    Given a visitor without a token
    When the visitor lists the subscription plans
    Then the response status is 401

  Scenario: A token signed with another secret is rejected
    Given a visitor with a token signed with another secret
    When the visitor lists the subscription plans
    Then the response status is 401

  Scenario: An expired token is rejected
    Given a visitor with an expired token
    When the visitor lists the subscription plans
    Then the response status is 401

  Scenario: A role that the platform does not know has no access
    Given "Zoe" is signed in as a "Hacker"
    When "Zoe" lists the subscription plans
    Then the response status is 403
