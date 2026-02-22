-- MariaDB/MySQL Test Database Setup Script
-- Run: mysql -u root -p Northwind < mariadb-setup.sql

USE Northwind;

-- Drop existing procedures if they exist
DROP PROCEDURE IF EXISTS GetAllProducts;
DROP PROCEDURE IF EXISTS GetProductsByCategory;
DROP PROCEDURE IF EXISTS GetProductCount;
DROP PROCEDURE IF EXISTS GetProductCountWithOutput;
DROP PROCEDURE IF EXISTS GetCategoryWithProductCount;

DELIMITER //

-- Get all products
CREATE PROCEDURE GetAllProducts()
BEGIN
  SELECT product_id AS ProductID, product_name AS ProductName, 
         unit_price AS UnitPrice, units_in_stock AS UnitsInStock, 
         category_id AS CategoryID
  FROM products
  ORDER BY product_id;
END //

-- Get products by category
CREATE PROCEDURE GetProductsByCategory(IN p_CategoryId INT)
BEGIN
  SELECT product_id AS ProductID, product_name AS ProductName,
         unit_price AS UnitPrice, units_in_stock AS UnitsInStock,
         category_id AS CategoryID
  FROM products
  WHERE category_id = p_CategoryId
  ORDER BY product_id;
END //

-- Get product count
CREATE PROCEDURE GetProductCount()
BEGIN
  SELECT COUNT(*) AS Count FROM products;
END //

-- Get product count with output parameter
CREATE PROCEDURE GetProductCountWithOutput(OUT p_ProductCount INT)
BEGIN
  SELECT COUNT(*) INTO p_ProductCount FROM products;
END //

-- Get category with product count
CREATE PROCEDURE GetCategoryWithProductCount(
    IN p_CategoryId INT,
    OUT p_ProductCount INT)
BEGIN
  SELECT category_id AS CategoryID, category_name AS CategoryName, 
         description AS Description
  FROM categories
  WHERE category_id = p_CategoryId;
  
  SELECT COUNT(*) INTO p_ProductCount FROM products WHERE category_id = p_CategoryId;
END //

DELIMITER ;

-- Verify procedures were created
SHOW PROCEDURE STATUS WHERE Db = 'Northwind';
