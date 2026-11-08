@xunit:collection(gateway)
Feature: Routing of the API gateway
  As a client of the platform
  I want a single entry point that sends each request to the service that owns it
  So that the frontend only knows one address (TS-025)

  Background:
    Given the gateway is running with two replicas of the core and the Subscriptions service

  Scenario Outline: The paths of Subscriptions go to the Subscriptions service
    When a client sends GET "<path>"
    Then the response status is 200
    And the request was served by the "subscriptions" service
    And the core received nothing

    Examples:
      | path                                                  |
      | /api/v1/subscription-plans                            |
      | /api/v1/subscription-plans/2                          |
      | /api/v1/subscriptions                                 |
      | /api/v1/subscriptions/users/7/active                  |
      | /api/v1/subscriptions/users/7/payments                |
      | /api/v1/subscriptions/stripe-checkout/cs_test_1/confirm |

  Scenario Outline: Everything else goes to the core
    When a client sends GET "<path>"
    Then the response status is 200
    And the request was served by the "core" service
    And the Subscriptions service received nothing

    Examples:
      | path                            |
      | /api/v1/animals                 |
      | /api/v1/authentication/sign-in  |
      | /uploads/animals/cow.png        |
      | /swagger/index.html             |

  Scenario: The requests of the core are shared between the two replicas
    When a client sends GET "/api/v1/animals" 10 times
    Then both replicas of the core received requests
    And all the answers have status 200

  Scenario: The Stripe webhook reaches Subscriptions with its body and signature untouched
    When a client posts a Stripe event with the signature "t=1,v1=abc" to "/api/v1/subscriptions/stripe/webhook"
    Then the Subscriptions service received that body and that signature unchanged

  Scenario: The token is forwarded and never validated by the gateway
    When a client sends GET "/api/v1/animals" with the token "not-a-real-token"
    Then the response status is 200
    And the core received the header Authorization "Bearer not-a-real-token"
