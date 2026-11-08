Feature: Subscription and payments belong to their owner
  As a rancher or a veterinarian
  I want to see my active subscription and my payment history, and nobody else's
  So that my billing data stays private (US-061, US-063, QAS-08)

  Background:
    Given "Ana" is signed in as a "Rancher"
    And "Luis" is signed in as a "Rancher"

  Scenario: A user sees their own active subscription
    Given "Ana" has paid the plan "Ganadero Pro"
    When "Ana" asks for the active subscription of "Ana"
    Then the response status is 200
    And the subscription is for the plan "Ganadero Pro"
    And the subscription status is "Active"

  Scenario: A user sees their own payment history
    Given "Ana" has paid the plan "Ganadero Pro"
    When "Ana" asks for the payments of "Ana"
    Then the response status is 200
    And the list has 1 items
    And the first payment is 99 PEN with status "Paid"

  Scenario: A user without payments has no active subscription
    When "Luis" asks for the active subscription of "Luis"
    Then the response status is 404

  Scenario: Another user cannot see the active subscription
    Given "Ana" has paid the plan "Ganadero Pro"
    When "Luis" asks for the active subscription of "Ana"
    Then the response status is 404

  Scenario: Another user cannot see the payment history
    Given "Ana" has paid the plan "Ganadero Pro"
    When "Luis" asks for the payments of "Ana"
    Then the response status is 404

  Scenario: Another user cannot open a subscription by its id
    Given "Ana" has paid the plan "Ganadero Basico"
    When "Luis" asks for the subscription of "Ana" by id
    Then the response status is 404
    And "Ana" can open the subscription of "Ana" by id

  Scenario: The list of subscriptions only shows the ones of the caller
    Given "Ana" has paid the plan "Ganadero Basico"
    When "Luis" lists the subscriptions
    Then the response status is 200
    And the list has 0 items
