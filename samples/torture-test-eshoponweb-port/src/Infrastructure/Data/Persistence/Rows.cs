using Jaunty.Attributes;

namespace Microsoft.eShopWeb.Infrastructure.Data.Persistence;

// Flat "Row" POCOs consumed directly by Jaunty (public parameterless ctor + public settable
// properties). Column names / table names mirror the shapes previously produced by the EF Core
// IEntityTypeConfiguration classes under Data/Config so the hand-written SQLite DDL in
// SqliteSchema and the seed data continue to match the domain expectations.
//
// Owned value objects (Address, CatalogItemOrdered) are flattened onto the parent Row, matching
// EF's OwnsOne column layout (owner property name is prefixed, e.g. ShipToAddress_Street).

[Table("Catalog")]
public sealed class CatalogItemRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? PictureUri { get; set; }
    public int CatalogTypeId { get; set; }
    public int CatalogBrandId { get; set; }
}

[Table("CatalogBrands")]
public sealed class CatalogBrandRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;
}

[Table("CatalogTypes")]
public sealed class CatalogTypeRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Type { get; set; } = string.Empty;
}

[Table("Baskets")]
public sealed class BasketRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string BuyerId { get; set; } = string.Empty;
}

[Table("BasketItems")]
public sealed class BasketItemRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int CatalogItemId { get; set; }
    public int BasketId { get; set; }
}

[Table("Orders")]
public sealed class OrderRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    // Stored as an ISO-8601 round-trip string. SQLite has no native DateTimeOffset type and
    // Jaunty's reflection value-converter cannot coerce a TEXT column into DateTimeOffset
    // (DateTimeOffset is not IConvertible), so the mapping is done explicitly in AggregateMappers.
    public string OrderDate { get; set; } = string.Empty;

    // Flattened Address owned value object (EF OwnsOne -> ShipToAddress_* columns).
    [Column("ShipToAddress_Street")]
    public string ShipToAddress_Street { get; set; } = string.Empty;

    [Column("ShipToAddress_City")]
    public string ShipToAddress_City { get; set; } = string.Empty;

    [Column("ShipToAddress_State")]
    public string? ShipToAddress_State { get; set; }

    [Column("ShipToAddress_Country")]
    public string ShipToAddress_Country { get; set; } = string.Empty;

    [Column("ShipToAddress_ZipCode")]
    public string ShipToAddress_ZipCode { get; set; } = string.Empty;
}

[Table("OrderItems")]
public sealed class OrderItemRow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int OrderId { get; set; }
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }

    // Flattened CatalogItemOrdered owned value object (EF OwnsOne -> ItemOrdered_* columns).
    [Column("ItemOrdered_CatalogItemId")]
    public int ItemOrdered_CatalogItemId { get; set; }

    [Column("ItemOrdered_ProductName")]
    public string ItemOrdered_ProductName { get; set; } = string.Empty;

    [Column("ItemOrdered_PictureUri")]
    public string ItemOrdered_PictureUri { get; set; } = string.Empty;
}
