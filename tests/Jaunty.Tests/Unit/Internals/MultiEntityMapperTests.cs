using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Read;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests MultiEntityMapper&lt;T1, T2&gt; Build/ApplyT1/ApplyT2 logic.
/// T1 has priority when columns match both types.
/// Uses real SQLite in-memory connections to produce IDataReader instances.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class MultiEntityMapperTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public MultiEntityMapperTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        SeedData();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SeedData()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE orders (order_id INTEGER PRIMARY KEY, customer_name TEXT NOT NULL, total REAL NOT NULL);
            CREATE TABLE order_details (detail_id INTEGER PRIMARY KEY, order_id INTEGER, product_name TEXT, quantity INTEGER);

            INSERT INTO orders (order_id, customer_name, total) VALUES (1, 'Alice', 99.95);
            INSERT INTO orders (order_id, customer_name, total) VALUES (2, 'Bob', 150.00);
            INSERT INTO order_details (detail_id, order_id, product_name, quantity) VALUES (1, 1, 'Widget', 5);
            INSERT INTO order_details (detail_id, order_id, product_name, quantity) VALUES (2, 1, 'Gadget', 3);";
        cmd.ExecuteNonQuery();
    }

    #region Test Entities

    public class OrderEntity
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public double Total { get; set; }
    }

    public class DetailEntity
    {
        public int DetailId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class EntityA
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class EntityB
    {
        public int Code { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    // Entities with overlapping column: both have 'SharedCol'
    public class OverlapT1
    {
        public int T1Id { get; set; }
        public string SharedCol { get; set; } = string.Empty;
    }

    public class OverlapT2
    {
        public int T2Id { get; set; }
        public string SharedCol { get; set; } = string.Empty;
    }

    public class NullableT1
    {
        public int Id { get; set; }
        public string? NullableName { get; set; }
    }

    public class NullableT2
    {
        public int Code { get; set; }
        public int? NullableValue { get; set; }
    }

    public class NonNullableT2
    {
        public int Code { get; set; }
        public int StrictValue { get; set; }
    }

    [Table("items")]
    public class ColumnMappedT1
    {
        [Column("col_id")]
        public int Id { get; set; }

        [Column("col_name")]
        public string Name { get; set; } = string.Empty;
    }

    #endregion

    #region Build - Basic Mapping

    [Fact]
    public void Build_DisjointColumns_MapsBothTypes()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT o.order_id AS OrderId, o.customer_name AS CustomerName, o.total AS Total,
                   d.detail_id AS DetailId, d.product_name AS ProductName, d.quantity AS Quantity
            FROM orders o
            JOIN order_details d ON o.order_id = d.order_id
            WHERE o.order_id = 1 AND d.detail_id = 1";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<OrderEntity, DetailEntity>.Build(reader);

        var order = new OrderEntity();
        var detail = new DetailEntity();
        mapper.ApplyT1(order, reader);
        mapper.ApplyT2(detail, reader);

        Assert.Equal(1, order.OrderId);
        Assert.Equal("Alice", order.CustomerName);
        Assert.Equal(99.95, order.Total, 2);
        Assert.Equal(1, detail.DetailId);
        Assert.Equal("Widget", detail.ProductName);
        Assert.Equal(5, detail.Quantity);
    }

    [Fact]
    public void Build_MultipleRows_ReusesSameMapper()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT o.order_id AS OrderId, o.customer_name AS CustomerName, o.total AS Total,
                   d.detail_id AS DetailId, d.product_name AS ProductName, d.quantity AS Quantity
            FROM orders o
            JOIN order_details d ON o.order_id = d.order_id
            WHERE o.order_id = 1
            ORDER BY d.detail_id";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<OrderEntity, DetailEntity>.Build(reader);

        var results = new List<(OrderEntity, DetailEntity)>();

        do
        {
            var order = new OrderEntity();
            var detail = new DetailEntity();
            mapper.ApplyT1(order, reader);
            mapper.ApplyT2(detail, reader);
            results.Add((order, detail));
        }
        while (reader.Read());

        Assert.Equal(2, results.Count);
        Assert.Equal("Widget", results[0].Item2.ProductName);
        Assert.Equal("Gadget", results[1].Item2.ProductName);
        // Both rows share the same order
        Assert.Equal(1, results[0].Item1.OrderId);
        Assert.Equal(1, results[1].Item1.OrderId);
    }

    #endregion

    #region Build - T1 Priority

    [Fact]
    public void Build_OverlappingColumn_T1HasPriority()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS T1Id, 'FromT1' AS SharedCol, 2 AS T2Id";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<OverlapT1, OverlapT2>.Build(reader);

        var t1 = new OverlapT1();
        var t2 = new OverlapT2();
        mapper.ApplyT1(t1, reader);
        mapper.ApplyT2(t2, reader);

        // SharedCol should map to T1, not T2
        Assert.Equal(1, t1.T1Id);
        Assert.Equal("FromT1", t1.SharedCol);
        Assert.Equal(2, t2.T2Id);
        Assert.Equal(string.Empty, t2.SharedCol); // T2 should NOT get SharedCol
    }

    #endregion

    #region Build - Unmatched Columns

    [Fact]
    public void Build_UnmatchedColumn_Ignored()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS Id, 'test' AS Name, 999 AS UnknownColumn, 2 AS Code, 'lbl' AS Label";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<EntityA, EntityB>.Build(reader);

        var a = new EntityA();
        var b = new EntityB();
        mapper.ApplyT1(a, reader);
        mapper.ApplyT2(b, reader);

        Assert.Equal(1, a.Id);
        Assert.Equal("test", a.Name);
        Assert.Equal(2, b.Code);
        Assert.Equal("lbl", b.Label);
    }

    #endregion

    #region Build - Column Attribute Mapping

    [Fact]
    public void Build_ColumnAttribute_MapsViaColumnName()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS col_id, 'mapped' AS col_name, 2 AS Code, 'lbl' AS Label";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<ColumnMappedT1, EntityB>.Build(reader);

        var t1 = new ColumnMappedT1();
        var t2 = new EntityB();
        mapper.ApplyT1(t1, reader);
        mapper.ApplyT2(t2, reader);

        Assert.Equal(1, t1.Id);
        Assert.Equal("mapped", t1.Name);
        Assert.Equal(2, t2.Code);
        Assert.Equal("lbl", t2.Label);
    }

    [Fact]
    public void Build_ColumnAttribute_AlsoMapsViaPropertyName()
    {
        using var cmd = _connection.CreateCommand();
        // Use property names (Id, Name) instead of column names (col_id, col_name)
        cmd.CommandText = "SELECT 1 AS Id, 'byProp' AS Name, 2 AS Code, 'lbl' AS Label";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<ColumnMappedT1, EntityB>.Build(reader);

        var t1 = new ColumnMappedT1();
        var t2 = new EntityB();
        mapper.ApplyT1(t1, reader);
        mapper.ApplyT2(t2, reader);

        Assert.Equal(1, t1.Id);
        Assert.Equal("byProp", t1.Name);
    }

    #endregion

    #region Build - Case Insensitive

    [Fact]
    public void Build_CaseInsensitive_Matching()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS ORDERID, 'Alice' AS CUSTOMERNAME, 50.0 AS TOTAL, 1 AS DETAILID, 'Widget' AS PRODUCTNAME, 3 AS QUANTITY";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<OrderEntity, DetailEntity>.Build(reader);

        var order = new OrderEntity();
        var detail = new DetailEntity();
        mapper.ApplyT1(order, reader);
        mapper.ApplyT2(detail, reader);

        Assert.Equal(1, order.OrderId);
        Assert.Equal("Alice", order.CustomerName);
        Assert.Equal(1, detail.DetailId);
        Assert.Equal("Widget", detail.ProductName);
    }

    #endregion

    #region ApplyT1 / ApplyT2 - NULL Handling

    [Fact]
    public void Apply_NullableProperty_NullValue_SetsNull()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS Id, NULL AS NullableName, 2 AS Code, NULL AS NullableValue";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<NullableT1, NullableT2>.Build(reader);

        var t1 = new NullableT1();
        var t2 = new NullableT2();
        mapper.ApplyT1(t1, reader);
        mapper.ApplyT2(t2, reader);

        Assert.Equal(1, t1.Id);
        Assert.Null(t1.NullableName);
        Assert.Equal(2, t2.Code);
        Assert.Null(t2.NullableValue);
    }

    [Fact]
    public void Apply_NullableProperty_NonNullValue_SetsValue()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS Id, 'hello' AS NullableName, 2 AS Code, 42 AS NullableValue";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<NullableT1, NullableT2>.Build(reader);

        var t1 = new NullableT1();
        var t2 = new NullableT2();
        mapper.ApplyT1(t1, reader);
        mapper.ApplyT2(t2, reader);

        Assert.Equal("hello", t1.NullableName);
        Assert.Equal(42, t2.NullableValue);
    }

    [Fact]
    public void ApplyT2_NonNullableValueType_NullThrows()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS Id, 'hello' AS NullableName, 2 AS Code, NULL AS StrictValue";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<NullableT1, NonNullableT2>.Build(reader);

        var t1 = new NullableT1();
        var t2 = new NonNullableT2();
        mapper.ApplyT1(t1, reader);

        var ex = Assert.Throws<InvalidOperationException>(() => mapper.ApplyT2(t2, reader));
        Assert.Contains("Cannot assign NULL to non-nullable property", ex.Message);
        Assert.Contains("StrictValue", ex.Message);
    }

    #endregion

    #region Empty Results

    [Fact]
    public void Build_NoMatchingColumns_ReturnsMapperWithEmptySetters()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS UnknownA, 2 AS UnknownB";
        using var reader = cmd.ExecuteReader();
        reader.Read();

        var mapper = global::Jaunty.Internals.Read.MultiEntityMapper<EntityA, EntityB>.Build(reader);

        // Should not throw — just no properties mapped
        var a = new EntityA();
        var b = new EntityB();
        mapper.ApplyT1(a, reader);
        mapper.ApplyT2(b, reader);

        // Default values remain
        Assert.Equal(0, a.Id);
        Assert.Equal(string.Empty, a.Name);
        Assert.Equal(0, b.Code);
        Assert.Equal(string.Empty, b.Label);
    }

    #endregion

    #region Build - Arity-3 Schema Key Collision (Fix 1 regression)

    // Entities scoped to this scenario so their generic MultiEntityMapper<...>
    // static cache can't be warmed by any other test in the suite.
    public class SchemaKeyColA
    {
        public int? A { get; set; }
    }

    public class SchemaKeyColBC
    {
        public int? BC { get; set; }
    }

    public class SchemaKeyColC
    {
        public int? C { get; set; }
    }

    [Fact]
    public void Build_Arity3_DifferentColumnSets_SameFieldCount_DoNotCollide()
    {
        // Regression test for the BuildSchemaKey cache-key collision bug: with a
        // delimiter-less concatenation, field count 2 with column names ["A","BC"]
        // and ["AB","C"] both produced the same cache key ("2ABC"), so the second
        // query would silently reuse the first query's cached mapper.
        //
        // The outer cache under test lives in Jaunty.Internals.Read.MultiEntityMapperN.
        // The inner Jaunty.Extensions.Reflection resolver has its own, separately
        // scoped (currently unfixed) cache with the same structural bug, which would
        // otherwise also collide on this exact scenario and mask what's being tested.
        // To isolate the outer cache, this test installs a resolver stub that rebuilds
        // appliers fresh on every call (no caching of its own). Any stale/wrong
        // mapping observed here can therefore only come from the outer BuildSchemaKey
        // cache under test.
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? originalResolver =
            JauntyConfig.ReflectionMultiMapperResolverN;

        try
        {
            JauntyConfig.ReflectionMultiMapperResolverN = (types, reader) =>
            {
                var appliers = new Action<object, IDataRecord>[types.Length];
                for (int i = 0; i < types.Length; i++)
                    appliers[i] = BuildLiveApplier(types[i], reader);
                return appliers;
            };

            using var cmdA = _connection.CreateCommand();
            cmdA.CommandText = "SELECT 1 AS A, 2 AS BC";
            using var readerA = cmdA.ExecuteReader();
            readerA.Read();

            var mapperA = global::Jaunty.Internals.Read.MultiEntityMapper<SchemaKeyColA, SchemaKeyColBC, SchemaKeyColC>.Build(readerA);
            var a1 = new SchemaKeyColA();
            var bc1 = new SchemaKeyColBC();
            var c1 = new SchemaKeyColC();
            mapperA.ApplyT1(a1, readerA);
            mapperA.ApplyT2(bc1, readerA);
            mapperA.ApplyT3(c1, readerA);

            Assert.Equal(1, a1.A);
            Assert.Equal(2, bc1.BC);
            Assert.Null(c1.C);

            using var cmdB = _connection.CreateCommand();
            cmdB.CommandText = "SELECT 100 AS AB, 200 AS C";
            using var readerB = cmdB.ExecuteReader();
            readerB.Read();

            var mapperB = global::Jaunty.Internals.Read.MultiEntityMapper<SchemaKeyColA, SchemaKeyColBC, SchemaKeyColC>.Build(readerB);
            var a2 = new SchemaKeyColA();
            var bc2 = new SchemaKeyColBC();
            var c2 = new SchemaKeyColC();
            mapperB.ApplyT1(a2, readerB);
            mapperB.ApplyT2(bc2, readerB);
            mapperB.ApplyT3(c2, readerB);

            // With the collision bug, mapperB would be the SAME cached instance as
            // mapperA (schema key "2ABC" for both), so ApplyT1 would blindly bind
            // ordinal 0 to A (reading readerB's "AB" value = 100) instead of correctly
            // recognizing there is no "A" column in readerB.
            Assert.Null(a2.A);
            Assert.Null(bc2.BC);
            Assert.Equal(200, c2.C);
        }
        finally
        {
            JauntyConfig.ReflectionMultiMapperResolverN = originalResolver;
        }
    }

    private static Action<object, IDataRecord> BuildLiveApplier(Type type, IDataReader reader)
    {
        var matches = new List<(System.Reflection.PropertyInfo Property, int Ordinal)>();
        foreach (var property in type.GetProperties())
        {
            int ordinal = FindOrdinal(reader, property.Name);
            if (ordinal >= 0)
                matches.Add((property, ordinal));
        }

        return (target, record) =>
        {
            foreach ((System.Reflection.PropertyInfo property, int ordinal) in matches)
            {
                object value = record.GetValue(ordinal);
                if (value is DBNull)
                {
                    property.SetValue(target, null);
                    continue;
                }

                Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object converted = targetType.IsInstanceOfType(value) ? value : Convert.ChangeType(value, targetType);
                property.SetValue(target, converted);
            }
        };
    }

    private static int FindOrdinal(IDataReader reader, string name)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    #endregion
}