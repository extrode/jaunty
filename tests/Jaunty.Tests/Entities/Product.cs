using System.Data;

using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Tests.Entities;

public class Product : IEntity<int>, IMapped<Product>
{
    private static readonly string[] array = [
        "ProductId", "ProductName", "SupplierId", "CategoryId",
            "QuantityPerUnit", "UnitPrice", "UnitsInStock", "UnitsOnOrder",
            "ReorderLevel", "Discontinued"
    ];

    [Ignore]
    public int Id
    {
        get => ProductId;
        set => ProductId = value;
    }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int? SupplierId { get; set; }

    public short? CategoryId { get; set; }

    public string? QuantityPerUnit { get; set; }

    public decimal? UnitPrice { get; set; }

    public short? UnitsInStock { get; set; }

    public short? UnitsOnOrder { get; set; }

    public short? ReorderLevel { get; set; }

    public bool Discontinued { get; set; }

    public static ReadOnlySpan<string> ColumnNames => array.AsSpan();

    public static Product ReadEntity(IDataReader reader, ReadOnlySpan<int> ordinals) => new()
    {
        ProductId = reader.GetInt32(ordinals[0]),
        ProductName = reader.GetString(ordinals[1]),
        SupplierId = reader.IsDBNull(ordinals[2]) ? null : reader.GetInt32(ordinals[2]),
        CategoryId = reader.IsDBNull(ordinals[3]) ? null : reader.GetInt16(ordinals[3]),
        QuantityPerUnit = reader.IsDBNull(ordinals[4]) ? null : reader.GetString(ordinals[4]),
        UnitPrice = reader.IsDBNull(ordinals[5]) ? null : reader.GetDecimal(ordinals[5]),
        UnitsInStock = reader.IsDBNull(ordinals[6]) ? null : reader.GetInt16(ordinals[6]),
        UnitsOnOrder = reader.IsDBNull(ordinals[7]) ? null : reader.GetInt16(ordinals[7]),
        ReorderLevel = reader.IsDBNull(ordinals[8]) ? null : reader.GetInt16(ordinals[8]),
        Discontinued = reader.GetBoolean(ordinals[9])
    };
}
