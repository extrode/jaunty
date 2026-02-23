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

#if NET8_0_OR_GREATER
    public static Product ReadEntity(IDataReader reader)
#else
    public Product ReadEntity(IDataReader reader)
#endif
    {
        var ordinal = new OrdinalCache(reader);

        return new Product
        {
            ProductId = reader.GetInt32(ordinal.Required("ProductId", "product_id", "ProductID")),
            ProductName = reader.GetString(ordinal.Required("ProductName", "product_name")),
            SupplierId = ordinal.GetNullableInt32(reader, "SupplierId", "supplier_id", "SupplierID"),
            CategoryId = ordinal.GetNullableInt16(reader, "CategoryId", "category_id", "CategoryID"),
            QuantityPerUnit = ordinal.GetNullableString(reader, "QuantityPerUnit", "quantity_per_unit"),
            UnitPrice = ordinal.GetNullableDecimal(reader, "UnitPrice", "unit_price"),
            UnitsInStock = ordinal.GetNullableInt16(reader, "UnitsInStock", "units_in_stock"),
            UnitsOnOrder = ordinal.GetNullableInt16(reader, "UnitsOnOrder", "units_on_order"),
            ReorderLevel = ordinal.GetNullableInt16(reader, "ReorderLevel", "reorder_level"),
            Discontinued = ordinal.GetBooleanOrDefault(reader, false, "Discontinued", "discontinued")
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

        public int Required(params string[] columnNames)
        {
            for (var i = 0; i < columnNames.Length; i++)
            {
                if (TryGetOrdinal(columnNames[i], out var ordinal))
                    return ordinal;
            }

            throw new ArgumentOutOfRangeException(nameof(columnNames), $"None of the required columns were found: {string.Join(", ", columnNames)}.");
        }

        public bool TryGetOrdinal(string columnName, out int ordinal)
        {
            if (_cache.TryGetValue(columnName, out ordinal))
                return true;

            try
            {
                ordinal = _reader.GetOrdinal(columnName);
                _cache[columnName] = ordinal;
                return true;
            }
            catch (IndexOutOfRangeException)
            {
                ordinal = -1;
                return false;
            }
        }

        public int? GetNullableInt32(IDataReader reader, params string[] columnNames)
        {
            if (!TryResolve(columnNames, out var ordinal) || reader.IsDBNull(ordinal))
                return null;
            return reader.GetInt32(ordinal);
        }

        public short? GetNullableInt16(IDataReader reader, params string[] columnNames)
        {
            if (!TryResolve(columnNames, out var ordinal) || reader.IsDBNull(ordinal))
                return null;
            return reader.GetInt16(ordinal);
        }

        public decimal? GetNullableDecimal(IDataReader reader, params string[] columnNames)
        {
            if (!TryResolve(columnNames, out var ordinal) || reader.IsDBNull(ordinal))
                return null;
            return reader.GetDecimal(ordinal);
        }

        public string? GetNullableString(IDataReader reader, params string[] columnNames)
        {
            if (!TryResolve(columnNames, out var ordinal) || reader.IsDBNull(ordinal))
                return null;
            return reader.GetString(ordinal);
        }

        public bool GetBooleanOrDefault(IDataReader reader, bool defaultValue, params string[] columnNames)
        {
            if (!TryResolve(columnNames, out var ordinal) || reader.IsDBNull(ordinal))
                return defaultValue;
            return reader.GetBoolean(ordinal);
        }

        private bool TryResolve(string[] columnNames, out int ordinal)
        {
            for (var i = 0; i < columnNames.Length; i++)
            {
                if (TryGetOrdinal(columnNames[i], out ordinal))
                    return true;
            }

            ordinal = -1;
            return false;
        }
    }
}
