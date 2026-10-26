Feature: Veterinarian access approval
  As a rancher
  I want to decide which veterinarians can see my herds and clinical history
  So that nobody reads my data without my explicit approval (US-023, US-071, US-072)

  Scenario: A veterinarian request starts as pending
    When veterinarian 10 requests access to rancher 1
    Then the request is saved with the status "Pending"
    And the request has no acceptance date

  Scenario: The rancher approves a pending request
    Given veterinarian 10 requested access to rancher 1
    When rancher 1 approves the request of veterinarian 10
    Then the request has the status "Accepted"
    And the request has an acceptance date

  Scenario: The rancher rejects a pending request
    Given veterinarian 10 requested access to rancher 1
    When rancher 1 rejects the request of veterinarian 10
    Then the request has the status "Rejected"

  Scenario: The rancher revokes the access of an accepted veterinarian
    Given veterinarian 10 requested access to rancher 1
    And rancher 1 approves the request of veterinarian 10
    When rancher 1 rejects the request of veterinarian 10
    Then the request has the status "Rejected"
    And the request has no acceptance date

  Scenario: A veterinarian cannot ask twice while the request is open
    Given veterinarian 10 requested access to rancher 1
    When veterinarian 10 requests access to rancher 1
    Then the operation fails with the error "VeterinarianClientAlreadyExists"

  Scenario: A veterinarian can ask again after a rejection
    Given veterinarian 10 requested access to rancher 1
    And rancher 1 rejects the request of veterinarian 10
    When veterinarian 10 requests access to rancher 1
    Then the request has the status "Pending"

  Scenario: A rejected request cannot be approved without a new request
    Given veterinarian 10 requested access to rancher 1
    And rancher 1 rejects the request of veterinarian 10
    When rancher 1 approves the request of veterinarian 10
    Then the operation fails with the error "VeterinarianClientNotPending"

  Scenario: Approving a request that does not exist fails
    When rancher 1 approves the request of veterinarian 99
    Then the operation fails with the error "VeterinarianClientNotFound"

  Scenario: Only an approved request opens the data of the rancher
    Given veterinarian 10 requested access to rancher 1
    Then the rancher 1 data is not visible to veterinarian 10
    When rancher 1 approves the request of veterinarian 10
    Then the rancher 1 data is visible to veterinarian 10
