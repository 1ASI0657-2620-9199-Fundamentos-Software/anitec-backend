Feature: Payment notifications from Stripe
  As the platform
  I want to record a payment when Stripe notifies it with a valid signature
  So that the subscription is activated even if the user closes the browser (US-062, TS-016)

  Background:
    Given "Ana" is signed in as a "Rancher"

  Scenario: A signed notification of a paid checkout stores the subscription and the payment
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    And "Ana" paid that checkout
    When Stripe notifies the payment of the checkout of "Ana"
    Then the response status is 200
    And the notification is reported as handled
    And "Ana" has 1 payments and 1 subscriptions
    And the payment of "Ana" is 99 PEN paid through "StripeTest"
    And the subscription status of "Ana" is "Active"

  Scenario: The same notification delivered twice is stored once
    Given "Ana" opened a checkout for the plan "Ganadero Basico"
    And "Ana" paid that checkout
    When Stripe notifies the payment of the checkout of "Ana"
    And Stripe notifies the same payment again
    Then the response status is 200
    And the notification is reported as already handled
    And "Ana" has 1 payments and 1 subscriptions

  Scenario: A notification with a forged signature is rejected
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    And "Ana" paid that checkout
    When someone notifies the payment of the checkout of "Ana" with a forged signature
    Then the response status is 400
    And "Ana" has 0 payments and 0 subscriptions

  Scenario: A notification whose body changed after signing is rejected
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    And "Ana" paid that checkout
    When someone changes the body of the notification of the checkout of "Ana" after signing it
    Then the response status is 400
    And "Ana" has 0 payments and 0 subscriptions

  Scenario: A notification without signature is rejected
    Given "Ana" opened a checkout for the plan "Ganadero Pro"
    And "Ana" paid that checkout
    When someone notifies the payment of the checkout of "Ana" without a signature
    Then the response status is 400

  Scenario: A notification of another type is acknowledged and ignored
    Given "Ana" opened a checkout for the plan "Ganadero Basico"
    And "Ana" paid that checkout
    When Stripe sends a notification of type "invoice.paid" for the checkout of "Ana"
    Then the response status is 200
    And the notification is reported as ignored
    And "Ana" has 0 payments and 0 subscriptions

  Scenario: A completed checkout that was not paid is ignored
    Given "Ana" opened a checkout for the plan "Ganadero Basico"
    When Stripe notifies the payment of the checkout of "Ana"
    Then the response status is 200
    And the notification is reported as ignored
    And "Ana" has 0 payments and 0 subscriptions

  Scenario: Without the webhook secret the service answers that it is unavailable
    Given Stripe has no keys configured
    When someone notifies an empty event with any signature
    Then the response status is 503
