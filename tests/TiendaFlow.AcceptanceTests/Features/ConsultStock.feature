Feature: Consult stock
  As a store employee
  I want to check the stock of a product in my store in real time
  So that I can tell customers what is available

  Background:
    Given store 1 has 10 units of "SKU-200" in location "Aisle 3"

  Scenario: Check the stock of a product
    When I consult the stock of "SKU-200" in store 1
    Then I see 10 units available in location "Aisle 3"

  Scenario: The stock reflects a sale
    When 4 units of "SKU-200" are sold in store 1
    And I consult the stock of "SKU-200" in store 1
    Then I see 6 units available in location "Aisle 3"
    And the last movement is a sale of 4 units

  Scenario: A sale larger than the available stock is rejected
    When I try to sell 15 units of "SKU-200" in store 1
    Then the sale is rejected because there is not enough stock
    And store 1 still has 10 units of "SKU-200"

  Scenario: A product without stock in the store cannot be consulted
    When I try to consult the stock of "SKU-404" in store 1
    Then the consultation is rejected because there is no stock for that product
