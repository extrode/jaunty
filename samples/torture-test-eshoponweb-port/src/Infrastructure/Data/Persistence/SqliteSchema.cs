using System.Data;

namespace Microsoft.eShopWeb.Infrastructure.Data.Persistence;

// Hand-written SQLite DDL for the 7 Catalog/Basket/Order tables. There is no EF migration path
// once CatalogContext is removed, so the schema is created explicitly at startup (test fixtures
// and any SQLite-backed run). Column names/shapes match the Row types in Rows.cs, which in turn
// mirror the former EF Core IEntityTypeConfiguration mappings.
public static class SqliteSchema
{
    public const string CreateScript = @"
CREATE TABLE IF NOT EXISTS CatalogBrands (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Brand   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS CatalogTypes (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Type    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Catalog (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Name            TEXT NOT NULL,
    Description     TEXT NOT NULL,
    Price           TEXT NOT NULL,
    PictureUri      TEXT NULL,
    CatalogTypeId   INTEGER NOT NULL,
    CatalogBrandId  INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Baskets (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    BuyerId TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS BasketItems (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    UnitPrice       TEXT NOT NULL,
    Quantity        INTEGER NOT NULL,
    CatalogItemId   INTEGER NOT NULL,
    BasketId        INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Orders (
    Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    BuyerId                 TEXT NOT NULL,
    OrderDate               TEXT NOT NULL,
    ShipToAddress_Street    TEXT NOT NULL,
    ShipToAddress_City      TEXT NOT NULL,
    ShipToAddress_State     TEXT NULL,
    ShipToAddress_Country   TEXT NOT NULL,
    ShipToAddress_ZipCode   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS OrderItems (
    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId                     INTEGER NOT NULL,
    UnitPrice                   TEXT NOT NULL,
    Units                       INTEGER NOT NULL,
    ItemOrdered_CatalogItemId   INTEGER NOT NULL,
    ItemOrdered_ProductName     TEXT NOT NULL,
    ItemOrdered_PictureUri      TEXT NOT NULL
);
";

    /// <summary>
    /// Executes the schema-creation DDL against the supplied open connection.
    /// Idempotent (uses CREATE TABLE IF NOT EXISTS).
    /// </summary>
    public static void EnsureCreated(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = CreateScript;
        command.ExecuteNonQuery();
    }
}
