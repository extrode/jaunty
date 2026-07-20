using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// Regression tests for CachedDialectMetadata double-escaping. GetColumnNameFromProperty (and
/// GetColumnNameFromSelector) return a dialect-ESCAPED column name, since they're backed by
/// CachedDialectMetadata's pre-escaped cache. Several call sites used to escape that value a
/// second time via ISqlDialect.EscapeColumnName - a no-op for ordinary column names, but for a
/// column whose name collides with a SQL reserved keyword (escaped to e.g. "[order]" or
/// "\"order\""), the second escape call fed the already-bracketed/quoted text back into
/// SqlIdentifierValidator, which rejects anything that isn't a plain identifier and threw
/// ArgumentException. KeywordColumnEntity ("order" column, a SQLite keyword) exercises that
/// path. These tests only call ToSql() - the table is never created, so nothing here executes
/// against the database.
/// </summary>
public class KeywordColumnEscapingTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public KeywordColumnEscapingTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void OrderBy_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .OrderBy(e => e.Order)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void OrderByDescending_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .OrderByDescending(e => e.Order)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void ThenBy_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .OrderBy(e => e.Id)
            .ThenBy(e => e.Order)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void Set_ExpressionSelector_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .Set(e => e.Order, 5)
            .ToSql();

        Assert.Contains("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Set_AnonymousObject_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .Set(new { Order = 5 })
            .ToSql();

        Assert.Contains("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InsertValue_ExpressionSelector_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.Into<KeywordColumnEntity>()
            .Value(e => e.Id, 1)
            .Value(e => e.Order, 5)
            .ToSql();

        Assert.Contains("INSERT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InsertValues_AnonymousObject_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.Into<KeywordColumnEntity>()
            .Values(new { Id = 1, Order = 5 })
            .ToSql();

        Assert.Contains("INSERT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhereIn_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .WhereIn(e => e.Order, new[] { 1, 2, 3 })
            .ToSql();

        Assert.Contains("IN (", sql);
    }

    [Fact]
    public void WhereBetween_OnKeywordColumn_DoesNotThrow()
    {
        var sql = _fixture.Connection.From<KeywordColumnEntity>()
            .WhereBetween(e => e.Order, 1, 10)
            .ToSql();

        Assert.Contains("BETWEEN", sql);
    }
}
