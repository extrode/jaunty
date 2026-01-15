using System.Data;

using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Tests.Entities;

public class Product : IEntity<int>, IMapped<Product>
{
    [Ignore]
    public int Id
    {
        get => ProductId;
        set => ProductId = value;
    }

    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    
    [Column("supplier_id")]
    public int? SupplierId { get; set; }
    
    [Column("category_id")]
    public short? CategoryId { get; set; }
    
    [Column("quantity_per_unit")]
    public string? QuantityPerUnit { get; set; }
    
    [Column("unit_price")]
    public decimal? UnitPrice { get; set; }
    
    [Column("units_in_stock")]
    public short? UnitsInStock { get; set; }
    
    [Column("units_on_order")]
    public short? UnitsOnOrder { get; set; }
    
    [Column("reorder_level")]
    public short? ReorderLevel { get; set; }
    
    [Column("discontinued")]
    public bool Discontinued { get; set; }

    public static Product ReadEntity(IDataReader reader)
    {
        var ordinal = new OrdinalCache(reader);

        return new()
        {
            ProductId = reader.GetInt32(ordinal["ProductId"]),
            ProductName = reader.GetString(ordinal["ProductName"]),
            SupplierId = reader.IsDBNull(ordinal["SupplierId"]) ? null : reader.GetInt32(ordinal["SupplierId"]),
            CategoryId = reader.IsDBNull(ordinal["CategoryId"]) ? null : reader.GetInt16(ordinal["CategoryId"]),
            QuantityPerUnit = reader.IsDBNull(ordinal["QuantityPerUnit"]) ? null : reader.GetString(ordinal["QuantityPerUnit"]),
            UnitPrice = reader.IsDBNull(ordinal["UnitPrice"]) ? null : reader.GetDecimal(ordinal["UnitPrice"]),
            UnitsInStock = reader.IsDBNull(ordinal["UnitsInStock"]) ? null : reader.GetInt16(ordinal["UnitsInStock"]),
            UnitsOnOrder = reader.IsDBNull(ordinal["UnitsOnOrder"]) ? null : reader.GetInt16(ordinal["UnitsOnOrder"]),
            ReorderLevel = reader.IsDBNull(ordinal["ReorderLevel"]) ? null : reader.GetInt16(ordinal["ReorderLevel"]),
            Discontinued = reader.GetBoolean(ordinal["Discontinued"])
        };
    }

    private readonly struct OrdinalCache(IDataReader reader)
    {
        private readonly Dictionary<string, int> _cache = [];

        public int this[string columnName] => _cache.TryGetValue(columnName, out var ordinal) ? ordinal : _cache[columnName] = reader.GetOrdinal(columnName);
    }
}
