using System.Data;

using Jaunty.Fluent.SourceGen.Tests.Entities;

using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace Jaunty.Fluent.SourceGen.Tests.Integration;

/// <summary>
/// Spec 004 (gap #13, GroupBy+joins) T010: proves GroupBy-on-joins works against all 4 real
/// dialects (SqlServer/Postgres/MySQL/MariaDB), reusing SourceGenWidget/SourceGenWidgetTag -
/// both have a "widget_id" column, deliberately exercising the table-alias-qualification fix
/// (a naive unqualified GROUP BY/SELECT column reference is ambiguous once both joined tables
/// share a column name, and fails at execution on every one of these 4 real engines).
/// SQLite coverage lives in tests/Jaunty.Fluent.Tests/Integration/FluentGroupByJoinTests.cs
/// (the project every other Fluent SQLite integration test already lives in).
/// </summary>
public sealed class FluentGroupByJoinSourceGenTests
{
    [Fact]
    public void SqlServer_GroupByJoin_ReturnsAggregatedResults()
    {
        var connStr = Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Assert.Skip("JAUNTY_TEST_SQLSERVER not set");
            return;
        }

        using var connection = new SqlConnection(connStr);
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                IF OBJECT_ID('sourcegen_widgets', 'U') IS NOT NULL DROP TABLE sourcegen_widgets;
                CREATE TABLE sourcegen_widgets (widget_id INT PRIMARY KEY, name NVARCHAR(100) NOT NULL, price DECIMAL(10,2) NOT NULL);
                IF OBJECT_ID('sourcegen_widget_tags', 'U') IS NOT NULL DROP TABLE sourcegen_widget_tags;
                CREATE TABLE sourcegen_widget_tags (tag_id INT PRIMARY KEY, widget_id INT NOT NULL, tag NVARCHAR(50) NOT NULL);
                INSERT INTO sourcegen_widgets VALUES (1, 'Gadget', 9.99), (2, 'Gizmo', 19.99);
                INSERT INTO sourcegen_widget_tags VALUES (1, 1, 'new'), (2, 1, 'sale'), (3, 2, 'new');
                """;
            cmd.ExecuteNonQuery();
        }

        RunGroupByJoinAssertions(connection);
    }

    [Fact]
    public void Postgres_GroupByJoin_ReturnsAggregatedResults()
    {
        var connStr = Environment.GetEnvironmentVariable("JAUNTY_TEST_POSTGRESQL");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Assert.Skip("JAUNTY_TEST_POSTGRESQL not set");
            return;
        }

        using var connection = new NpgsqlConnection(connStr);
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                DROP TABLE IF EXISTS sourcegen_widget_tags;
                DROP TABLE IF EXISTS sourcegen_widgets;
                CREATE TABLE sourcegen_widgets (widget_id INT PRIMARY KEY, name VARCHAR(100) NOT NULL, price NUMERIC(10,2) NOT NULL);
                CREATE TABLE sourcegen_widget_tags (tag_id INT PRIMARY KEY, widget_id INT NOT NULL, tag VARCHAR(50) NOT NULL);
                INSERT INTO sourcegen_widgets VALUES (1, 'Gadget', 9.99), (2, 'Gizmo', 19.99);
                INSERT INTO sourcegen_widget_tags VALUES (1, 1, 'new'), (2, 1, 'sale'), (3, 2, 'new');
                """;
            cmd.ExecuteNonQuery();
        }

        RunGroupByJoinAssertions(connection);
    }

    [Fact]
    public void MySql_GroupByJoin_ReturnsAggregatedResults()
    {
        var connStr = Environment.GetEnvironmentVariable("JAUNTY_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Assert.Skip("JAUNTY_TEST_MYSQL not set");
            return;
        }

        using var connection = new MySqlConnection(connStr);
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                DROP TABLE IF EXISTS sourcegen_widget_tags;
                DROP TABLE IF EXISTS sourcegen_widgets;
                CREATE TABLE sourcegen_widgets (widget_id INT PRIMARY KEY, name VARCHAR(100) NOT NULL, price DECIMAL(10,2) NOT NULL);
                CREATE TABLE sourcegen_widget_tags (tag_id INT PRIMARY KEY, widget_id INT NOT NULL, tag VARCHAR(50) NOT NULL);
                INSERT INTO sourcegen_widgets VALUES (1, 'Gadget', 9.99), (2, 'Gizmo', 19.99);
                INSERT INTO sourcegen_widget_tags VALUES (1, 1, 'new'), (2, 1, 'sale'), (3, 2, 'new');
                """;
            cmd.ExecuteNonQuery();
        }

        RunGroupByJoinAssertions(connection);
    }

    [Fact]
    public void MariaDb_GroupByJoin_ReturnsAggregatedResults()
    {
        var connStr = Environment.GetEnvironmentVariable("JAUNTY_TEST_MARIADB");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Assert.Skip("JAUNTY_TEST_MARIADB not set");
            return;
        }

        using var connection = new MySqlConnection(connStr);
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                DROP TABLE IF EXISTS sourcegen_widget_tags;
                DROP TABLE IF EXISTS sourcegen_widgets;
                CREATE TABLE sourcegen_widgets (widget_id INT PRIMARY KEY, name VARCHAR(100) NOT NULL, price DECIMAL(10,2) NOT NULL);
                CREATE TABLE sourcegen_widget_tags (tag_id INT PRIMARY KEY, widget_id INT NOT NULL, tag VARCHAR(50) NOT NULL);
                INSERT INTO sourcegen_widgets VALUES (1, 'Gadget', 9.99), (2, 'Gizmo', 19.99);
                INSERT INTO sourcegen_widget_tags VALUES (1, 1, 'new'), (2, 1, 'sale'), (3, 2, 'new');
                """;
            cmd.ExecuteNonQuery();
        }

        RunGroupByJoinAssertions(connection);
    }

    private static void RunGroupByJoinAssertions(IDbConnection connection)
    {
        var results = connection.From<SourceGenWidget>()
            .InnerJoin<SourceGenWidgetTag>().On((w, t) => w.WidgetId == t.WidgetId)
            .GroupBy((w, t) => w.WidgetId)
            .Select(g => new
            {
                WidgetId = g.Key,
                TagCount = g.Count(),
                TotalPrice = g.Sum((w, t) => w.Price)
            });

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.WidgetId == 1 && r.TagCount == 2 && r.TotalPrice == 19.98m);
        Assert.Contains(results, r => r.WidgetId == 2 && r.TagCount == 1 && r.TotalPrice == 19.99m);

        var havingResults = connection.From<SourceGenWidget>()
            .InnerJoin<SourceGenWidgetTag>().On((w, t) => w.WidgetId == t.WidgetId)
            .GroupBy((w, t) => w.WidgetId)
            .Having(g => g.Count() > 1)
            .Select(g => new { WidgetId = g.Key, TagCount = g.Count() });

        var havingResult = Assert.Single(havingResults);
        Assert.Equal(1, havingResult.WidgetId);
        Assert.Equal(2, havingResult.TagCount);
    }
}
