using System.Data;

using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Fluent.Tests.Entities;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// AUD-R38-106 and 107 - the two lexical rules <c>ParameterRenamer.Rename</c> takes from the dialect:
/// whether <c>[...]</c> is a quoted identifier, and whether a backslash escapes a quote.
/// </summary>
public class ParameterRenamerDialectTests
{
    public ParameterRenamerDialectTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(PostgresRenamerConnection), new PostgreSqlDialect());
    }

    private static ParameterCollection Params(params (string Name, object? Value)[] entries)
    {
        var collection = new ParameterCollection();
        foreach ((string name, object? value) in entries)
            collection.Add(name, value);

        return collection;
    }

    [Fact]
    public void Postgres_ArraySubscript_IsRenamed()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE tags[@i] = 'x' AND id = ANY(ARRAY[@a, @b])",
            Params(("@i", 1), ("@a", 2), ("@b", 3)),
            "sq0",
            new PostgreSqlDialect());

        Assert.Equal("SELECT * FROM t WHERE tags[@sq0_i] = 'x' AND id = ANY(ARRAY[@sq0_a, @sq0_b])", sql);
    }

    [Theory]
    [InlineData("sqlserver")]
    [InlineData("sqlite")]
    public void BracketDialects_BracketedIdentifier_IsNotRenamed(string name)
    {
        ISqlDialect dialect = name == "sqlserver" ? new SqlServerDialect() : new SQLiteDialect();

        var (sql, _) = ParameterRenamer.Rename(
            "SELECT [@p0] FROM t WHERE id = @p0",
            Params(("@p0", 1)),
            "sq0",
            dialect);

        Assert.Equal("SELECT [@p0] FROM t WHERE id = @sq0_p0", sql);
    }

    [Fact]
    public void MySql_BackslashEscapedQuote_DoesNotEndTheLiteral()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE name = 'O\\'Brien @p0' AND id = @p0",
            Params(("@p0", 1)),
            "sq0",
            new MySqlDialect());

        Assert.Equal("SELECT * FROM t WHERE name = 'O\\'Brien @p0' AND id = @sq0_p0", sql);
    }

    [Fact]
    public void MySql_EscapedBackslashBeforeTheClosingQuote_EndsTheLiteral()
    {
        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE path = 'C:\\\\' AND id = @p0",
            Params(("@p0", 1)),
            "sq0",
            new MySqlDialect());

        Assert.Equal("SELECT * FROM t WHERE path = 'C:\\\\' AND id = @sq0_p0", sql);
    }

    [Theory]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    public void NonMySql_BackslashIsAnOrdinaryCharacter(string name)
    {
        ISqlDialect dialect = name == "sqlserver" ? new SqlServerDialect() : new PostgreSqlDialect();

        var (sql, _) = ParameterRenamer.Rename(
            "SELECT * FROM t WHERE path = 'C:\\' AND id = @p0",
            Params(("@p0", 1)),
            "sq0",
            dialect);

        Assert.Equal("SELECT * FROM t WHERE path = 'C:\\' AND id = @sq0_p0", sql);
    }

    [Fact]
    public void Union_PassesTheDialect_ToBothOperands()
    {
        var connection = new PostgresRenamerConnection();

        var sql = connection.From<Product>().WhereRaw("tags[@a] = 1", new { a = 1 })
            .Union(connection.From<Product>().WhereRaw("tags[@b] = 1", new { b = 2 }))
            .ToSql();

        Assert.DoesNotContain("[@a]", sql);
        Assert.DoesNotContain("[@b]", sql);
    }

    [Fact]
    public void WhereInSubquery_PassesTheDialect()
    {
        var connection = new PostgresRenamerConnection();

        var sql = connection.From<Product>()
            .WhereInSubquery(p => p.ProductId, (Product q) => q.ProductId,
                connection.From<Product>().WhereRaw("tags[@a] = 1", new { a = 1 }))
            .ToSql();

        Assert.DoesNotContain("[@a]", sql);
    }

    [Fact]
    public void Cte_PassesTheDialect_ToBothAsOverloads()
    {
        var connection = new PostgresRenamerConnection();

        var fromBuilder = connection.Cte<Product>("Tagged")
            .As(q => q.WhereRaw("tags[@a] = 1", new { a = 1 }))
            .ToSql();
        var fromQuery = connection.Cte<Product>("Tagged")
            .As(connection.From<Product>().WhereRaw("tags[@a] = 1", new { a = 1 }))
            .ToSql();

        Assert.DoesNotContain("[@a]", fromBuilder);
        Assert.DoesNotContain("[@a]", fromQuery);
    }

    private sealed class PostgresRenamerConnection : IDbConnection
    {
        #pragma warning disable CS8767 // IDbConnection.ConnectionString is [AllowNull]; the attribute is not public on net472.
        public string ConnectionString { get => ""; set { } }
        #pragma warning restore CS8767
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
