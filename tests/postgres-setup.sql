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
    "ProductId" INTEGER,
    "ProductName" VARCHAR,
    "SupplierId" INTEGER,
    "CategoryId" INTEGER,
    "QuantityPerUnit" VARCHAR,
    "UnitPrice" DECIMAL,
    "UnitsInStock" SMALLINT,
    "UnitsOnOrder" SMALLINT,
    "ReorderLevel" SMALLINT,
    "Discontinued" BOOLEAN
)
LANGUAGE SQL
AS $$
    SELECT p.product_id AS "ProductId", p.product_name AS "ProductName", p.supplier_id AS "SupplierId", p.category_id AS "CategoryId",
           p.quantity_per_unit AS "QuantityPerUnit", p.unit_price AS "UnitPrice", p.units_in_stock AS "UnitsInStock", p.units_on_order AS "UnitsOnOrder",
           p.reorder_level AS "ReorderLevel", p.discontinued AS "Discontinued"
    FROM products p;
$$;

CREATE OR REPLACE FUNCTION GetProductsByCategory(p_category_id INTEGER)
RETURNS TABLE (
    "ProductId" INTEGER,
    "ProductName" VARCHAR,
    "SupplierId" INTEGER,
    "CategoryId" INTEGER,
    "QuantityPerUnit" VARCHAR,
    "UnitPrice" DECIMAL,
    "UnitsInStock" SMALLINT,
    "UnitsOnOrder" SMALLINT,
    "ReorderLevel" SMALLINT,
    "Discontinued" BOOLEAN
)
LANGUAGE SQL
AS $$
    SELECT p.product_id AS "ProductId", p.product_name AS "ProductName", p.supplier_id AS "SupplierId", p.category_id AS "CategoryId",
           p.quantity_per_unit AS "QuantityPerUnit", p.unit_price AS "UnitPrice", p.units_in_stock AS "UnitsInStock", p.units_on_order AS "UnitsOnOrder",
           p.reorder_level AS "ReorderLevel", p.discontinued AS "Discontinued"
    FROM products p
    WHERE p.category_id = p_category_id;
$$;

CREATE OR REPLACE FUNCTION GetProductById(p_product_id INTEGER)
RETURNS TABLE (
    "ProductId" INTEGER,
    "ProductName" VARCHAR,
    "SupplierId" INTEGER,
    "CategoryId" INTEGER,
    "QuantityPerUnit" VARCHAR,
    "UnitPrice" DECIMAL,
    "UnitsInStock" SMALLINT,
    "UnitsOnOrder" SMALLINT,
    "ReorderLevel" SMALLINT,
    "Discontinued" BOOLEAN
)
LANGUAGE SQL
AS $$
    SELECT p.product_id AS "ProductId", p.product_name AS "ProductName", p.supplier_id AS "SupplierId", p.category_id AS "CategoryId",
           p.quantity_per_unit AS "QuantityPerUnit", p.unit_price AS "UnitPrice", p.units_in_stock AS "UnitsInStock", p.units_on_order AS "UnitsOnOrder",
           p.reorder_level AS "ReorderLevel", p.discontinued AS "Discontinued"
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
    "ProductId" INTEGER,
    "ProductName" VARCHAR,
    "SupplierId" INTEGER,
    "CategoryId" INTEGER,
    "QuantityPerUnit" VARCHAR,
    "UnitPrice" DECIMAL,
    "UnitsInStock" SMALLINT,
    "UnitsOnOrder" SMALLINT,
    "ReorderLevel" SMALLINT,
    "Discontinued" BOOLEAN
)
LANGUAGE SQL
AS $$
    SELECT p.product_id AS "ProductId", p.product_name AS "ProductName", p.supplier_id AS "SupplierId", p.category_id AS "CategoryId",
           p.quantity_per_unit AS "QuantityPerUnit", p.unit_price AS "UnitPrice", p.units_in_stock AS "UnitsInStock", p.units_on_order AS "UnitsOnOrder",
           p.reorder_level AS "ReorderLevel", p.discontinued AS "Discontinued"
    FROM products p
    WHERE 1 = 0;
$$;
