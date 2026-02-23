-- MySQL test stored procedures for Jaunty integration tests.
-- Run: mysql -u root -p < mysql-setup.sql

CREATE DATABASE IF NOT EXISTS Northwind;
USE Northwind;

DROP PROCEDURE IF EXISTS GetAllProducts;
DROP PROCEDURE IF EXISTS GetProductsByCategory;
DROP PROCEDURE IF EXISTS GetProductById;
DROP PROCEDURE IF EXISTS GetProductCount;
DROP PROCEDURE IF EXISTS GetProductCountByCategory;
DROP PROCEDURE IF EXISTS UpdateProductPrice;
DROP PROCEDURE IF EXISTS GetProductCountWithOutput;
DROP PROCEDURE IF EXISTS GetNoResults;

DELIMITER //

CREATE PROCEDURE GetAllProducts()
BEGIN
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products;
END //

CREATE PROCEDURE GetProductsByCategory(IN p_CategoryId INT)
BEGIN
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products
    WHERE category_id = p_CategoryId;
END //

CREATE PROCEDURE GetProductById(IN p_ProductId INT)
BEGIN
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products
    WHERE product_id = p_ProductId;
END //

CREATE PROCEDURE GetProductCount()
BEGIN
    SELECT COUNT(*) FROM products;
END //

CREATE PROCEDURE GetProductCountByCategory(IN p_CategoryId INT)
BEGIN
    SELECT COUNT(*) FROM products WHERE category_id = p_CategoryId;
END //

CREATE PROCEDURE UpdateProductPrice(IN p_ProductId INT, IN p_NewPrice DECIMAL(18,2))
BEGIN
    UPDATE products SET unit_price = p_NewPrice WHERE product_id = p_ProductId;
END //

CREATE PROCEDURE GetProductCountWithOutput(IN p_CategoryId INT, OUT p_ProductCount INT)
BEGIN
    SELECT COUNT(*) INTO p_ProductCount FROM products WHERE category_id = p_CategoryId;
END //

CREATE PROCEDURE GetNoResults()
BEGIN
    SELECT product_id, product_name, supplier_id, category_id,
           quantity_per_unit, unit_price, units_in_stock, units_on_order,
           reorder_level, discontinued
    FROM products
    WHERE 1 = 0;
END //

DELIMITER ;
