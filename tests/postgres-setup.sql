-- PostgreSQL Test Database Setup Script
-- Run: psql -U postgres -d postgres -f postgres-setup.sql

-- Create northwind database if it doesn't exist
SELECT 'CREATE DATABASE northwind' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'northwind')\gexec

-- Connect to northwind database
\c northwind

-- Drop existing procedures
DROP PROCEDURE IF EXISTS get_all_products();
DROP PROCEDURE IF EXISTS get_products_by_category(INTEGER);
DROP PROCEDURE IF EXISTS get_product_count();
DROP PROCEDURE IF EXISTS get_category_with_product_count(INTEGER, INTEGER);

-- Get all products
CREATE OR REPLACE PROCEDURE get_all_products()
LANGUAGE SQL
AS $$
  SELECT product_id, product_name, unit_price, units_in_stock, category_id
  FROM products
  ORDER BY product_id;
$$;

-- Get products by category
CREATE OR REPLACE PROCEDURE get_products_by_category(IN p_category_id INTEGER)
LANGUAGE SQL
AS $$
  SELECT product_id, product_name, unit_price, units_in_stock, category_id
  FROM products
  WHERE category_id = p_category_id
  ORDER BY product_id;
$$;

-- Get product count
CREATE OR REPLACE PROCEDURE get_product_count()
LANGUAGE SQL
AS $$
  SELECT COUNT(*) FROM products;
$$;

-- Get category with product count (for output parameter test)
-- Note: PostgreSQL doesn't support OUTPUT parameters the same way
-- This is a placeholder that may need adjustment
CREATE OR REPLACE PROCEDURE get_category_with_product_count(
    IN p_category_id INTEGER,
    OUT p_product_count INTEGER)
LANGUAGE SQL
AS $$
  SELECT COUNT(*) FROM products WHERE category_id = p_category_id;
$$;

-- Verify procedures were created
SELECT routine_name 
FROM information_schema.routines 
WHERE routine_schema = 'public' 
AND routine_type = 'PROCEDURE'
ORDER BY routine_name;
