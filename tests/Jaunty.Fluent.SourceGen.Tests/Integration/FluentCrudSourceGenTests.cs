using System.Data;

using Jaunty;
using Jaunty.Fluent.SourceGen.Tests.Entities;

using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace Jaunty.Fluent.SourceGen.Tests.Integration;

/// <summary>
/// Spec 003 (fluent NativeAOT-safe metadata) task T009: proves core CRUD
/// (Insert/Update/Delete/Upsert, which routes through CrudSqlCache) resolves
/// source-generated entities across all 4 real dialects with zero calls to
/// UseReflectionMapping() - this project has no reference to
/// Jaunty.Extensions.Reflection at all (see the .csproj). Skips per-dialect if the
/// corresponding JAUNTY_TEST_* connection string env var isn't set, matching
/// Jaunty.Tests' existing convention for optional real-database coverage.
/// </summary>
public sealed class FluentCrudSourceGenTests
{
    [Fact]
    public void SqlServer_InsertUpdateDeleteUpsert_RoundTrips()
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
                IF OBJECT_ID('crud_sourcegen_widgets', 'U') IS NOT NULL DROP TABLE crud_sourcegen_widgets;
                CREATE TABLE crud_sourcegen_widgets (
                    widget_id INT IDENTITY(1,1) PRIMARY KEY,
                    name NVARCHAR(100) NOT NULL,
                    price DECIMAL(10,2) NOT NULL
                );
                IF OBJECT_ID('crud_sourcegen_settings', 'U') IS NOT NULL DROP TABLE crud_sourcegen_settings;
                CREATE TABLE crud_sourcegen_settings (
                    setting_key NVARCHAR(50) PRIMARY KEY,
                    setting_value NVARCHAR(200) NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        RunCrudRoundTrip(connection);
        RunUpsertRoundTrip(connection);
    }

    [Fact]
    public void Postgres_InsertUpdateDeleteUpsert_RoundTrips()
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
                DROP TABLE IF EXISTS crud_sourcegen_widgets;
                CREATE TABLE crud_sourcegen_widgets (
                    widget_id SERIAL PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    price NUMERIC(10,2) NOT NULL
                );
                DROP TABLE IF EXISTS crud_sourcegen_settings;
                CREATE TABLE crud_sourcegen_settings (
                    setting_key VARCHAR(50) PRIMARY KEY,
                    setting_value VARCHAR(200) NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        RunCrudRoundTrip(connection);
        RunUpsertRoundTrip(connection);
    }

    [Fact]
    public void MySql_InsertUpdateDeleteUpsert_RoundTrips()
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
                DROP TABLE IF EXISTS crud_sourcegen_widgets;
                CREATE TABLE crud_sourcegen_widgets (
                    widget_id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    price DECIMAL(10,2) NOT NULL
                );
                DROP TABLE IF EXISTS crud_sourcegen_settings;
                CREATE TABLE crud_sourcegen_settings (
                    setting_key VARCHAR(50) PRIMARY KEY,
                    setting_value VARCHAR(200) NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        RunCrudRoundTrip(connection);
        RunUpsertRoundTrip(connection);
    }

    [Fact]
    public void MariaDb_InsertUpdateDeleteUpsert_RoundTrips()
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
                DROP TABLE IF EXISTS crud_sourcegen_widgets;
                CREATE TABLE crud_sourcegen_widgets (
                    widget_id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    price DECIMAL(10,2) NOT NULL
                );
                DROP TABLE IF EXISTS crud_sourcegen_settings;
                CREATE TABLE crud_sourcegen_settings (
                    setting_key VARCHAR(50) PRIMARY KEY,
                    setting_value VARCHAR(200) NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        RunCrudRoundTrip(connection);
        RunUpsertRoundTrip(connection);
    }

    private static void RunCrudRoundTrip(IDbConnection connection)
    {
        var widget = new CrudSourceGenWidget { Name = "Gadget", Price = 9.99m };
        long generatedId = connection.Insert(widget);
        Assert.True(generatedId > 0);
        widget.WidgetId = (int)generatedId;

        widget.Name = "Updated Gadget";
        widget.Price = 14.99m;
        int updateRows = connection.Update(widget);
        Assert.Equal(1, updateRows);

        CrudSourceGenWidget? fetched = connection.Get<CrudSourceGenWidget>(widget.WidgetId);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Gadget", fetched.Name);
        Assert.Equal(14.99m, fetched.Price);

        int deleteRows = connection.Delete(widget);
        Assert.Equal(1, deleteRows);
    }

    // Natural (non-identity) key, matching Jaunty.Tests' UpsertTests.cs convention - see
    // CrudSourceGenSetting.cs for why this uses a separate table from the identity-keyed
    // widget round trip above.
    private static void RunUpsertRoundTrip(IDbConnection connection)
    {
        var setting = new CrudSourceGenSetting { SettingKey = "theme", SettingValue = "dark" };
        int insertRows = connection.Upsert(setting);
        Assert.Equal(1, insertRows);

        setting.SettingValue = "light";
        int updateRows = connection.Upsert(setting);
        // MySQL/MariaDB's ON DUPLICATE KEY UPDATE reports 2 affected rows for an update
        // (not 1) per documented client-library behavior - assert "succeeded", not an
        // exact dialect-specific literal.
        Assert.True(updateRows > 0);

        CrudSourceGenSetting? fetched = connection.Get<CrudSourceGenSetting>("theme");
        Assert.NotNull(fetched);
        Assert.Equal("light", fetched.SettingValue);
    }
}
