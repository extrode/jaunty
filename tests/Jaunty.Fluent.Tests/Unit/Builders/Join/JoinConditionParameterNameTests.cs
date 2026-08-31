using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// Regression tests (round 24): <c>On&lt;TValue&gt;(condition, value)</c> always bound its value
/// under the hardcoded name "value", so a join chain with two raw-value conditions added the
/// same parameter name twice to the shared <c>ParameterCollection</c> and threw
/// <c>ArgumentException</c> - and the API offered no way to name the parameter, so more than one
/// raw-value join condition per query was simply impossible. The
/// <c>On(condition, parameterName, value)</c> overload is the fix.
///
/// These run without the SQLite fixture (no query is ever executed) so they cover the 2-, 3-
/// and 4-table builders on any platform.
/// </summary>
public class JoinConditionParameterNameTests
{
    private readonly BuilderOnlyConnection _connection;

    public JoinConditionParameterNameTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(BuilderOnlyConnection), new TestDialect());
        _connection = new BuilderOnlyConnection();
    }

    [Fact]
    public void TwoTableJoin_OnNamedValue_BindsUnderTheGivenName()
    {
        var joined = _connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("products.category_id = categories.category_id AND categories.category_id = @categoryId", "categoryId", 7);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(joined);
        var parameters = builder.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "@categoryId" && Equals(p.Value, 7));
        Assert.DoesNotContain(parameters, p => p.Name == "@value");
    }

    [Fact]
    public void TwoTableJoin_OnTypedValue_StillBindsTheDefaultName()
    {
        var joined = _connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("products.category_id = categories.category_id AND categories.category_id = @value", 7);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(joined);
        var parameters = builder.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "@value" && Equals(p.Value, 7));
    }

    [Fact]
    public void ThreeTableJoin_TwoRawValueConditions_DistinctNames_BothBind()
    {
        var joined = _connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("products.category_id = categories.category_id AND categories.category_id = @categoryId", "categoryId", 1)
            .InnerJoin<Supplier>()
            .On<int>("products.supplier_id = suppliers.supplier_id AND suppliers.supplier_id = @supplierId", "supplierId", 2);

        var builder = Assert.IsType<JoinedQuery3Builder<Product, Category, Supplier>>(joined);
        var parameters = builder._parent.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "@categoryId" && Equals(p.Value, 1));
        Assert.Contains(parameters, p => p.Name == "@supplierId" && Equals(p.Value, 2));
    }

    [Fact]
    public void ThreeTableJoin_TwoRawValueConditions_DefaultName_ThrowsWithRemedy()
    {
        var ex = Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("products.category_id = categories.category_id AND categories.category_id = @value", 1)
            .InnerJoin<Supplier>()
            .On<int>("products.supplier_id = suppliers.supplier_id AND suppliers.supplier_id = @value", 2));

        Assert.Contains("@value", ex.Message);
        Assert.Contains("On(condition, parameterName, value)", ex.Message);
    }

    [Fact]
    public void ThreeTableJoin_TwoRawValueConditions_SameExplicitName_ThrowsWithRemedy()
    {
        var ex = Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("categories.category_id = @id", "id", 1)
            .InnerJoin<Supplier>()
            .On<int>("suppliers.supplier_id = @id", "id", 2));

        Assert.Contains("@id", ex.Message);
    }

    [Fact]
    public void FourTableJoin_ThreeRawValueConditions_DistinctNames_AllBind()
    {
        var joined = _connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On<int>("p.category_id = c.category_id AND c.category_id = @categoryId", "categoryId", 1)
            .InnerJoin<Supplier>("s")
            .On<int>("p.supplier_id = s.supplier_id AND s.supplier_id = @supplierId", "supplierId", 2)
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On<int>("o.order_id = @orderId", "orderId", 3);

        var builder = Assert.IsType<JoinedQuery4Builder<Product, Category, Supplier, Order>>(joined);
        var parameters = builder._parent._parent.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "@categoryId" && Equals(p.Value, 1));
        Assert.Contains(parameters, p => p.Name == "@supplierId" && Equals(p.Value, 2));
        Assert.Contains(parameters, p => p.Name == "@orderId" && Equals(p.Value, 3));
    }

    [Fact]
    public void FourTableJoin_TwoRawValueConditions_DefaultName_ThrowsWithRemedy()
    {
        var ex = Assert.Throws<ArgumentException>(() => _connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On<int>("p.category_id = c.category_id AND c.category_id = @value", 1)
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On<int>("o.order_id = @value", 2));

        Assert.Contains("@value", ex.Message);
        Assert.Contains("On(condition, parameterName, value)", ex.Message);
    }

    [Theory]
    [InlineData("orderId")]
    [InlineData("@orderId")]
    public void FourTableJoin_OnNamedValue_AcceptsBareOrPrefixedName(string parameterName)
    {
        var joined = _connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On<int>("o.order_id = @orderId", parameterName, 3);

        var builder = Assert.IsType<JoinedQuery4Builder<Product, Category, Supplier, Order>>(joined);
        var parameters = builder._parent._parent.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "@orderId" && Equals(p.Value, 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@")]
    public void OnNamedValue_RejectsEmptyOrPrefixOnlyName(string parameterName)
    {
        Assert.Throws<ArgumentException>(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("products.category_id = categories.category_id", parameterName, 1));
    }

    private sealed class BuilderOnlyConnection : IDbConnection
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
}
