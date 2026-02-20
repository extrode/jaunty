-- Jaunty Test Stored Procedures for PostgreSQL
-- Run this script against a Northwind database to create the stored procedures
-- needed by the Jaunty integration tests.
--
-- Prerequisites:
--   1. A PostgreSQL instance with a Northwind database
--   2. Tables: products, categories (with standard Northwind schema)
--
-- Usage:
--   psql -h localhost -d northwind -U postgres -f create-stored-procedures.sql

-- =============================================
-- GetAllProducts: Returns all products
-- Tests: ExecuteStoredProcedure<T>(procedureName)
-- =============================================
CREATE OR REPLACE FUNCTION GetAllProducts()
RETURNS TABLE (
    product_id INTEGER,
    product_name VARCHAR,
    supplier_id INTEGER,
    category_id INTEGER,
    quantity_per_unit VARCHAR,
    unit_price DECIMAL,
    units_in_stock SMALLINT,
    units_on_order SMALLINT,
    reorder_level SMALLINT,
    discontinued BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- GetProductsByCategory: Returns products filtered by category_id
-- Tests: ExecuteStoredProcedure<T>(procedureName, parameters)
-- =============================================
CREATE OR REPLACE FUNCTION GetProductsByCategory(p_category_id INTEGER)
RETURNS TABLE (
    product_id INTEGER,
    product_name VARCHAR,
    supplier_id INTEGER,
    category_id INTEGER,
    quantity_per_unit VARCHAR,
    unit_price DECIMAL,
    units_in_stock SMALLINT,
    units_on_order SMALLINT,
    reorder_level SMALLINT,
    discontinued BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p
    WHERE p.category_id = p_category_id;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- GetProductById: Returns a single product by ID
-- Tests: ExecuteStoredProcedureFirst, ExecuteStoredProcedureSingle
-- =============================================
CREATE OR REPLACE FUNCTION GetProductById(p_product_id INTEGER)
RETURNS TABLE (
    product_id INTEGER,
    product_name VARCHAR,
    supplier_id INTEGER,
    category_id INTEGER,
    quantity_per_unit VARCHAR,
    unit_price DECIMAL,
    units_in_stock SMALLINT,
    units_on_order SMALLINT,
    reorder_level SMALLINT,
    discontinued BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p
    WHERE p.product_id = p_product_id;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- GetProductCount: Returns scalar count of all products
-- Tests: ExecuteStoredProcedureScalar<T>
-- =============================================
CREATE OR REPLACE FUNCTION GetProductCount()
RETURNS INTEGER AS $$
DECLARE
    result INTEGER;
BEGIN
    SELECT COUNT(*) INTO result FROM products;
    RETURN result;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- GetProductCountByCategory: Returns scalar count with parameter
-- Tests: ExecuteStoredProcedureScalar<T>(procedureName, parameters)
-- =============================================
CREATE OR REPLACE FUNCTION GetProductCountByCategory(p_category_id INTEGER)
RETURNS INTEGER AS $$
DECLARE
    result INTEGER;
BEGIN
    SELECT COUNT(*) INTO result FROM products WHERE category_id = p_category_id;
    RETURN result;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- UpdateProductPrice: Non-query that updates unit_price
-- Tests: ExecuteStoredProcedureNonQuery
-- Note: Tests should wrap in transaction and rollback
-- =============================================
CREATE OR REPLACE FUNCTION UpdateProductPrice(p_product_id INTEGER, p_new_price DECIMAL)
RETURNS VOID AS $$
BEGIN
    UPDATE products SET unit_price = p_new_price WHERE product_id = p_product_id;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- GetProductCountWithOutput: Returns count via INOUT parameter
-- Tests: ExecuteStoredProcedure with SpParameters (output)
-- Note: PostgreSQL uses INOUT parameters instead of OUTPUT parameters
-- =============================================
CREATE OR REPLACE FUNCTION GetProductCountWithOutput(
    p_category_id INTEGER,
    INOUT p_product_count INTEGER DEFAULT 0
) AS $$
BEGIN
    SELECT COUNT(*) INTO p_product_count FROM products WHERE category_id = p_category_id;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- GetNoResults: Returns empty result set
-- Tests: FirstOrDefault returns null, First throws
-- =============================================
CREATE OR REPLACE FUNCTION GetNoResults()
RETURNS TABLE (
    product_id INTEGER,
    product_name VARCHAR,
    supplier_id INTEGER,
    category_id INTEGER,
    quantity_per_unit VARCHAR,
    unit_price DECIMAL,
    units_in_stock SMALLINT,
    units_on_order SMALLINT,
    reorder_level SMALLINT,
    discontinued BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p
    WHERE 1 = 0;
END;
$$ LANGUAGE plpgsql;

DO $$ BEGIN RAISE NOTICE 'All Jaunty test stored procedures created successfully.'; END $$;
