using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R35-014. The string-based <c>Where(string column, object value)</c> on the two-, three- and
/// four-table joined builders named its placeholder straight from the column text with no
/// uniquifier and no sanitisation, so a range filter on one column threw
/// <c>ArgumentException: A parameter named '@o_order_date' has already been added</c> and two
/// distinct columns could collapse onto one name. The single-table twin had both protections.
/// </summary>
public class JoinedStringWhereParameterNamingTests
{
    private readonly WhereNamingConnection _connection;

    public JoinedStringWhereParameterNamingTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(WhereNamingConnection), new TestDialect());
        _connection = new WhereNamingConnection();
    }

    private IJoinedQuery<Product, Category> TwoTable() =>
        _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

    private IJoinedQuery3<Product, Category, Supplier> ThreeTable() =>
        _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId);

    private IJoinedQuery4<Product, Category, Supplier, Order> FourTable() =>
        ThreeTable()
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0");

    // ------------------------------------------------------------------
    // The reported shape: one column, two filters
    // ------------------------------------------------------------------

    [Fact]
    public void TwoTableJoin_TheSameColumnFilteredTwice_BindsTwoDistinctParameters()
    {
        var query = TwoTable().Where("p.unit_price", 10m).Where("p.unit_price", 20m);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(query);
        IReadOnlyList<(string Name, object? Value)> parameters = builder.GetParameters().GetAll();

        Assert.Equal(2, parameters.Count);
        Assert.NotEqual(parameters[0].Name, parameters[1].Name);
        Assert.Equal(10m, parameters[0].Value);
        Assert.Equal(20m, parameters[1].Value);
    }

    [Fact]
    public void TwoTableJoin_BothPlaceholdersReachTheSql()
    {
        var query = TwoTable().Where("p.unit_price", 10m).Where("p.unit_price", 20m);

        string sql = query.ToSql();
        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(query);

        foreach ((string name, _) in builder.GetParameters().GetAll())
            Assert.Contains(name, sql);
    }

    [Fact]
    public void ThreeTableJoin_TheSameColumnFilteredTwice_BindsTwoDistinctParameters()
    {
        var query = ThreeTable().Where("p.unit_price", 10m).Where("p.unit_price", 20m);

        string sql = query.ToSql();

        Assert.Equal(2, CountOccurrences(sql, "[unit_price] = @"));
        Assert.DoesNotContain("@p_unit_price ", sql + " ");
    }

    [Fact]
    public void FourTableJoin_TheSameColumnFilteredTwice_BindsTwoDistinctParameters()
    {
        var query = FourTable().Where("p.unit_price", 10m).Where("p.unit_price", 20m);

        string sql = query.ToSql();

        Assert.Equal(2, CountOccurrences(sql, "[unit_price] = @"));
    }

    // ------------------------------------------------------------------
    // The second collision: "." rewritten to "_" collapsed distinct columns
    // ------------------------------------------------------------------

    [Fact]
    public void AQualifiedColumnAndAnUnderscoredOneDoNotCollapseOntoOneName()
    {
        var query = TwoTable().Where("p.category_id", 1).Where("p_category_id", 2);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(query);
        IReadOnlyList<(string Name, object? Value)> parameters = builder.GetParameters().GetAll();

        Assert.Equal(2, parameters.Count);
        Assert.NotEqual(parameters[0].Name, parameters[1].Name);
    }

    // ------------------------------------------------------------------
    // Sanitisation, which the joined overloads also lacked
    // ------------------------------------------------------------------

    [Fact]
    public void AColumnWhoseNameNeedsSanitisingNeverReachesTheSqlAsASpacedPlaceholder()
    {
        // The alias segment is validated, so a dotted name with a space throws before binding;
        // an unqualified one binds, and its placeholder must still be a legal identifier.
        var query = TwoTable().Where("unit price", 10m);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(query);
        (string name, _) = builder.GetParameters().GetAll()[0];

        Assert.DoesNotContain(" ", name);
        Assert.StartsWith("@", name);
    }

    // ------------------------------------------------------------------
    // Controls
    // ------------------------------------------------------------------

    [Fact]
    public void ASingleFilterStillBindsOneParameterCarryingItsValue()
    {
        var query = TwoTable().Where("p.unit_price", 10m);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(query);
        IReadOnlyList<(string Name, object? Value)> parameters = builder.GetParameters().GetAll();

        Assert.Single(parameters);
        Assert.Equal(10m, parameters[0].Value);
        Assert.Contains("unit_price", parameters[0].Name);
    }

    [Fact]
    public void MixingTheStringOverloadWithAnExpressionOverloadStillBinds()
    {
        var query = TwoTable()
            .Where("p.unit_price", 10m)
            .And((p, c) => p.Discontinued == false);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(query);
        var names = new HashSet<string>();

        foreach ((string name, _) in builder.GetParameters().GetAll())
            Assert.True(names.Add(name), $"duplicate parameter name '{name}'");

        Assert.True(names.Count >= 2);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }
        return count;
    }

    private sealed class WhereNamingConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }
}
