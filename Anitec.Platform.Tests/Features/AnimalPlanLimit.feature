Feature: Animal limit of the subscription plan
  As a product owner
  I want the animal limit that each plan advertises to be applied
  So that the plan that a rancher pays for means something, without ever blocking a rancher because payments are down

  Scenario: A rancher without an active plan is not limited
    Given rancher 1 has 5 animals
    And rancher 1 has no active plan
    When rancher 1 wants to add 10 animals
    Then the animals are allowed because there is no plan

  Scenario: A rancher within the limit of the plan can add animals
    Given rancher 1 has 70 animals
    And rancher 1 has the plan "Ganadero Basico" with a limit of 80 animals
    When rancher 1 wants to add 10 animals
    Then the animals are allowed within the limit

  Scenario: A rancher that reaches the limit cannot add more
    Given rancher 1 has 80 animals
    And rancher 1 has the plan "Ganadero Basico" with a limit of 80 animals
    When rancher 1 wants to add 1 animals
    Then the animals are refused because the limit of 80 is reached with 80 animals

  Scenario: A batch that would pass the limit is refused as a whole
    Given rancher 1 has 75 animals
    And rancher 1 has the plan "Ganadero Basico" with a limit of 80 animals
    When rancher 1 wants to add 10 animals
    Then the animals are refused because the limit of 80 is reached with 75 animals

  Scenario: Only the animals of the rancher count
    Given rancher 1 has 10 animals
    And rancher 2 has 500 animals
    And rancher 1 has the plan "Ganadero Basico" with a limit of 80 animals
    When rancher 1 wants to add 70 animals
    Then the animals are allowed within the limit

  Scenario: When Subscriptions cannot be reached the rancher keeps working
    Given rancher 1 has 500 animals
    And the Subscriptions service is not available
    When rancher 1 wants to add 10 animals
    Then the animals are allowed because the plan could not be checked

  Scenario: Without the address of the service the limit is not checked
    Given rancher 1 has 500 animals
    And the address of the Subscriptions service is not configured
    When rancher 1 wants to add 10 animals
    Then the animals are allowed and the check does not apply

  Scenario: A veterinarian is not limited by the plan of a rancher
    Given rancher 1 has 80 animals
    And rancher 1 has the plan "Ganadero Basico" with a limit of 80 animals
    When veterinarian 10 wants to add 1 animals
    Then the animals are allowed and the check does not apply

  Scenario: Without a token the plan cannot be asked for
    Given rancher 1 has 80 animals
    And rancher 1 has the plan "Ganadero Basico" with a limit of 80 animals
    When rancher 1 without a token wants to add 1 animals
    Then the animals are allowed and the check does not apply
