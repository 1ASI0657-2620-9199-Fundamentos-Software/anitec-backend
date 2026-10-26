Feature: Minimum interval between device readings
  As the platform that runs on limited compute
  I want each device to send at most one reading per interval
  So that a device stuck in a retry loop cannot exhaust the service, whichever replica receives it (QAS-11, US-059)

  Background:
    Given the minimum interval between readings is 60 seconds
    And a device with id 7 sent a reading of 38.5 at 10:00:00

  Scenario: A reading inside the interval is rejected
    When the device with id 7 sends a reading of 38.7 at 10:00:30
    Then the reading is rejected because it came too soon
    And the device with id 7 has 1 stored readings

  Scenario: A reading after the interval is stored
    When the device with id 7 sends a reading of 38.7 at 10:01:00
    Then the reading is stored
    And the device with id 7 has 2 stored readings

  Scenario: The interval is counted per device
    When the device with id 8 sends a reading of 20.1 at 10:00:05
    Then the reading is stored

  Scenario: The first reading of a device is always stored
    When the device with id 9 sends a reading of 19.0 at 10:00:05
    Then the reading is stored
