Feature: Device authentication for telemetry ingestion
  As a rancher that installs a field device
  I want the device to authenticate with its own API key
  So that readings are accepted only from the device they claim to come from (QAS-11, US-058, US-059)

  Background:
    Given a device named "Collar Luna" exists
    And a device named "Balanza Los Alamos" exists

  Scenario: Issuing a key returns it once and stores only its hash
    When an API key is issued for "Collar Luna"
    Then the key starts with "ak_"
    And the stored value for "Collar Luna" is a SHA-256 hash and not the key

  Scenario: A valid key authenticates the device
    Given an API key was issued for "Collar Luna"
    When "Collar Luna" authenticates with its key
    Then the device is authenticated

  Scenario: A wrong key is rejected
    Given an API key was issued for "Collar Luna"
    When "Collar Luna" authenticates with the key "ak_not-the-key"
    Then the device is rejected

  Scenario: The key of another device is rejected
    Given an API key was issued for "Collar Luna"
    And an API key was issued for "Balanza Los Alamos"
    When "Collar Luna" authenticates with the key of "Balanza Los Alamos"
    Then the device is rejected

  Scenario: A device without a key cannot authenticate
    When "Collar Luna" authenticates with the key "ak_anything"
    Then the device is rejected

  Scenario: Issuing a new key invalidates the previous one
    Given an API key was issued for "Collar Luna"
    And a new API key is issued for "Collar Luna"
    When "Collar Luna" authenticates with its first key
    Then the device is rejected

  Scenario: Issuing a key for an unknown device fails
    When an API key is issued for the device with id 999
    Then issuing the key fails because the device does not exist

  Scenario: The HTTP filter accepts a request with a valid key for its device
    Given an API key was issued for "Collar Luna"
    When a telemetry request for "Collar Luna" carries its key in the "X-Device-Api-Key" header
    Then the request is authorized as "Collar Luna"

  Scenario: The HTTP filter rejects a request without the header
    Given an API key was issued for "Collar Luna"
    When a telemetry request for "Collar Luna" carries no key
    Then the request is rejected with status 401

  Scenario: The HTTP filter rejects a key sent to the route of another device
    Given an API key was issued for "Collar Luna"
    When a telemetry request for "Balanza Los Alamos" carries the key of "Collar Luna"
    Then the request is rejected with status 401
