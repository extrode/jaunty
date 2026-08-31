using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// Regression tests (round 22): QueryBuilder.GetUniqueParamName and InsertBuilder.Value(string,
/// object?) interpolated the raw caller-supplied column name into the SQL parameter placeholder
/// with no sanitization. The four built-in dialects all reject a column name containing a space
/// (or any other non-identifier character) via SqlIdentifierValidator before the placeholder is
/// ever built, so the malformed-placeholder scenario isn't reachable through them - but ISqlDialect
/// is a public extensibility point, and a custom dialect's EscapeColumnName is not required to
/// validate. TestDialect doesn't validate, so it exercises the sanitization directly.
/// </summary>
public class ParameterNameSanitizationTests
{
    private readonly NoValidationConnection _connection;

    public ParameterNameSanitizationTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(NoValidationConnection), new TestDialect());
        _connection = new NoValidationConnection();
    }

    [Fact]
    public void Where_StringColumnWithSpace_GeneratesWellFormedParameterPlaceholder()
    {
        var sql = _connection.From<Product>()
            .Where("Order Date", "2024-01-01")
            .ToSql();

        Assert.DoesNotContain("@Order Date", sql);
        Assert.Contains("@Order_Date", sql);
    }

    [Fact]
    public void Value_StringColumnWithSpace_GeneratesWellFormedParameterPlaceholder()
    {
        var sql = _connection.Into<Product>()
            .Value("Order Date", "2024-01-01")
            .ToSql();

        Assert.DoesNotContain("@Order Date", sql);
        Assert.Contains("@Order_Date", sql);
    }

    private sealed class NoValidationConnection : IDbConnection
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
