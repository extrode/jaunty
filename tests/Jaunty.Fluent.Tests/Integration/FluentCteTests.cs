using System.Data;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Fluent CTE (Common Table Expression) support.
/// </summary>
public class FluentCteTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentCteTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    #region Basic CTE Tests

    [Fact]
    public void Cte_SimpleQuery_ReturnsResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("ExpensiveProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .Select();

        // Assert
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 50));
    }

    [Fact]
    public void Cte_ToSql_GeneratesCorrectSql()
    {
        // Arrange & Act
        var sql = _fixture.Connection.Cte<Product>("ExpensiveProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .ToSql();

        // Assert
        Assert.StartsWith("WITH ExpensiveProducts AS (", sql);
        Assert.Contains("SELECT * FROM", sql);
        Assert.Contains("products", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains(") SELECT * FROM ExpensiveProducts", sql);
    }

    [Fact]
    public void Cte_WithAdditionalWhere_FiltersResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(q => q.Where(p => p.UnitPrice > 20))
            .Where(p => p.CategoryId == 1)
            .Select();

        // Assert
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 20 && p.CategoryId == 1));
    }

    [Fact]
    public void Cte_WithAdditionalWhere_ToSql_GeneratesCorrectSql()
    {
        // Act
        var sql = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(q => q.Where(p => p.UnitPrice > 20))
            .Where(p => p.CategoryId == 1)
            .ToSql();

        // Assert
        Assert.Contains("WITH FilteredProducts AS (", sql);
        Assert.Contains(") SELECT * FROM FilteredProducts WHERE", sql);
        Assert.Contains("category_id", sql);
    }

    #endregion

    #region CTE with Ordering and Pagination

    [Fact]
    public void Cte_WithOrderBy_ReturnsOrderedResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("SortedProducts")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .OrderBy(p => p.ProductName)
            .Select();

        // Assert
        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Cte_WithOrderByDescending_ReturnsDescendingResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("SortedProducts")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        // Assert
        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(products[i - 1].UnitPrice >= products[i].UnitPrice);
        }
    }

    [Fact]
    public void Cte_WithTake_LimitsResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("LimitedProducts")
            .As(q => q.Where(p => p.UnitPrice > 5))
            .Take(5)
            .Select();

        // Assert
        Assert.True(products.Count <= 5);
    }

    [Fact]
    public void Cte_WithSkipAndTake_PaginatesResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("PaginatedProducts")
            .As(q => q.Where(p => p.UnitPrice > 5))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Take(3)
            .Select();

        // Assert
        Assert.True(products.Count <= 3);
    }

    #endregion

    #region CTE with And/Or

    [Fact]
    public void Cte_WithAndCondition_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("MultiFilter")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .Where(p => p.CategoryId == 1)
            .And(p => p.UnitsInStock > 0)
            .Select();

        // Assert
        Assert.All(products, p =>
            Assert.True(p.UnitPrice > 10 && p.CategoryId == 1 && p.UnitsInStock > 0));
    }

    [Fact]
    public void Cte_WithOrCondition_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("OrFilter")
            .As(q => q.Where(p => p.UnitPrice > 100))
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        // Assert
        Assert.All(products, p =>
            Assert.True(p.UnitPrice > 100 && (p.CategoryId == 1 || p.CategoryId == 2)));
    }

    [Fact]
    public void Cte_ChainedWithOrAnd_AppliesCorrectPrecedence()
    {
        // Chained: Where(...).Or(...).And(...) - regression test for AND/OR precedence
        // (round 10): must evaluate as (CategoryId == 1 OR CategoryId == 2) AND UnitPrice > 10,
        // not CategoryId == 1 OR (CategoryId == 2 AND UnitPrice > 10) per SQL's native
        // AND-before-OR precedence. Seed data has a CategoryId == 1 row with UnitPrice == 5,
        // which the buggy unparenthesized form would incorrectly let through.
        var products = _fixture.Connection.Cte<Product>("ChainedOrAnd")
            .As(q => q.Where(p => p.UnitsInStock >= 0))
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .And(p => p.UnitPrice > 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True((p.CategoryId == 1 || p.CategoryId == 2) && p.UnitPrice > 10));
    }

    [Fact]
    public void Cte_SameColumnFilteredInsideAndOutside_DoesNotCollideParameterNames()
    {
        // Regression test (round 10): the inner .As(q => q.Where(...)) query and the CTE's
        // own outer .Where()/.And() both filter UnitPrice, so both independently generate a
        // "first use" parameter named "@unit_price" for the column. Before the fix, CteBuilder
        // copied the inner query's parameters into its own ParameterCollection without
        // reserving that name, so the outer WhereExpressionVisitor (seeded with an empty
        // count dictionary) regenerated the same name and threw
        // ArgumentException: "A parameter named '@unit_price' has already been added."
        var products = _fixture.Connection.Cte<Product>("SameColumnFilter")
            .As(q => q.Where(p => p.UnitPrice > 0))
            .Where(p => p.CategoryId == 1)
            .And(p => p.UnitPrice > 10)
            .Select();

        Assert.All(products, p => Assert.True(p.CategoryId == 1 && p.UnitPrice > 10));
    }

    [Fact]
    public void Cte_AsIWhereClause_SameColumnFilteredInsideAndOutside_DoesNotCollideParameterNames()
    {
        // Same collision as above, but through the As(IWhereClause<T>) overload.
        var products = _fixture.Connection.Cte<Product>("SameColumnFilterIWhere")
            .As(_fixture.Connection.From<Product>().Where(p => p.UnitPrice > 0))
            .Where(p => p.UnitPrice > 10)
            .Select();

        Assert.All(products, p => Assert.True(p.UnitPrice > 10));
    }

    #endregion

    #region CTE with SelectFirst/SelectFirstOrDefault

    [Fact]
    public void Cte_SelectFirst_ReturnsSingleResult()
    {
        // Arrange & Act
        var product = _fixture.Connection.Cte<Product>("FirstProduct")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirst();

        // Assert
        Assert.NotNull(product);
        Assert.True(product.UnitPrice > 50);
    }

    [Fact]
    public void Cte_SelectFirstOrDefault_WithNoResults_ReturnsNull()
    {
        // Arrange & Act
        var product = _fixture.Connection.Cte<Product>("NoProducts")
            .As(q => q.Where(p => p.UnitPrice > 999999))
            .SelectFirstOrDefault();

        // Assert
        Assert.Null(product);
    }

    #endregion

    #region CTE Async Tests

    [Fact]
    public async Task Cte_SelectAsync_ReturnsResults()
    {
        // Arrange & Act
        var products = await _fixture.Connection.Cte<Product>("AsyncProducts")
            .As(q => q.Where(p => p.UnitPrice > 30))
            .SelectAsync();

        // Assert
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 30));
    }

    [Fact]
    public async Task Cte_SelectFirstAsync_ReturnsTheSameRowAsItsSyncTwin()
    {
        var sync = _fixture.Connection.Cte<Product>("FirstAsyncProduct")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirst();

        var async = await _fixture.Connection.Cte<Product>("FirstAsyncProduct")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirstAsync();

        Assert.NotNull(async);
        Assert.Equal(sync.ProductId, async.ProductId);
    }

    [Fact]
    public async Task Cte_SelectFirstAsync_WithOptions_ReturnsTheFirstRow()
    {
        var product = await _fixture.Connection.Cte<Product>("FirstAsyncOptions")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirstAsync(new CommandOptions(commandTimeout: 30));

        Assert.NotNull(product);
        Assert.True(product.UnitPrice > 50);
    }

    [Fact]
    public async Task Cte_SelectFirstOrDefaultAsync_WithNoResults_ReturnsNull()
    {
        var product = await _fixture.Connection.Cte<Product>("NoAsyncProducts")
            .As(q => q.Where(p => p.UnitPrice > 999999))
            .SelectFirstOrDefaultAsync();

        Assert.Null(product);
    }

    [Fact]
    public async Task Cte_SelectFirstOrDefaultAsync_WithResults_ReturnsTheFirstRow()
    {
        var product = await _fixture.Connection.Cte<Product>("SomeAsyncProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirstOrDefaultAsync();

        Assert.NotNull(product);
        Assert.True(product.UnitPrice > 50);
    }

    [Fact]
    public async Task Cte_SelectFirstOrDefaultAsync_WithOptions_ReturnsTheFirstRow()
    {
        var product = await _fixture.Connection.Cte<Product>("AsyncOptionsProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirstOrDefaultAsync(new CommandOptions(commandTimeout: 30));

        Assert.NotNull(product);
        Assert.True(product.UnitPrice > 50);
    }

    /// <summary>
    /// The async terminals restore <c>_takeCount</c> the way the sync ones do (AUD-R34's reuse
    /// case), so a later Select on the same held builder is not silently capped at one row.
    /// </summary>
    [Fact]
    public async Task Cte_SelectFirstAsync_DoesNotCapALaterSelectOnTheSameBuilder()
    {
        var builder = _fixture.Connection.Cte<Product>("ReusedAsyncCte")
            .As(q => q.Where(p => p.UnitPrice > 0));

        await builder.SelectFirstAsync();
        var all = builder.Select();

        Assert.True(all.Count > 1);
    }

    #endregion

    #region CTE with Column-based Where

    [Fact]
    public void Cte_WhereWithColumnName_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("ColumnFilter")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .Where("category_id", 1)
            .Select();

        // Assert
        Assert.All(products, p => Assert.True(p.UnitPrice > 10 && p.CategoryId == 1));
    }

    #endregion

    #region CTE with As(IWhereClause) Overload

    [Fact]
    public void Cte_AsIWhereClause_ReturnsResults()
    {
        // Arrange & Act: Use As with IWhereClause overload
        var products = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(_fixture.Connection.From<Product>().Where(p => p.UnitPrice > 30))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 30));
    }

    [Fact]
    public void Cte_AsIWhereClause_ToSql_GeneratesCorrectSql()
    {
        // Act
        var sql = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(_fixture.Connection.From<Product>().Where(p => p.UnitPrice > 30))
            .ToSql();

        // Assert
        Assert.Contains("WITH FilteredProducts AS (", sql);
        Assert.Contains(") SELECT * FROM FilteredProducts", sql);
    }

    #endregion

    #region CTE Where(string, object) dialect parameter prefix (AUD-R11)

    [Fact]
    public void Cte_WhereStringOverload_UsesDialectParameterPrefix_NotHardcodedAt()
    {
        // Regression test: CteBuilder.Where(string, object) used to hardcode the bound parameter
        // name as "@cte_p{n}" regardless of dialect. A dialect with a non-"@" parameter prefix
        // (e.g. DuckDB's "$") would end up with a bound parameter name that didn't match the
        // dialect's own placeholder syntax. Mirrors JoinClauseBuilderParameterPrefixTests' pattern.
        SqlDialectFactory.RegisterDialect(nameof(DollarPrefixConnection), new DollarPrefixDialect());
        var connection = new DollarPrefixConnection();

        var sql = connection.Cte<Product>("PriceFiltered")
            .As(q => q.Where(p => p.UnitPrice > 0))
            .Where("UnitPrice", 10m)
            .ToSql();

        Assert.Contains("$cte_p", sql);
        Assert.DoesNotContain("@cte_p", sql);
    }

    private sealed class DollarPrefixConnection : IDbConnection
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

    private sealed class DollarPrefixDialect : ISqlDialect
    {
        private readonly SQLiteDialect _inner = new();

        public string ParameterPrefix => "$";

        public bool SupportsForeignKeyToggle => _inner.SupportsForeignKeyToggle;
        public bool RequiresAutocommitForForeignKeyToggle => _inner.RequiresAutocommitForForeignKeyToggle;
        public bool SupportsUpsert => _inner.SupportsUpsert;
        public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
        public bool SupportsNativeBulkCopy => _inner.SupportsNativeBulkCopy;
        public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;
        public IBulkCopyProvider? CreateBulkCopyProvider() => _inner.CreateBulkCopyProvider();
        public string? GetDisableForeignKeyChecksSql() => _inner.GetDisableForeignKeyChecksSql();
        public string? GetEnableForeignKeyChecksSql() => _inner.GetEnableForeignKeyChecksSql();

        public string GetDefaultSchema() => _inner.GetDefaultSchema();
        public string EscapeTableName(string? schemaName, string tableName) => _inner.EscapeTableName(schemaName, tableName);
        public string EscapeColumnName(string columnName) => _inner.EscapeColumnName(columnName);
        public string EscapeStringLiteral(string value) => _inner.EscapeStringLiteral(value);
        public string GetLastInsertIdSql(params string[] columnNames) => _inner.GetLastInsertIdSql(columnNames);
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => _inner.GetPagingSql(baseSql, offset, fetchNext);
        public bool IsKeyword(string identifier) => _inner.IsKeyword(identifier);
        public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseSensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseInsensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) => _inner.GenerateCaseInsensitiveEquals(columnName, parameterName);
        public string FormatContainsPattern(string value) => _inner.FormatContainsPattern(value);
        public string FormatStartsWithPattern(string value) => _inner.FormatStartsWithPattern(value);
        public string FormatEndsWithPattern(string value) => _inner.FormatEndsWithPattern(value);
        public string FormatBooleanLiteral(bool value) => _inner.FormatBooleanLiteral(value);
        public string GenerateCoalesce(params string[] expressions) => _inner.GenerateCoalesce(expressions);
        public string GenerateIsNull(string expression, string defaultExpression) => _inner.GenerateIsNull(expression, defaultExpression);
        public string GenerateNullIf(string expression, string compareExpression) => _inner.GenerateNullIf(expression, compareExpression);
        public string GenerateLength(string expression) => _inner.GenerateLength(expression);
        public string GenerateUpper(string expression) => _inner.GenerateUpper(expression);
        public string GenerateLower(string expression) => _inner.GenerateLower(expression);
        public string GenerateTrim(string expression) => _inner.GenerateTrim(expression);
        public string GenerateSubstring(string expression, string start, string length) => _inner.GenerateSubstring(expression, start, length);
        public string GenerateYear(string expression) => _inner.GenerateYear(expression);
        public string GenerateMonth(string expression) => _inner.GenerateMonth(expression);
        public string GenerateDay(string expression) => _inner.GenerateDay(expression);
        public string GenerateUpsertSql(string tableName, string[] insertColumns, string[] insertParams, string[] updateColumns, string[] updateParams, string[] keyColumns, string[] keyParams) => _inner.GenerateUpsertSql(tableName, insertColumns, insertParams, updateColumns, updateParams, keyColumns, keyParams);
        public string GenerateRowNumber() => _inner.GenerateRowNumber();
        public string GenerateRank() => _inner.GenerateRank();
        public string GenerateDenseRank() => _inner.GenerateDenseRank();
        public string GenerateNTile(int buckets) => _inner.GenerateNTile(buckets);
        public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy) => _inner.GenerateOverClause(partitionBy, orderBy);
        public string GenerateWindowAggregate(string function, string? expression) => _inner.GenerateWindowAggregate(function, expression);
    }

    #endregion
}