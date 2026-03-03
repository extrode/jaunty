using System.Data;
using System.Runtime.CompilerServices;

namespace Jaunty.Tests.Unit.SourceGeneration;

public class OrdinalMapCacheEntryTests
{
    [Fact]
    public void Resolve_DifferentReaderInstances_WithDifferentColumnOrder_MapsCorrectly()
    {
        using var reader1 = CreateReader(
            ("product_id", typeof(int)),
            ("product_name", typeof(string)),
            ("unit_price", typeof(decimal)),
            1, "Chai", 18m);

        Assert.True(reader1.Read());
        var first = FakeGeneratedProduct.Read(reader1);

        Assert.Equal(1, first.ProductId);
        Assert.Equal("Chai", first.ProductName);
        Assert.Equal(18m, first.UnitPrice);

        using var reader2 = CreateReader(
            ("unit_price", typeof(decimal)),
            ("product_name", typeof(string)),
            ("product_id", typeof(int)),
            19m, "Chang", 2);

        Assert.True(reader2.Read());
        var second = FakeGeneratedProduct.Read(reader2);

        Assert.Equal(2, second.ProductId);
        Assert.Equal("Chang", second.ProductName);
        Assert.Equal(19m, second.UnitPrice);
    }

    [Fact]
    public void Resolve_SameReader_NextResultWithDifferentColumnOrder_MapsCorrectly()
    {
        var table1 = CreateTable(
            ("product_id", typeof(int)),
            ("product_name", typeof(string)),
            ("unit_price", typeof(decimal)),
            3, "Aniseed Syrup", 10m);

        var table2 = CreateTable(
            ("unit_price", typeof(decimal)),
            ("product_name", typeof(string)),
            ("product_id", typeof(int)),
            22m, "Chef Anton's Cajun Seasoning", 4);

        using var reader = new DataTableReader(new[] { table1, table2 });

        Assert.True(reader.Read());
        var first = FakeGeneratedProduct.Read(reader);

        Assert.Equal(3, first.ProductId);
        Assert.Equal("Aniseed Syrup", first.ProductName);
        Assert.Equal(10m, first.UnitPrice);

        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        var second = FakeGeneratedProduct.Read(reader);

        Assert.Equal(4, second.ProductId);
        Assert.Equal("Chef Anton's Cajun Seasoning", second.ProductName);
        Assert.Equal(22m, second.UnitPrice);
    }

    private static IDataReader CreateReader(
        (string Name, Type Type) col1,
        (string Name, Type Type) col2,
        (string Name, Type Type) col3,
        object value1,
        object value2,
        object value3)
    {
        var table = CreateTable(col1, col2, col3, value1, value2, value3);
        return table.CreateDataReader();
    }

    private static DataTable CreateTable(
        (string Name, Type Type) col1,
        (string Name, Type Type) col2,
        (string Name, Type Type) col3,
        object value1,
        object value2,
        object value3)
    {
        var table = new DataTable();
        table.Columns.Add(col1.Name, col1.Type);
        table.Columns.Add(col2.Name, col2.Type);
        table.Columns.Add(col3.Name, col3.Type);
        table.Rows.Add(value1, value2, value3);
        return table;
    }

    private sealed class FakeGeneratedProduct
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal? UnitPrice { get; set; }

        public static FakeGeneratedProduct Read(IDataReader reader)
        {
            var ord = OrdinalMap.Resolve(reader);
            return new FakeGeneratedProduct
            {
                ProductId = reader.GetInt32(ord[0]),
                ProductName = reader.GetString(ord[1]),
                UnitPrice = reader.IsDBNull(ord[2]) ? null : reader.GetDecimal(ord[2])
            };
        }

        private static class OrdinalMap
        {
            private static readonly ConditionalWeakTable<IDataReader, CacheEntry> Cache = new();
            private static CacheEntry? _last;

            public static int[] Resolve(IDataReader reader)
            {
                var last = _last;
                if (last is not null && last.Matches(reader))
                    return last.Ordinals;

                if (Cache.TryGetValue(reader, out var cached) && cached.Matches(reader))
                {
                    _last = cached;
                    return cached.Ordinals;
                }

                var ords = new int[3];
                ords[0] = reader.GetOrdinal("product_id");
                ords[1] = reader.GetOrdinal("product_name");
                ords[2] = reader.GetOrdinal("unit_price");

                var entry = new CacheEntry(reader, ords);
                Cache.Remove(reader);
                Cache.Add(reader, entry);
                _last = entry;
                return ords;
            }

            private sealed class CacheEntry
            {
                private readonly IDataReader _reader;
                private readonly int _fieldCount;

                public CacheEntry(IDataReader reader, int[] ordinals)
                {
                    _reader = reader;
                    _fieldCount = reader.FieldCount;
                    Ordinals = ordinals;
                }

                public int[] Ordinals { get; }

                public bool Matches(IDataReader reader)
                {
                    if (!ReferenceEquals(reader, _reader))
                        return false;

                    if (reader.FieldCount != _fieldCount)
                        return false;

                    var ord0 = Ordinals[0];
                    if ((uint)ord0 >= (uint)reader.FieldCount)
                        return false;
                    if (!string.Equals(reader.GetName(ord0), "product_id", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var ord1 = Ordinals[1];
                    if ((uint)ord1 >= (uint)reader.FieldCount)
                        return false;
                    if (!string.Equals(reader.GetName(ord1), "product_name", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var ord2 = Ordinals[2];
                    if ((uint)ord2 >= (uint)reader.FieldCount)
                        return false;
                    if (!string.Equals(reader.GetName(ord2), "unit_price", StringComparison.OrdinalIgnoreCase))
                        return false;

                    return true;
                }
            }
        }
    }
}
