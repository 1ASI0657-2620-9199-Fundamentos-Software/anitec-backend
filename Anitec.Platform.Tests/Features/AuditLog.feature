Feature: Audit log of sensitive operations
  As a rancher
  I want to know who created, changed or deleted sensitive information of mine
  So that I can verify how my data is used (US-074, TS-028)

  Scenario: A sensitive operation is recorded with actor, owner, action and correlation id
    Given the request carries the correlation id "req-0001"
    When user 10 with role "Veterinarian" performs "CreateHealthEvent" on "HealthEvent" 7 of rancher 1
    Then rancher 1 sees 1 audit entry
    And the newest entry of rancher 1 has actor 10 with role "Veterinarian" and action "CreateHealthEvent"
    And the newest entry of rancher 1 has the correlation id "req-0001"
    And the newest entry of rancher 1 has the result "Success"

  Scenario: A rancher sees only the entries about its own data
    When user 1 with role "Rancher" performs "DeleteAnimal" on "Animal" 5 of rancher 1
    And user 2 with role "Rancher" performs "DeleteAnimal" on "Animal" 9 of rancher 2
    Then rancher 1 sees 1 audit entry
    And rancher 2 sees 1 audit entry
    And rancher 3 sees 0 audit entries

  Scenario: What a veterinarian does on the data of a rancher appears to the rancher
    When user 10 with role "Veterinarian" performs "UpdateHealthEvent" on "HealthEvent" 7 of rancher 1
    Then rancher 1 sees 1 audit entry
    And the newest entry of rancher 1 has actor 10 with role "Veterinarian" and action "UpdateHealthEvent"

  Scenario: The entries can be filtered by action
    When user 1 with role "Rancher" performs "DeleteAnimal" on "Animal" 5 of rancher 1
    And user 1 with role "Rancher" performs "DeleteHerd" on "Herd" 2 of rancher 1
    Then rancher 1 sees 1 audit entry when filtering by action "DeleteHerd"
    And rancher 1 sees 2 audit entries

  Scenario: The newest entries come first
    When user 1 with role "Rancher" performs "DeleteAnimal" on "Animal" 5 of rancher 1
    And one minute passes
    And user 1 with role "Rancher" performs "DeleteHerd" on "Herd" 2 of rancher 1
    Then the newest entry of rancher 1 has actor 1 with role "Rancher" and action "DeleteHerd"

  Scenario: An entry cannot be modified
    Given user 1 with role "Rancher" performed "DeleteAnimal" on "Animal" 5 of rancher 1
    When someone tries to change the stored entry
    Then the change is rejected because the log is append-only

  Scenario: An entry cannot be removed
    Given user 1 with role "Rancher" performed "DeleteAnimal" on "Animal" 5 of rancher 1
    When someone tries to remove the stored entry
    Then the change is rejected because the log is append-only

  Scenario: A failure to store the entry does not undo the operation that already happened
    Given the audit storage is not available
    When user 1 with role "Rancher" performs "DeleteAnimal" on "Animal" 5 of rancher 1
    Then no error reaches the caller
    And the failure is written to the log

  Scenario: The log has no operation to create, change or remove entries through the API
    Then the audit log controller only offers reading
    And only ranchers can read the audit log
