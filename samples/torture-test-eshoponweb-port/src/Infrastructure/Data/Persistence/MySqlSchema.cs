using System;
using System.Data;

namespace Microsoft.eShopWeb.Infrastructure.Data.Persistence;

public static class MySqlSchema
{
    public const string CreateScript = @"
CREATE TABLE IF NOT EXISTS CatalogBrands (
    Id      INT AUTO_INCREMENT PRIMARY KEY,
    Brand   VARCHAR(255) NOT NULL
);

CREATE TABLE IF NOT EXISTS CatalogTypes (
    Id      INT AUTO_INCREMENT PRIMARY KEY,
    Type    VARCHAR(255) NOT NULL
);

CREATE TABLE IF NOT EXISTS Catalog (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    Name            VARCHAR(255) NOT NULL,
    Description     TEXT NOT NULL,
    Price           DECIMAL(18,2) NOT NULL,
    PictureUri      VARCHAR(1000) NULL,
    CatalogTypeId   INT NOT NULL,
    CatalogBrandId  INT NOT NULL
);

CREATE TABLE IF NOT EXISTS Baskets (
    Id      INT AUTO_INCREMENT PRIMARY KEY,
    BuyerId VARCHAR(255) NOT NULL
);

CREATE TABLE IF NOT EXISTS BasketItems (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    Quantity        INT NOT NULL,
    CatalogItemId   INT NOT NULL,
    BasketId        INT NOT NULL
);

CREATE TABLE IF NOT EXISTS Orders (
    Id                      INT AUTO_INCREMENT PRIMARY KEY,
    BuyerId                 VARCHAR(255) NOT NULL,
    OrderDate               VARCHAR(64) NOT NULL,
    ShipToAddress_Street    VARCHAR(255) NOT NULL,
    ShipToAddress_City      VARCHAR(255) NOT NULL,
    ShipToAddress_State     VARCHAR(255) NULL,
    ShipToAddress_Country   VARCHAR(255) NOT NULL,
    ShipToAddress_ZipCode   VARCHAR(64) NOT NULL
);

CREATE TABLE IF NOT EXISTS OrderItems (
    Id                          INT AUTO_INCREMENT PRIMARY KEY,
    OrderId                     INT NOT NULL,
    UnitPrice                   DECIMAL(18,2) NOT NULL,
    Units                       INT NOT NULL,
    ItemOrdered_CatalogItemId   INT NOT NULL,
    ItemOrdered_ProductName     VARCHAR(255) NOT NULL,
    ItemOrdered_PictureUri      VARCHAR(1000) NOT NULL
);
";

    public const string ResetScript = @"
DROP TABLE IF EXISTS OrderItems;
DROP TABLE IF EXISTS Orders;
DROP TABLE IF EXISTS BasketItems;
DROP TABLE IF EXISTS Baskets;
DROP TABLE IF EXISTS Catalog;
DROP TABLE IF EXISTS CatalogTypes;
DROP TABLE IF EXISTS CatalogBrands;
";

    public static void EnsureCreated(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        ExecuteBatch(connection, CreateScript);
    }

    public static void Reset(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        ExecuteBatch(connection, ResetScript);
        ExecuteBatch(connection, CreateScript);
    }

    private static void ExecuteBatch(IDbConnection connection, string script)
    {
        foreach (var raw in script.Split(';'))
        {
            var statement = raw.Trim();
            if (statement.Length == 0)
            {
                continue;
            }

            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }
}
