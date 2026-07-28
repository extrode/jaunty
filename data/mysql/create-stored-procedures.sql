-- Jaunty Test Stored Procedures for MySQL / MariaDB
-- Run this script against a Northwind database to create the stored procedures
-- needed by the Jaunty integration tests.
--
-- Prerequisites:
--   1. A MySQL/MariaDB instance with a Northwind database
--   2. Tables: products, categories (with standard Northwind schema)
--
-- Usage:
--   mysql -h localhost -u root northwind < create-stored-procedures.sql

-- =============================================
-- GetAllProducts: Returns all products
-- Tests: ExecuteStoredProcedure<T>(procedureName)
-- =============================================
DROP PROCEDURE IF EXISTS GetAllProducts;

DELIMITER //
CREATE PROCEDURE GetAllProducts()
BEGIN
    SELECT product_id AS ProductId, product_name AS ProductName,
           supplier_id AS SupplierId, category_id AS CategoryId,
           quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
           units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder,
           reorder_level AS ReorderLevel, discontinued AS Discontinued
    FROM products;
END //
DELIMITER ;

-- =============================================
-- GetProductsByCategory: Returns products filtered by category_id
-- Tests: ExecuteStoredProcedure<T>(procedureName, parameters)
-- =============================================
DROP PROCEDURE IF EXISTS GetProductsByCategory;

DELIMITER //
CREATE PROCEDURE GetProductsByCategory(IN p_CategoryId INT)
BEGIN
    SELECT product_id AS ProductId, product_name AS ProductName,
           supplier_id AS SupplierId, category_id AS CategoryId,
           quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
           units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder,
           reorder_level AS ReorderLevel, discontinued AS Discontinued
    FROM products
    WHERE category_id = p_CategoryId;
END //
DELIMITER ;

-- =============================================
-- GetProductById: Returns a single product by ID
-- Tests: ExecuteStoredProcedureFirst, ExecuteStoredProcedureSingle
-- =============================================
DROP PROCEDURE IF EXISTS GetProductById;

DELIMITER //
CREATE PROCEDURE GetProductById(IN p_ProductId INT)
BEGIN
    SELECT product_id AS ProductId, product_name AS ProductName,
           supplier_id AS SupplierId, category_id AS CategoryId,
           quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
           units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder,
           reorder_level AS ReorderLevel, discontinued AS Discontinued
    FROM products
    WHERE product_id = p_ProductId;
END //
DELIMITER ;

-- =============================================
-- GetProductCount: Returns scalar count of all products
-- Tests: ExecuteStoredProcedureScalar<T>
-- =============================================
DROP PROCEDURE IF EXISTS GetProductCount;

DELIMITER //
CREATE PROCEDURE GetProductCount()
BEGIN
    SELECT COUNT(*) FROM products;
END //
DELIMITER ;

-- =============================================
-- GetProductCountByCategory: Returns scalar count with parameter
-- Tests: ExecuteStoredProcedureScalar<T>(procedureName, parameters)
-- =============================================
DROP PROCEDURE IF EXISTS GetProductCountByCategory;

DELIMITER //
CREATE PROCEDURE GetProductCountByCategory(IN p_CategoryId INT)
BEGIN
    SELECT COUNT(*) FROM products WHERE category_id = p_CategoryId;
END //
DELIMITER ;

-- =============================================
-- UpdateProductPrice: Non-query that updates unit_price
-- Tests: ExecuteStoredProcedureNonQuery
-- Note: Tests should wrap in transaction and rollback
-- =============================================
DROP PROCEDURE IF EXISTS UpdateProductPrice;

DELIMITER //
CREATE PROCEDURE UpdateProductPrice(IN p_ProductId INT, IN p_NewPrice DECIMAL(18,2))
BEGIN
    UPDATE products SET unit_price = p_NewPrice WHERE product_id = p_ProductId;
END //
DELIMITER ;

-- =============================================
-- GetProductCountWithOutput: Returns count via OUTPUT parameter
-- Tests: ExecuteStoredProcedure with SpParameters (output)
-- =============================================
DROP PROCEDURE IF EXISTS GetProductCountWithOutput;

DELIMITER //
CREATE PROCEDURE GetProductCountWithOutput(IN p_CategoryId INT, OUT p_ProductCount INT)
BEGIN
    SELECT COUNT(*) INTO p_ProductCount FROM products WHERE category_id = p_CategoryId;
END //
DELIMITER ;

-- =============================================
-- GetNoResults: Returns empty result set
-- Tests: FirstOrDefault returns null, First throws
-- =============================================
DROP PROCEDURE IF EXISTS GetNoResults;

DELIMITER //
CREATE PROCEDURE GetNoResults()
BEGIN
    SELECT product_id AS ProductId, product_name AS ProductName,
           supplier_id AS SupplierId, category_id AS CategoryId,
           quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
           units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder,
           reorder_level AS ReorderLevel, discontinued AS Discontinued
    FROM products
    WHERE 1 = 0;
END //
DELIMITER ;

SELECT 'All Jaunty test stored procedures created successfully.' AS status;
