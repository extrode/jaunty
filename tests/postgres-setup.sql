-- PostgreSQL test functions/procedures for Jaunty integration tests.
-- Run: psql -U postgres -d postgres -f postgres-setup.sql

SELECT 'CREATE DATABASE "Northwind"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'Northwind')\gexec
\c Northwind

DROP FUNCTION IF EXISTS GetAllProducts();
DROP FUNCTION IF EXISTS GetProductsByCategory(INTEGER);
DROP FUNCTION IF EXISTS GetProductById(INTEGER);
DROP FUNCTION IF EXISTS GetProductCount();
DROP FUNCTION IF EXISTS GetProductCountByCategory(INTEGER);
DROP FUNCTION IF EXISTS UpdateProductPrice(INTEGER, DECIMAL);
DROP FUNCTION IF EXISTS GetProductCountWithOutput(INTEGER);
DROP FUNCTION IF EXISTS GetNoResults();

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
)
LANGUAGE SQL
AS $$
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p;
$$;

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
)
LANGUAGE SQL
AS $$
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p
    WHERE p.category_id = p_category_id;
$$;

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
)
LANGUAGE SQL
AS $$
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p
    WHERE p.product_id = p_product_id;
$$;

CREATE OR REPLACE FUNCTION GetProductCount()
RETURNS INTEGER
LANGUAGE SQL
AS $$
    SELECT COUNT(*)::INTEGER FROM products;
$$;

CREATE OR REPLACE FUNCTION GetProductCountByCategory(p_category_id INTEGER)
RETURNS INTEGER
LANGUAGE SQL
AS $$
    SELECT COUNT(*)::INTEGER FROM products WHERE category_id = p_category_id;
$$;

CREATE OR REPLACE FUNCTION UpdateProductPrice(p_product_id INTEGER, p_new_price DECIMAL)
RETURNS INTEGER
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE products SET unit_price = p_new_price WHERE product_id = p_product_id;
    RETURN 0;
END;
$$;

CREATE OR REPLACE FUNCTION GetProductCountWithOutput(
    IN p_category_id INTEGER,
    INOUT p_product_count INTEGER
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT COUNT(*)::INTEGER INTO p_product_count
    FROM products
    WHERE category_id = p_category_id;
END;
$$;

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
)
LANGUAGE SQL
AS $$
    SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
           p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
           p.reorder_level, p.discontinued
    FROM products p
    WHERE 1 = 0;
$$;
