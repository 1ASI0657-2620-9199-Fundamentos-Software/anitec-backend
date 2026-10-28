Feature: Authorization of herds, corrals, devices and readings
  As a rancher or a veterinarian
  I want the herd, corral, device and reading lists to contain only what belongs to me or to the ranchers that approved me
  So that nobody reads the infrastructure or the sensor data of another owner by calling the API directly (QAS-08)

  Background:
    Given the rancher "Ana" with id 1 has the herd "Los Alamos" with a corral, a device and a reading
    And the rancher "Luis" with id 2 has the herd "El Molino" with a corral, a device and a reading

  Scenario: A rancher only sees its own herds
    When user 1 with role "Rancher" lists the herds
    Then 1 results are returned
    And every result is related to "Los Alamos"

  Scenario: A rancher only sees its own corrals
    When user 1 with role "Rancher" lists the corrals
    Then 1 results are returned
    And every result is related to "Los Alamos"

  Scenario: A rancher only sees its own devices
    When user 1 with role "Rancher" lists the devices
    Then 1 results are returned
    And every result is related to "Los Alamos"

  Scenario: A rancher only sees the readings of its own devices
    When user 1 with role "Rancher" lists the readings
    Then 1 results are returned
    And every result is related to "Los Alamos"

  Scenario Outline: A rancher cannot read the detail of a resource of another rancher
    When user 1 with role "Rancher" asks for the <resource> of rancher 2
    Then the item is not found

    Examples:
      | resource |
      | herd     |
      | corral   |
      | device   |
      | reading  |

  Scenario Outline: A rancher can read the detail of its own resources
    When user 1 with role "Rancher" asks for the <resource> of rancher 1
    Then the item is found

    Examples:
      | resource |
      | herd     |
      | corral   |
      | device   |
      | reading  |

  Scenario Outline: A veterinarian without an accepted relationship sees nothing
    Given veterinarian 10 has a "<status>" link with rancher 1
    When user 10 with role "Veterinarian" lists the <resources>
    Then 0 results are returned

    Examples:
      | status   | resources |
      | Pending  | herds     |
      | Pending  | corrals   |
      | Pending  | devices   |
      | Pending  | readings  |
      | Rejected | herds     |
      | Rejected | corrals   |
      | Rejected | devices   |
      | Rejected | readings  |

  Scenario: A veterinarian with no relationship sees no devices
    When user 10 with role "Veterinarian" lists the devices
    Then 0 results are returned

  Scenario Outline: An approved veterinarian sees only the resources of the rancher that approved it
    Given veterinarian 10 has a "Accepted" link with rancher 1
    When user 10 with role "Veterinarian" lists the <resources>
    Then 1 results are returned
    And every result is related to "Los Alamos"

    Examples:
      | resources |
      | herds     |
      | corrals   |
      | devices   |
      | readings  |

  Scenario: An approved veterinarian cannot read the detail of a resource of another rancher
    Given veterinarian 10 has a "Accepted" link with rancher 1
    When user 10 with role "Veterinarian" asks for the device of rancher 2
    Then the item is not found

  Scenario: A device installed on an animal is visible to the owner and to the approved veterinarian
    Given rancher 1 also has a device installed on one of its animals
    And veterinarian 10 has a "Accepted" link with rancher 1
    When user 1 with role "Rancher" lists the devices
    Then 2 results are returned
    When user 10 with role "Veterinarian" lists the devices
    Then 2 results are returned

  Scenario: A device that is not installed on a herd or an animal is visible to nobody
    Given a device that is not installed anywhere exists
    When user 1 with role "Rancher" lists the devices
    Then 1 results are returned

  Scenario: Only the owner counts as the owner of a herd
    Then user 1 with role "Rancher" is the owner of the herd of rancher 1
    And user 1 with role "Rancher" is not the owner of the herd of rancher 2
    And user 10 with role "Veterinarian" is not the owner of the herd of rancher 1
