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

    // IMapped<T>.ReadEntity is a static abstract interface member on net8.0+ and an instance
    // method on net472, which does not support static abstract members.
#if NET8_0_OR_GREATER
    public static Product ReadEntity(IDataReader reader)
#else
    public Product ReadEntity(IDataReader reader)
#endif
    {
        var ordinal = new OrdinalCache(reader);

        return new Product
        {
            ProductId = reader.GetInt32(ordinal["ProductId"]),
            ProductName = reader.GetString(ordinal["ProductName"]),
            SupplierId = reader.IsDBNull(ordinal["SupplierId"]) ? null : reader.GetInt32(ordinal["SupplierId"]),
            CategoryId = reader.IsDBNull(ordinal["CategoryId"]) ? null : reader.GetInt16(ordinal["CategoryId"]),
            QuantityPerUnit = reader.IsDBNull(ordinal["QuantityPerUnit"]) ? null : reader.GetString(ordinal["QuantityPerUnit"]),
            UnitPrice = reader.IsDBNull(ordinal["UnitPrice"]) ? null : Convert.ToDecimal(reader.GetValue(ordinal["UnitPrice"])),
            UnitsInStock = reader.IsDBNull(ordinal["UnitsInStock"]) ? null : reader.GetInt16(ordinal["UnitsInStock"]),
            UnitsOnOrder = reader.IsDBNull(ordinal["UnitsOnOrder"]) ? null : reader.GetInt16(ordinal["UnitsOnOrder"]),
            ReorderLevel = reader.IsDBNull(ordinal["ReorderLevel"]) ? null : reader.GetInt16(ordinal["ReorderLevel"]),
            Discontinued = reader.GetBoolean(ordinal["Discontinued"])
        };
    }

    private readonly struct OrdinalCache
    {
        private readonly IDataReader _reader;
        private readonly Dictionary<string, int> _cache;

        public OrdinalCache(IDataReader reader)
        {
            _reader = reader;
            _cache = [];
        }

        public int this[string columnName] => _cache.TryGetValue(columnName, out int ordinal) ? ordinal : _cache[columnName] = _reader.GetOrdinal(columnName);
    }
}