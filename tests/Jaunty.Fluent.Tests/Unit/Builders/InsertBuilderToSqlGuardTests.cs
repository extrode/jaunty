using System.Data;

using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R30: Insert()/InsertAsync() fail fast when no values were specified, but ToSql() built
/// "INSERT INTO x () VALUES ()" - invalid SQL on every engine - without complaint. Reachable via
/// Values(new { }) or an entity whose every column is identity/computed.
/// </summary>
public class InsertBuilderToSqlGuardTests
{
    [Fact]
    public void ToSql_NoValuesSpecified_ThrowsLikeInsertDoes()
    {
        using var connection = new NpgsqlConnection();

        var ex = Assert.Throws<InvalidOperationException>(() => connection.Into<Product>().Values(new { }).ToSql());
        Assert.Contains("at least one value", ex.Message);
    }

    [Fact]
    public void ToSql_WithAValue_StillBuildsTheStatement()
    {
        using var connection = new NpgsqlConnection();

        var sql = connection.Into<Product>().Value(p => p.ProductName, "Widget").ToSql();

        Assert.Contains("INSERT INTO", sql);
        Assert.Contains("ProductName", sql);
    }

    private sealed class NpgsqlConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
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
