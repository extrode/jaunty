-- Jaunty Test Database Setup Script
-- Run this script on your test database servers to create required stored procedures

-- ============================================================================
-- SQL Server Setup
-- ============================================================================

USE Northwind;
GO

-- Drop existing procedures if they exist
IF OBJECT_ID('GetAllProducts', 'P') IS NOT NULL DROP PROCEDURE GetAllProducts;
IF OBJECT_ID('GetProductsByCategory', 'P') IS NOT NULL DROP PROCEDURE GetProductsByCategory;
IF OBJECT_ID('GetProductCount', 'P') IS NOT NULL DROP PROCEDURE GetProductCount;
IF OBJECT_ID('GetCategoryWithProductCount', 'P') IS NOT NULL DROP PROCEDURE GetCategoryWithProductCount;
GO

-- Get all products
CREATE PROCEDURE GetAllProducts
AS
BEGIN
    SELECT ProductID, ProductName, UnitPrice, UnitsInStock, CategoryID
    FROM Products
    ORDER BY ProductID;
END
GO

-- Get products by category
CREATE PROCEDURE GetProductsByCategory
    @CategoryId INT
AS
BEGIN
    SELECT ProductID, ProductName, UnitPrice, UnitsInStock, CategoryID
    FROM Products
    WHERE CategoryID = @CategoryId
    ORDER BY ProductID;
END
GO

-- Get product count
CREATE PROCEDURE GetProductCount
AS
BEGIN
    SELECT COUNT(*) FROM Products;
END
GO

-- Get category with product count (for output parameter test)
CREATE PROCEDURE GetCategoryWithProductCount
    @CategoryId INT,
    @ProductCount INT OUTPUT
AS
BEGIN
    SELECT CategoryID, CategoryName, Description
    FROM Categories
    WHERE CategoryID = @CategoryId;
    
    SELECT @ProductCount = COUNT(*) FROM Products WHERE CategoryID = @CategoryId;
END
GO

-- ============================================================================
-- PostgreSQL Setup
-- ============================================================================

-- Run on PostgreSQL server:
-- psql -d northwind -f setup_postgres.sql

-- DROP PROCEDURE IF EXISTS get_all_products();
-- DROP PROCEDURE IF EXISTS get_products_by_category(INTEGER);
-- DROP PROCEDURE IF EXISTS get_product_count();

-- CREATE OR REPLACE PROCEDURE get_all_products()
-- LANGUAGE SQL
-- AS $$
--   SELECT product_id, product_name, unit_price, units_in_stock, category_id
--   FROM products
--   ORDER BY product_id;
-- $$;

-- CREATE OR REPLACE PROCEDURE get_products_by_category(IN p_category_id INTEGER)
-- LANGUAGE SQL
-- AS $$
--   SELECT product_id, product_name, unit_price, units_in_stock, category_id
--   FROM products
--   WHERE category_id = p_category_id
--   ORDER BY product_id;
-- $$;

-- CREATE OR REPLACE PROCEDURE get_product_count()
-- LANGUAGE SQL
-- AS $$
--   SELECT COUNT(*) FROM products;
-- $$;

-- ============================================================================
-- MySQL Setup  
-- ============================================================================

-- Run on MySQL server:
-- mysql northwind < setup_mysql.sql

-- DROP PROCEDURE IF EXISTS GetAllProducts;
-- DROP PROCEDURE IF EXISTS GetProductsByCategory;
-- DROP PROCEDURE IF EXISTS GetProductCount;

-- DELIMITER //
-- CREATE PROCEDURE GetAllProducts()
-- BEGIN
--   SELECT ProductID, ProductName, UnitPrice, UnitsInStock, CategoryID
--   FROM Products
--   ORDER BY ProductID;
-- END //

-- CREATE PROCEDURE GetProductsByCategory(IN p_CategoryId INT)
-- BEGIN
--   SELECT ProductID, ProductName, UnitPrice, UnitsInStock, CategoryID
--   FROM Products
--   WHERE CategoryID = p_CategoryId
--   ORDER BY ProductID;
-- END //

-- CREATE PROCEDURE GetProductCount()
-- BEGIN
--   SELECT COUNT(*) FROM Products;
-- END //
-- DELIMITER ;
