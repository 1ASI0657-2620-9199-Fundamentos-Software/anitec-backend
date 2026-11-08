Feature: Paying a plan with Stripe in test mode
  As a rancher or a veterinarian
  I want to pay a plan on the payment page of Stripe
  So that my subscription becomes active (US-062)

  Background:
    Given "Ana" is signed in as a "Rancher"
    And "Luis" is signed in as a "Rancher"

  Scenario: The amount comes from the plan and the buyer from the token
    When "Ana" starts a checkout for the plan "Veterinario Profesional"
    Then the response status is 200
    And the answer has the address of the payment page
    And Stripe was asked to charge 129 PEN to "Ana" for the plan "Veterinario Profesional"

  Scenario: A plan that does not exist cannot be paid
    When "Ana" starts a checkout for the plan with id 999
    Then the response status is 404

  Scenario: Without Stripe keys the checkout answers that the service is unavailable
    Given Stripe has no keys configured
    When "Ana" starts a checkout for the plan "Ganadero Basico"
    Then the response status is 503
    And the answer does not reveal the Stripe configuration

  Scenario: When Stripe is not reachable the checkout answers a bad gateway
    Given Stripe is not reachable
    When "Ana" starts a checkout for the plan "Ganadero Basico"
    Then the response status is 502

  Scenario: Confirming a paid checkout stores the subscription and the payment once
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    And "Ana" paid that checkout
    When "Ana" confirms the checkout twice
    Then both answers have status 200
    And "Ana" has 1 payments and 1 subscriptions

  Scenario: Confirming a checkout that was not paid stores nothing
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    When "Ana" confirms the checkout
    Then the response status is 400
    And "Ana" has 0 payments and 0 subscriptions

  Scenario: Another user cannot confirm the checkout of someone else
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    And "Ana" paid that checkout
    When "Luis" confirms the checkout of "Ana"
    Then the response status is 404
    And "Luis" has 0 payments and 0 subscriptions

  Scenario: A session that does not exist is not found
    When "Ana" confirms a checkout that does not exist
    Then the response status is 404

  Scenario: A paid session that this service did not open is never attached to the caller
    Given Stripe has a paid session that this service did not open
    When "Ana" confirms that session
    Then the response status is 404
    And "Ana" has 0 payments and 0 subscriptions

  Scenario: A better plan closes the previous subscription
    Given "Ana" has paid the plan "Ganadero Basico"
    And "Ana" has paid the plan "Veterinario Profesional"
    Then "Ana" has 2 payments and 2 subscriptions
    And the active subscription of "Ana" is for the plan "Veterinario Profesional"
    And the subscription for the plan "Ganadero Basico" of "Ana" is closed
