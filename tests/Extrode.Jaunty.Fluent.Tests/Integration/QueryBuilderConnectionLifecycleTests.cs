using System.Data;
using System.Data.SQLite;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Fluent;
using Extrode.Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// coverage-gaps-2026-09-20: the `if (_connection is not DbConnection dbConn) throw` async guard
/// and the `wasClosed` auto-open/close lifecycle are each repeated across most of the builders in
/// this project, but neither had a single test anywhere - every existing fixture hands in an
/// already-open real <see cref="SQLiteConnection"/>. Covers both via <see cref="QueryBuilder{T}"/>'s
/// <c>Delete</c>/<c>DeleteAsync</c>, which is representative of the shared pattern.
/// </summary>
public class QueryBuilderConnectionLifecycleTests
{
    [Table("qbcl_widget")]
    public class QbclWidget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task DeleteAsync_GivenAnIDbConnectionThatIsNotADbConnection_Throws()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        using SQLiteCommand seed = inner.CreateCommand();
        seed.CommandText = "CREATE TABLE qbcl_widget (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO qbcl_widget (id, name) VALUES (1, 'a');";
        seed.ExecuteNonQuery();

        using var connection = new IDbConnectionWrapper(inner);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connection.From<QbclWidget>().Where(w => w.Id == 1).DeleteAsync());

        Assert.Equal("Async operations require a DbConnection.", ex.Message);
    }

    [Fact]
    public void Delete_GivenAnIDbConnectionThatIsNotADbConnection_StillWorksSynchronously()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        using SQLiteCommand seed = inner.CreateCommand();
        seed.CommandText = "CREATE TABLE qbcl_widget (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO qbcl_widget (id, name) VALUES (1, 'a');";
        seed.ExecuteNonQuery();

        using var connection = new IDbConnectionWrapper(inner);

        int rows = connection.From<QbclWidget>().Where(w => w.Id == 1).Delete();

        Assert.Equal(1, rows);
    }

    [Fact]
    public void Delete_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"qbcl_{Guid.NewGuid():N}.db");
        try
        {
            using (var seed = new SQLiteConnection($"Data Source={dbPath}"))
            {
                seed.Open();
                using SQLiteCommand cmd = seed.CreateCommand();
                cmd.CommandText = "CREATE TABLE qbcl_widget (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO qbcl_widget (id, name) VALUES (1, 'a');";
                cmd.ExecuteNonQuery();
            }

            using var connection = new SQLiteConnection($"Data Source={dbPath}");

            int rows = connection.From<QbclWidget>().Where(w => w.Id == 1).Delete();

            Assert.Equal(1, rows);
            Assert.Equal(ConnectionState.Closed, connection.State);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task DeleteAsync_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"qbcl_{Guid.NewGuid():N}.db");
        try
        {
            using (var seed = new SQLiteConnection($"Data Source={dbPath}"))
            {
                seed.Open();
                using SQLiteCommand cmd = seed.CreateCommand();
                cmd.CommandText = "CREATE TABLE qbcl_widget (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO qbcl_widget (id, name) VALUES (1, 'a');";
                cmd.ExecuteNonQuery();
            }

            using var connection = new SQLiteConnection($"Data Source={dbPath}");

            int rows = await connection.From<QbclWidget>().Where(w => w.Id == 1).DeleteAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, rows);
            Assert.Equal(ConnectionState.Closed, connection.State);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }
}
