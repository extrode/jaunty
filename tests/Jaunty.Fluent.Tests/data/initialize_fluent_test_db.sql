-- Schema for Jaunty.Fluent.Tests
-- Products table
CREATE TABLE products (
    product_id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_name TEXT NOT NULL,
    supplier_id INTEGER,
    category_id INTEGER,
    quantity_per_unit TEXT,
    unit_price REAL,
    units_in_stock INTEGER,
    units_on_order INTEGER,
    reorder_level INTEGER,
    discontinued INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (supplier_id) REFERENCES suppliers(supplier_id),
    FOREIGN KEY (category_id) REFERENCES categories(category_id)
);

-- Categories table
CREATE TABLE categories (
    category_id INTEGER PRIMARY KEY AUTOINCREMENT,
    category_name TEXT NOT NULL,
    description TEXT,
    picture BLOB
);

-- Suppliers table
CREATE TABLE suppliers (
    supplier_id INTEGER PRIMARY KEY AUTOINCREMENT,
    company_name TEXT NOT NULL,
    contact_name TEXT,
    contact_title TEXT,
    address TEXT,
    city TEXT,
    region TEXT,
    postal_code TEXT,
    country TEXT,
    phone TEXT,
    fax TEXT,
    homepage TEXT
);

-- Insert sample data into Categories
INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks, coffees, teas, beers, and ales');
INSERT INTO categories (category_name, description) VALUES ('Condiments', 'Sweet and savory sauces, relishes, spreads, and seasonings');
INSERT INTO categories (category_name, description) VALUES ('Confections', 'Desserts, candies, and sweet breads');

-- Insert sample data into Suppliers
INSERT INTO suppliers (company_name, contact_name, country) VALUES ('Exotic Liquid', 'Charlotte Cooper', 'UK');
INSERT INTO suppliers (company_name, contact_name, country) VALUES ('New Orleans Cajun Delights', 'Shelley Burke', 'USA');
INSERT INTO suppliers (company_name, contact_name, country) VALUES ('Grandma Kelly''s Homestead', 'Regina Murphy', 'USA');

-- Insert sample data into Products
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Chai', 1, 1, 18.00, 39);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Chang', 1, 1, 19.00, 17);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Aniseed Syrup', 1, 2, 10.00, 13);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Chef Anton''s Cajun Seasoning', 2, 2, 22.00, 53);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Chef Anton''s Gumbo Mix', 2, 2, 21.35, 0);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Grandma''s Boysenberry Spread', 3, 2, 25.00, 120);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Uncle Bob''s Organic Dried Pears', 3, 3, 30.00, 15);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Cheap Product', 1, 1, 5.00, 100);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Expensive Product', 2, 2, 60.00, 10);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Another Mid Product', 3, 3, 35.00, 20);
INSERT INTO products (product_name, supplier_id, category_id, unit_price, units_in_stock) VALUES ('Luxury Item', 1, 1, 120.00, 5);

-- Orders table (for 4-entity join tests: Product + Category + Supplier + Order)
CREATE TABLE orders (
    order_id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id TEXT,
    employee_id INTEGER,
    order_date TEXT,
    required_date TEXT,
    shipped_date TEXT,
    ship_via INTEGER,
    freight REAL,
    ship_name TEXT,
    ship_address TEXT,
    ship_city TEXT,
    ship_region TEXT,
    ship_postal_code TEXT,
    ship_country TEXT
);

-- Insert sample orders
INSERT INTO orders (customer_id, employee_id, order_date, ship_name, ship_country) VALUES ('ALFKI', 1, '2024-01-10', 'Alfreds Futterkiste', 'Germany');
INSERT INTO orders (customer_id, employee_id, order_date, ship_name, ship_country) VALUES ('ANATR', 2, '2024-01-15', 'Ana Trujillo', 'Mexico');
INSERT INTO orders (customer_id, employee_id, order_date, ship_name, ship_country) VALUES ('ALFKI', 1, '2024-02-01', 'Alfreds Futterkiste', 'Germany');
