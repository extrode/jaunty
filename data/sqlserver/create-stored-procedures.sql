-- Jaunty Test Stored Procedures for SQL Server
-- Run this script against a Northwind database to create the stored procedures
-- needed by the Jaunty integration tests.
--
-- Prerequisites:
--   1. A SQL Server instance with a Northwind database
--   2. Tables: products, categories (with standard Northwind schema)
--
-- Usage:
--   sqlcmd -S localhost -d Northwind -i create-stored-procedures.sql

-- =============================================
-- GetAllProducts: Returns all products
-- Tests: ExecuteStoredProcedure<T>(procedureName)
-- =============================================
IF OBJECT_ID('dbo.GetAllProducts', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetAllProducts;
GO

CREATE PROCEDURE dbo.GetAllProducts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products;
END;
GO

-- =============================================
-- GetProductsByCategory: Returns products filtered by category_id
-- Tests: ExecuteStoredProcedure<T>(procedureName, parameters)
-- =============================================
IF OBJECT_ID('dbo.GetProductsByCategory', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetProductsByCategory;
GO

CREATE PROCEDURE dbo.GetProductsByCategory
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products
    WHERE category_id = @CategoryId;
END;
GO

-- =============================================
-- GetProductById: Returns a single product by ID
-- Tests: ExecuteStoredProcedureFirst, ExecuteStoredProcedureSingle
-- =============================================
IF OBJECT_ID('dbo.GetProductById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetProductById;
GO

CREATE PROCEDURE dbo.GetProductById
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products
    WHERE product_id = @ProductId;
END;
GO

-- =============================================
-- GetProductCount: Returns scalar count of all products
-- Tests: ExecuteStoredProcedureScalar<T>
-- =============================================
IF OBJECT_ID('dbo.GetProductCount', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetProductCount;
GO

CREATE PROCEDURE dbo.GetProductCount
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM products;
END;
GO

-- =============================================
-- GetProductCountByCategory: Returns scalar count with parameter
-- Tests: ExecuteStoredProcedureScalar<T>(procedureName, parameters)
-- =============================================
IF OBJECT_ID('dbo.GetProductCountByCategory', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetProductCountByCategory;
GO

CREATE PROCEDURE dbo.GetProductCountByCategory
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM products WHERE category_id = @CategoryId;
END;
GO

-- =============================================
-- UpdateProductPrice: Non-query that updates unit_price
-- Tests: ExecuteStoredProcedureNonQuery
-- Note: Tests should wrap in transaction and rollback
-- =============================================
IF OBJECT_ID('dbo.UpdateProductPrice', 'P') IS NOT NULL
    DROP PROCEDURE dbo.UpdateProductPrice;
GO

CREATE PROCEDURE dbo.UpdateProductPrice
    @ProductId INT,
    @NewPrice DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE products SET unit_price = @NewPrice WHERE product_id = @ProductId;
END;
GO

-- =============================================
-- GetProductCountWithOutput: Returns count via OUTPUT parameter
-- Tests: ExecuteStoredProcedure with SpParameters (output)
-- =============================================
IF OBJECT_ID('dbo.GetProductCountWithOutput', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetProductCountWithOutput;
GO

CREATE PROCEDURE dbo.GetProductCountWithOutput
    @CategoryId INT,
    @ProductCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @ProductCount = COUNT(*) FROM products WHERE category_id = @CategoryId;
END;
GO

-- =============================================
-- GetNoResults: Returns empty result set
-- Tests: FirstOrDefault returns null, First throws
-- =============================================
IF OBJECT_ID('dbo.GetNoResults', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetNoResults;
GO

CREATE PROCEDURE dbo.GetNoResults
AS
BEGIN
    SET NOCOUNT ON;
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products
    WHERE 1 = 0;
END;
GO

PRINT 'All Jaunty test stored procedures created successfully.';
GO
