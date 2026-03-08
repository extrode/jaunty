using System.Data;
using System.Data.SQLite;

namespace Jaunty.Fluent.Tests.Helpers;

/// <summary>
/// Provides an isolated in-memory SQLite database for write tests.
/// Each instance creates a fresh database with seeded data.
/// The database is destroyed when disposed (connection closed).
/// </summary>
public class InMemoryDatabase : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public IDbConnection Connection => _connection;

    public InMemoryDatabase()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        CreateSchema();
        SeedData();
    }

    private void CreateSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );

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
                FOREIGN KEY (category_id) REFERENCES categories(category_id)
            );
        ";
        cmd.ExecuteNonQuery();
    }

    private void SeedData()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO categories (category_id, category_name, description)
            VALUES (1, 'Beverages', 'Soft drinks, coffees, teas, beers, and ales');
            INSERT INTO categories (category_id, category_name, description)
            VALUES (2, 'Condiments', 'Sweet and savory sauces, relishes, spreads, and seasonings');

            INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, units_on_order, reorder_level, discontinued)
            VALUES ('Chai', 1, 1, '10 boxes x 20 bags', 18.00, 39, 0, 10, 0);
            INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, units_on_order, reorder_level, discontinued)
            VALUES ('Chang', 1, 1, '24 - 12 oz bottles', 19.00, 17, 40, 25, 0);
            INSERT INTO products (product_name, supplier_id, category_id, quantity_per_unit, unit_price, units_in_stock, units_on_order, reorder_level, discontinued)
            VALUES ('Aniseed Syrup', 1, 2, '12 - 550 ml bottles', 10.00, 13, 70, 25, 0);
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Close();
        _connection?.Dispose();
        _disposed = true;
    }
}