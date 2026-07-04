using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Internals.Read;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests MultiEntityMapper&lt;T1, T2&gt; Build/ApplyT1/ApplyT2 logic.
/// T1 has priority when columns match both types.
/// Uses real SQLite in-memory connections to produce IDataReader instances.
/// </summary>
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
}