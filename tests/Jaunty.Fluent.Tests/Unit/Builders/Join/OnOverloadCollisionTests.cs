using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R34-022. <c>On(string leftColumn, string rightColumn)</c> and
/// <c>On&lt;TValue&gt;(string condition, TValue value)</c> collide for <c>TValue = string</c>, and
/// C# prefers the non-generic candidate - so a call written against the raw-condition-plus-parameter
/// overload silently bound to the column-pair one, concatenated the value into the ON clause
/// unescaped and left the <c>@value</c> placeholder unbound.
/// </summary>
public class OnOverloadCollisionTests
{
    private readonly OnBracketConnection _connection;

    public OnOverloadCollisionTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(OnBracketConnection), new TestDialect());
        _connection = new OnBracketConnection();
    }

    private sealed class OnBracketConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }

    [Fact]
    public void TwoTableJoin_OnWithAConditionAndAStringValue_IsRejectedRatherThanConcatenated()
    {
        var exception = Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id = categories.category_id AND categories.name = @value", "Widget"));

        Assert.Contains("On<string>(condition, value)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoTableJoin_OnWithAnInjectedFragment_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id", "categories.category_id; DROP TABLE products"));
    }

    [Fact]
    public void TwoTableJoin_OnWithAnExplicitTypeArgument_StillParameterises()
    {
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On<string>("products.category_id = categories.category_id AND categories.category_name = @value", "Widget")
            .ToSql();

        Assert.Contains("@value", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Widget", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoTableJoin_OnWithTwoColumns_StillJoins()
    {
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id", "categories.category_id")
            .ToSql();

        Assert.Contains("products.category_id = categories.category_id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThreeTableJoin_OnWithAConditionAndAStringValue_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>().On("products.category_id", "categories.category_id")
            .InnerJoin<Supplier>()
            .On("products.supplier_id = suppliers.supplier_id AND suppliers.city = @value", "Berlin"));
    }

    [Fact]
    public void ThreeTableJoin_OnWithTwoColumns_StillJoins()
    {
        string sql = _connection.From<Product>()
            .InnerJoin<Category>().On("products.category_id", "categories.category_id")
            .InnerJoin<Supplier>().On("products.supplier_id", "suppliers.supplier_id")
            .ToSql();

        Assert.Contains("products.supplier_id = suppliers.supplier_id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void FourTableJoin_OnWithAConditionAndAStringValue_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>().On("products.category_id", "categories.category_id")
            .InnerJoin<Supplier>().On("products.supplier_id", "suppliers.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("suppliers.supplier_id = orders.employee_id AND orders.ship_city = @value", "Berlin"));
    }

    [Fact]
    public void FourTableJoin_OnWithTwoColumns_StillJoins()
    {
        string sql = _connection.From<Product>()
            .InnerJoin<Category>().On("products.category_id", "categories.category_id")
            .InnerJoin<Supplier>().On("products.supplier_id", "suppliers.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>().On("suppliers.supplier_id", "orders.employee_id")
            .ToSql();

        Assert.Contains("suppliers.supplier_id = orders.employee_id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoTableJoin_OnWithAnEmptyColumn_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On("products.category_id", "   "));
    }
}
