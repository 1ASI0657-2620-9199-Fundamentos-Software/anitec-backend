@xunit:collection(gateway)
Feature: Contract of the API gateway
  As a client of the platform
  I want every request to be traceable and unsupported API versions to be rejected with a clear error
  So that problems can be diagnosed and old clients get a documented answer (TS-025)

  Background:
    Given the gateway is running with two replicas of the core and the Subscriptions service

  Scenario: A request without a correlation id gets one, and it reaches the service and comes back
    When a client sends GET "/api/v1/animals"
    Then the answer carries a correlation id
    And the service received that same correlation id

  Scenario: A well-formed correlation id sent by the caller is kept
    When a client sends GET "/api/v1/subscription-plans" with the correlation id "trace-2026.11_08"
    Then the answer carries the correlation id "trace-2026.11_08"
    And the service received that same correlation id

  Scenario Outline: A malformed correlation id is replaced
    When a client sends GET "/api/v1/animals" with the correlation id "<sent>"
    Then the answer carries a correlation id different from "<sent>"

    Examples:
      | sent            |
      | has spaces here |
      | semi;colon      |
      | <script>        |

  Scenario Outline: An unsupported API version gets a documented error and is not routed
    When a client sends GET "<path>"
    Then the response status is 400
    And the answer is the documented error for the unsupported version "<version>"
    And the core received nothing
    And the Subscriptions service received nothing

    Examples:
      | path                        | version |
      | /api/v2/subscriptions       | v2      |
      | /api/v2/animals             | v2      |
      | /api/v10/subscription-plans | v10     |

  Scenario: A supported version is routed
    When a client sends GET "/api/v1/animals"
    Then the response status is 200
    And the request was served by the "core" service

  Scenario: The own answer of a service is never replaced by the gateway
    When a client sends GET "/api/v1/subscriptions/force-503"
    Then the response status is 503
    And the answer is the one of the "subscriptions" service
