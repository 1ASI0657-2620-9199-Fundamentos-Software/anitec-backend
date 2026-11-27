Feature: Alerts about due dates that nobody confirmed
  As a rancher
  I want the system to warn me when a health follow-up or an activity is due and nobody attended it
  So that nothing is forgotten (US-070, QAS-03)

  Background:
    Given today is 2026-11-27
    And animal 5 belongs to rancher 1

  Scenario: A health event whose follow-up date arrived creates one alert for the owner of the animal
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-27
    When the scanner runs
    Then 1 alert is created
    And rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"

  Scenario: A follow-up in the future creates no alert
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-28
    When the scanner runs
    Then 0 alerts are created

  Scenario: Scanning again does not duplicate the alert
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    When the scanner runs
    And the scanner runs
    Then rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"

  Scenario: Two replicas that scan one after the other still create one alert
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    When the scanner runs on replica "api1"
    And the scanner runs on replica "api2"
    Then rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"

  Scenario: A new due date of the same health event gets its own alert
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When the due date of health event 7 moves to 2026-11-26
    And the scanner runs
    Then rancher 1 has 2 alerts

  Scenario: A pending activity whose date arrived creates an alert
    Given activity 3 of rancher 1 titled "Visita sanitaria" with status "Pendiente" is dated 2026-11-27
    When the scanner runs
    Then rancher 1 has 1 alert with status "Pending" titled "Visita sanitaria"

  Scenario: A completed activity creates no alert
    Given activity 3 of rancher 1 titled "Visita sanitaria" with status "Completado" is dated 2026-11-20
    When the scanner runs
    Then 0 alerts are created

  Scenario: An activity without an owner creates no alert
    Given activity 4 without owner titled "Compra" with status "Pendiente" is dated 2026-11-20
    When the scanner runs
    Then 0 alerts are created

  Scenario: An overdue alert stays visible and is not deleted
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When 3 days pass
    Then rancher 1 sees the alert "Vacuna aftosa" as overdue
    And rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"

  Scenario: The rancher confirms the attention and the actor and date are recorded
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When rancher 1 confirms the alert "Vacuna aftosa"
    Then the alert "Vacuna aftosa" has status "Confirmed"
    And the alert "Vacuna aftosa" was resolved by user 1

  Scenario: A confirmed alert does not come back when the scanner runs again
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    And rancher 1 confirms the alert "Vacuna aftosa"
    When the scanner runs
    Then rancher 1 has 1 alert with status "Confirmed" titled "Vacuna aftosa"

  Scenario: Postponing needs a reason
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When rancher 1 snoozes the alert "Vacuna aftosa" for 3 days with the reason ""
    Then the request is refused because a reason is required

  Scenario: Postponing moves the date and the alert comes back when the new date arrives
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When rancher 1 snoozes the alert "Vacuna aftosa" for 3 days with the reason "Esperando al veterinario"
    Then the alert "Vacuna aftosa" has status "Snoozed" and is due on 2026-11-30
    When 3 days pass
    And the scanner runs
    Then rancher 1 has 1 alert with status "Pending" titled "Vacuna aftosa"

  Scenario: The days of a postponement are limited
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When rancher 1 snoozes the alert "Vacuna aftosa" for 31 days with the reason "Esperando"
    Then the request is refused because the days are not valid

  Scenario: The rancher closes an alert without confirming it
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    When rancher 1 closes the alert "Vacuna aftosa" with the reason "El animal salió del hato"
    Then the alert "Vacuna aftosa" has status "Closed"

  Scenario: A confirmed alert cannot be postponed
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    And rancher 1 confirms the alert "Vacuna aftosa"
    When rancher 1 snoozes the alert "Vacuna aftosa" for 3 days with the reason "Otra vez"
    Then the request is refused because the transition is not allowed

  Scenario: Another rancher cannot see or change the alert
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    Then rancher 2 has 0 alerts
    When rancher 2 confirms the alert "Vacuna aftosa"
    Then the request is refused because the alert was not found
    And the alert "Vacuna aftosa" has status "Pending"

  Scenario: A veterinarian has no alerts
    Given health event 7 of animal 5 with type "Vacuna aftosa" is due on 2026-11-20
    And the scanner runs
    Then veterinarian 10 has 0 alerts
