using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Integration tests for QueryPartialList - returns raw rows as column-name-keyed dictionaries.
/// AUD-R11 batch-01: the CommandOptions and (parameters, CommandOptions) overloads had zero tests.
/// </summary>
public class QueryPartialListTests : IClassFixture<DialectFixture>
{
    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE partial_list_widgets (id INTEGER PRIMARY KEY, name TEXT, price REAL);
            INSERT INTO partial_list_widgets VALUES (1, 'Widget A', 9.99);
            INSERT INTO partial_list_widgets VALUES (2, 'Widget B', 19.99);
            INSERT INTO partial_list_widgets VALUES (3, NULL, 29.99);");
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_Sql_ReturnsAllRowsAsDictionaries(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryPartialList("SELECT id, name, price FROM partial_list_widgets ORDER BY id");

        Assert.Equal(3, results.Count);
        Assert.Equal(1L, results[0]["id"]);
        Assert.Equal("Widget A", results[0]["name"]);
        Assert.Equal(9.99, results[0]["price"]);
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_Sql_CoercesDbNullToNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryPartialList("SELECT id, name FROM partial_list_widgets ORDER BY id");

        Assert.Null(results[2]["name"]);
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_SqlAndParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryPartialList(
            "SELECT id, name, price FROM partial_list_widgets WHERE id = @Id",
            new { Id = 2 });

        Assert.Single(results);
        Assert.Equal("Widget B", results[0]["name"]);
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_SqlAndParameters_ReturnsEmptyListWhenNoMatch(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryPartialList(
            "SELECT id, name, price FROM partial_list_widgets WHERE id = @Id",
            new { Id = -999 });

        Assert.Empty(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_SqlAndCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE partial_list_widgets SET name = 'TXN-SENTINEL' WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions(transaction: txn);
        var results = connection.QueryPartialList(
            "SELECT id, name FROM partial_list_widgets WHERE id = 1", options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0]["name"]);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_SqlAndCommandOptions_ReturnsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions();

        var results = connection.QueryPartialList(
            "SELECT id, name, price FROM partial_list_widgets ORDER BY id", options);

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_SqlParametersAndCommandOptions_FiltersRowsUnderTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE partial_list_widgets SET price = 100.00 WHERE id = 2";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions(transaction: txn);
        var results = connection.QueryPartialList(
            "SELECT id, name, price FROM partial_list_widgets WHERE id = @Id",
            new { Id = 2 },
            options);

        Assert.Single(results);
        Assert.Equal(100.00, results[0]["price"]);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void QueryPartialList_SqlParametersAndCommandOptions_ReturnsEmptyListWhenNoMatch(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions();

        var results = connection.QueryPartialList(
            "SELECT id, name, price FROM partial_list_widgets WHERE id = @Id",
            new { Id = -999 },
            options);

        Assert.Empty(results);
    }
}
