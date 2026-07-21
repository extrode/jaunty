using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// Regression tests (round 17): several join-builder column/table-name resolvers used to fall
/// back to the raw, unescaped <c>metadata.TableName</c> when no alias was supplied
/// (<c>alias ?? metadata.TableName</c>) instead of routing through
/// <c>ISqlDialect.EscapeTableName</c> (the same bug already fixed in JoinExpressionVisitor/3/4).
/// This drops schema qualification and dialect escaping for every join/order-by/select whose
/// table isn't explicitly aliased. Uses TestDialect, which always brackets identifiers, so the
/// escaped-vs-raw distinction is visible regardless of whether the table name is a keyword.
/// </summary>
public class AliasEscapingFallbackTests
{
    private readonly BracketConnection _connection;

    public AliasEscapingFallbackTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(BracketConnection), new TestDialect());
        _connection = new BracketConnection();
    }

    [Fact]
    public void TwoTableJoin_OnAndSelect_NoAlias_UsesEscapedTableNames()
    {
        // Covers JoinClauseBuilder.GetColumnName (ON clause) and
        // JoinedQueryBuilder.GetPrefixedColumns (FROM select list).
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        Assert.Contains("[products].[product_id]", sql);
        Assert.Contains("[products].[category_id] = [categories].[category_id]", sql);
        Assert.DoesNotContain("products.[category_id]", sql);
        Assert.DoesNotContain("categories.[category_id]", sql);
    }

    [Fact]
    public void TwoTableJoin_GetPrefixedColumnsWithAlias_NoAlias_UsesEscapedTableName()
    {
        // Covers JoinedQueryBuilder.GetPrefixedColumnsWithAlias directly (internal API,
        // reached in practice by SelectBoth()/SelectBothAsync(), which require a live
        // connection to execute).
        var joined = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(joined);
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<Category>();
        string[] columns = builder.GetPrefixedColumnsWithAlias(metadata, tableAlias: null, columnPrefix: "j_");

        Assert.Contains(columns, c => c.StartsWith("[categories]."));
        Assert.DoesNotContain(columns, c => c.StartsWith("categories."));
    }

    [Fact]
    public void TwoTableJoin_OrderByJoined_NoAlias_UsesEscapedTableName()
    {
        // Covers JoinedQueryBuilderOrderBy.GetColumnName.
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByJoined(c => c.CategoryName)
            .ToSql();

        Assert.Contains("[categories].[category_name]", sql);
        Assert.DoesNotContain("categories.[category_name]", sql);
    }

    [Fact]
    public void ThreeTableJoin_OnThirdTable_NoAlias_UsesEscapedTableName()
    {
        // Covers JoinedQueryBuilder3.GetColumnName.
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .ToSql();

        Assert.Contains("[suppliers].[supplier_id]", sql);
        Assert.DoesNotContain("suppliers.[supplier_id]", sql);
    }

    [Fact]
    public void ThreeTableJoin_OrderByJoinedThirdTable_NoAlias_UsesEscapedTableName()
    {
        // Covers JoinedQueryBuilder3.GetColumnNameForOrderBy.
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderByJoined(s => s.CompanyName)
            .ToSql();

        Assert.Contains("[suppliers].[company_name]", sql);
        Assert.DoesNotContain("suppliers.[company_name]", sql);
    }

    [Fact]
    public void FourTableJoin_OnFourthTable_NoAlias_UsesEscapedTableName()
    {
        // Covers JoinClause4Builder.GetColumnName (the ON-clause resolver in
        // JoinedQueryBuilder4.cs).
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .OnFromThird(s => s.SupplierId, o => o.EmployeeId)
            .ToSql();

        Assert.Contains("[orders].[employee_id]", sql);
        Assert.DoesNotContain("orders.[employee_id]", sql);
    }

    [Fact]
    public void FourTableJoin_OrderByJoinedFourthTable_NoAlias_UsesEscapedTableName()
    {
        // Covers JoinedQueryBuilder4.GetColumnNameForOrderBy.
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .OnFromThird(s => s.SupplierId, o => o.EmployeeId)
            .OrderByJoined((Order o) => o.OrderDate)
            .ToSql();

        Assert.Contains("[orders].[order_date]", sql);
        Assert.DoesNotContain("orders.[order_date]", sql);
    }

    private sealed class BracketConnection : IDbConnection
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
