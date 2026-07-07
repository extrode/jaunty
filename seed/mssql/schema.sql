-- Reference DDL for the eShopOnWeb Jaunty torture-test (SQL Server 2022).
-- Reference copy only: runtime schema creation is done by the embedded C# DDL in
-- src/Infrastructure/Data/Persistence/SqlServerSchema.cs. Column shapes mirror Rows.cs.

IF OBJECT_ID(N'OrderItems', N'U') IS NOT NULL DROP TABLE OrderItems;
IF OBJECT_ID(N'Orders', N'U') IS NOT NULL DROP TABLE Orders;
IF OBJECT_ID(N'BasketItems', N'U') IS NOT NULL DROP TABLE BasketItems;
IF OBJECT_ID(N'Baskets', N'U') IS NOT NULL DROP TABLE Baskets;
IF OBJECT_ID(N'Catalog', N'U') IS NOT NULL DROP TABLE Catalog;
IF OBJECT_ID(N'CatalogTypes', N'U') IS NOT NULL DROP TABLE CatalogTypes;
IF OBJECT_ID(N'CatalogBrands', N'U') IS NOT NULL DROP TABLE CatalogBrands;

IF OBJECT_ID(N'CatalogBrands', N'U') IS NULL
CREATE TABLE CatalogBrands (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    Brand   NVARCHAR(255) NOT NULL
);

IF OBJECT_ID(N'CatalogTypes', N'U') IS NULL
CREATE TABLE CatalogTypes (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    Type    NVARCHAR(255) NOT NULL
);

IF OBJECT_ID(N'Catalog', N'U') IS NULL
CREATE TABLE Catalog (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(255) NOT NULL,
    Description     NVARCHAR(MAX) NOT NULL,
    Price           DECIMAL(18,2) NOT NULL,
    PictureUri      NVARCHAR(1000) NULL,
    CatalogTypeId   INT NOT NULL,
    CatalogBrandId  INT NOT NULL
);

IF OBJECT_ID(N'Baskets', N'U') IS NULL
CREATE TABLE Baskets (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    BuyerId NVARCHAR(255) NOT NULL
);

IF OBJECT_ID(N'BasketItems', N'U') IS NULL
CREATE TABLE BasketItems (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    UnitPrice       DECIMAL(18,2) NOT NULL,
    Quantity        INT NOT NULL,
    CatalogItemId   INT NOT NULL,
    BasketId        INT NOT NULL
);

IF OBJECT_ID(N'Orders', N'U') IS NULL
CREATE TABLE Orders (
    Id                      INT IDENTITY(1,1) PRIMARY KEY,
    BuyerId                 NVARCHAR(255) NOT NULL,
    OrderDate               NVARCHAR(64) NOT NULL,
    ShipToAddress_Street    NVARCHAR(255) NOT NULL,
    ShipToAddress_City      NVARCHAR(255) NOT NULL,
    ShipToAddress_State     NVARCHAR(255) NULL,
    ShipToAddress_Country   NVARCHAR(255) NOT NULL,
    ShipToAddress_ZipCode   NVARCHAR(64) NOT NULL
);

IF OBJECT_ID(N'OrderItems', N'U') IS NULL
CREATE TABLE OrderItems (
    Id                          INT IDENTITY(1,1) PRIMARY KEY,
    OrderId                     INT NOT NULL,
    UnitPrice                   DECIMAL(18,2) NOT NULL,
    Units                       INT NOT NULL,
    ItemOrdered_CatalogItemId   INT NOT NULL,
    ItemOrdered_ProductName     NVARCHAR(255) NOT NULL,
    ItemOrdered_PictureUri      NVARCHAR(1000) NOT NULL
);
