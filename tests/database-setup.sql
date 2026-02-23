-- SQL Server test stored procedures for Jaunty integration tests.
-- Run on SQL Server against Northwind.

USE Northwind;
GO

IF OBJECT_ID('dbo.GetAllProducts', 'P') IS NOT NULL DROP PROCEDURE dbo.GetAllProducts;
IF OBJECT_ID('dbo.GetProductsByCategory', 'P') IS NOT NULL DROP PROCEDURE dbo.GetProductsByCategory;
IF OBJECT_ID('dbo.GetProductById', 'P') IS NOT NULL DROP PROCEDURE dbo.GetProductById;
IF OBJECT_ID('dbo.GetProductCount', 'P') IS NOT NULL DROP PROCEDURE dbo.GetProductCount;
IF OBJECT_ID('dbo.GetProductCountByCategory', 'P') IS NOT NULL DROP PROCEDURE dbo.GetProductCountByCategory;
IF OBJECT_ID('dbo.UpdateProductPrice', 'P') IS NOT NULL DROP PROCEDURE dbo.UpdateProductPrice;
IF OBJECT_ID('dbo.GetProductCountWithOutput', 'P') IS NOT NULL DROP PROCEDURE dbo.GetProductCountWithOutput;
IF OBJECT_ID('dbo.GetNoResults', 'P') IS NOT NULL DROP PROCEDURE dbo.GetNoResults;
GO

CREATE PROCEDURE dbo.GetAllProducts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProductID AS product_id, ProductName AS product_name, SupplierID AS supplier_id, CategoryID AS category_id,
           QuantityPerUnit AS quantity_per_unit, UnitPrice AS unit_price, UnitsInStock AS units_in_stock,
           UnitsOnOrder AS units_on_order, ReorderLevel AS reorder_level, Discontinued AS discontinued
    FROM Products;
END
GO

CREATE PROCEDURE dbo.GetProductsByCategory
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProductID AS product_id, ProductName AS product_name, SupplierID AS supplier_id, CategoryID AS category_id,
           QuantityPerUnit AS quantity_per_unit, UnitPrice AS unit_price, UnitsInStock AS units_in_stock,
           UnitsOnOrder AS units_on_order, ReorderLevel AS reorder_level, Discontinued AS discontinued
    FROM Products
    WHERE CategoryID = @CategoryId;
END
GO

CREATE PROCEDURE dbo.GetProductById
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProductID AS product_id, ProductName AS product_name, SupplierID AS supplier_id, CategoryID AS category_id,
           QuantityPerUnit AS quantity_per_unit, UnitPrice AS unit_price, UnitsInStock AS units_in_stock,
           UnitsOnOrder AS units_on_order, ReorderLevel AS reorder_level, Discontinued AS discontinued
    FROM Products
    WHERE ProductID = @ProductId;
END
GO

CREATE PROCEDURE dbo.GetProductCount
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM Products;
END
GO

CREATE PROCEDURE dbo.GetProductCountByCategory
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM Products WHERE CategoryID = @CategoryId;
END
GO

CREATE PROCEDURE dbo.UpdateProductPrice
    @ProductId INT,
    @NewPrice DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Products SET UnitPrice = @NewPrice WHERE ProductID = @ProductId;
END
GO

CREATE PROCEDURE dbo.GetProductCountWithOutput
    @CategoryId INT,
    @ProductCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @ProductCount = COUNT(*) FROM Products WHERE CategoryID = @CategoryId;
END
GO

CREATE PROCEDURE dbo.GetNoResults
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProductID AS product_id, ProductName AS product_name, SupplierID AS supplier_id, CategoryID AS category_id,
           QuantityPerUnit AS quantity_per_unit, UnitPrice AS unit_price, UnitsInStock AS units_in_stock,
           UnitsOnOrder AS units_on_order, ReorderLevel AS reorder_level, Discontinued AS discontinued
    FROM Products
    WHERE 1 = 0;
END
GO
