@xunit:collection(gateway)
Feature: A failing service does not drag down the others
  As an operator of the platform
  I want the failure of one service or of one replica to stay contained
  So that the rest of the system keeps answering (QAS-09)

  Background:
    Given the gateway is running with two replicas of the core and the Subscriptions service

  Scenario: When Subscriptions is down, only its routes fail and the core keeps answering
    Given the Subscriptions service stops
    When a client sends GET "/api/v1/subscription-plans" 4 times
    Then every answer is a problem document that names the "subscriptions" service
    And 20 requests to "/api/v1/animals" are all answered with status 200

  Scenario: A service that fails its health check receives no traffic and the caller gets the error at once
    Given the Subscriptions service reports itself as unhealthy
    And the gateway reports the service "subscriptions" as "Unhealthy"
    When a client sends GET "/api/v1/subscription-plans" once
    Then every answer is a problem document that names the "subscriptions" service
    And the Subscriptions service received nothing
    And 5 requests to "/api/v1/animals" are all answered with status 200

  Scenario: When Subscriptions comes back, its routes work again
    Given the Subscriptions service stops
    And a client sends GET "/api/v1/subscription-plans" once
    When the Subscriptions service starts again
    Then the plans route answers 200 again

  Scenario: When a replica of the core fails, it leaves the rotation and requests keep being answered
    Given the replica "api1" of the core stops
    When a client sends GET "/api/v1/animals" 6 times
    Then at most 1 of those requests failed
    And 10 more requests to "/api/v1/animals" are all answered by the replica "api2"
    And the gateway reports the replica "api1" as "Unhealthy" and "api2" as "Healthy"

  Scenario: When the replica comes back, it returns to the rotation
    Given the replica "api1" of the core stops
    And a client sends GET "/api/v1/animals" 4 times
    When the replica "api1" starts again
    Then the gateway reports the replica "api1" as "Healthy"
    And both replicas receive traffic again without any failed request
