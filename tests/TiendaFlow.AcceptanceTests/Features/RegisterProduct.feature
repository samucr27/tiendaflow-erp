Feature: Register products
  As a store manager
  I want to register products with their barcode and prices
  So that they can be sold in every store

  Scenario: Register a product with a unique SKU and barcode
    Given no product is registered with SKU "SKU-100"
    When I register the product "SKU-100" with barcode "7701234567100", name "Granola 300 g" and price 9500
    Then the product "SKU-100" is available in the catalog

  Scenario: A duplicate SKU is rejected
    Given a product is registered with SKU "SKU-101" and barcode "7701234567101"
    When I try to register another product with SKU "SKU-101" and barcode "7701234567102"
    Then the registration is rejected because the SKU already exists
