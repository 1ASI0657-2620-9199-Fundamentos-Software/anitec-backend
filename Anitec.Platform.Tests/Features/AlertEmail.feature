Feature: E-mail about an alert
  As a rancher
  I want to receive an e-mail when an alert is created
  So that I notice it without opening the application (iteration 5, Resend)

  Background:
    Given today is 2026-11-27
    And animal 5 belongs to rancher 1
    And rancher 1 has the e-mail address "ganadero@example.com"
    And health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20

  Scenario: A new alert is sent by e-mail to its owner once
    When the scanner runs
    And the alert e-mails are sent
    And the alert e-mails are sent
    Then 1 e-mail was sent to "ganadero@example.com"
    And the e-mail mentions "Vacuna aftosa"
    And the alert "Vacuna aftosa" is marked as e-mailed

  Scenario: When the e-mail fails the alert stays and the next pass tries again
    Given the e-mail provider fails
    And the scanner runs
    When the alert e-mails are sent
    Then rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"
    And the alert "Vacuna aftosa" is not marked as e-mailed
    Given the e-mail provider works again
    When the alert e-mails are sent
    Then the alert "Vacuna aftosa" is marked as e-mailed

  Scenario: After three failed attempts the notifier stops trying
    Given the e-mail provider fails
    And the scanner runs
    When the alert e-mails are sent
    And the alert e-mails are sent
    And the alert e-mails are sent
    And the alert e-mails are sent
    Then the e-mail provider was called 3 times

  Scenario: An owner without an address still has the alert, but no e-mail is tried
    Given rancher 1 has no e-mail address
    And the scanner runs
    When the alert e-mails are sent
    And the alert e-mails are sent
    Then the e-mail provider was called 0 times
    And rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"

  Scenario: Without a key nothing is sent and no attempt is used up
    Given the e-mail provider is not configured
    And the scanner runs
    When the alert e-mails are sent
    And the alert e-mails are sent
    Then the alert "Vacuna aftosa" has 0 e-mail attempts
    Given the e-mail provider works again
    When the alert e-mails are sent
    Then 1 e-mail was sent to "ganadero@example.com"

  Scenario: A confirmed alert is not e-mailed
    Given the scanner runs
    And rancher 1 confirms the alert "Vacuna aftosa"
    When the alert e-mails are sent
    Then the e-mail provider was called 0 times

  Scenario: A snoozed alert is e-mailed again when it comes back
    Given the scanner runs
    And the alert e-mails are sent
    And rancher 1 snoozes the alert "Vacuna aftosa" for 3 days with the reason "Esperando al veterinario"
    When 3 days pass
    And the scanner runs
    And the alert e-mails are sent
    Then 2 e-mails were sent to "ganadero@example.com"

  Scenario: A failure of the e-mail provider does not stop the scanner from creating alerts
    Given the e-mail provider fails
    When the scanner runs
    Then 1 alert is created
