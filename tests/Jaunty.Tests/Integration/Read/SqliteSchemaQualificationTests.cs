using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Tests.Entities;
using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// SQLite has schemas - main, temp, and every ATTACH alias - and SQLiteDialect used to discard the
/// schema it was handed. Nothing failed: an entity mapped to a schema-qualified table emitted an
/// unqualified statement, which SQLite resolves against main, so reads returned another table's
/// rows and writes landed in another database file. These tests put a decoy table of the same name
/// in main, so any statement that loses the schema hits the decoy and the assertion fails.
/// </summary>
public sealed class SqliteSchemaQualificationTests : IDisposable
{
    private readonly string _mainPath;
    private readonly string _archivePath;
    private readonly SqliteConnection _connection;

    public SqliteSchemaQualificationTests()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"jaunty_sqlite_schema_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _mainPath = Path.Combine(dir, "main.db");
        _archivePath = Path.Combine(dir, "archive.db");

        using (var seed = new SqliteConnection($"Data Source={_archivePath}"))
        {
            seed.Open();
            Execute(seed, "CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
            Execute(seed, "INSERT INTO widgets (id, name) VALUES (1, 'archive-one')");
            Execute(seed, "INSERT INTO widgets (id, name) VALUES (2, 'archive-two')");
        }

        _connection = new SqliteConnection($"Data Source={_mainPath}");
        _connection.Open();
        Execute(_connection, "CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        Execute(_connection, "INSERT INTO widgets (id, name) VALUES (1, 'main-decoy')");
        Execute(_connection, $"ATTACH DATABASE '{_archivePath.Replace("'", "''")}' AS archive");
    }

    [Fact]
    public void EscapeTableName_QualifiesTheTableWithItsSchema()
    {
        var dialect = new SQLiteDialect();

        Assert.Equal("archive.widgets", dialect.EscapeTableName("archive", "widgets"));
    }

    [Fact]
    public void GetAll_ReadsTheAttachedSchemaRatherThanMain()
    {
        var widgets = _connection.GetAll<AttachedWidget>();

        Assert.Equal(2, widgets.Count);
        Assert.Equal(new[] { "archive-one", "archive-two" }, widgets.Select(w => w.Name).OrderBy(n => n));
    }

    [Fact]
    public void GetAll_WithoutASchema_StillReadsMain()
    {
        var widgets = _connection.GetAll<MainWidget>();

        Assert.Equal("main-decoy", Assert.Single(widgets).Name);
    }

    [Fact]
    public void Insert_WritesIntoTheAttachedSchemaAndLeavesMainAlone()
    {
        _connection.Insert(new AttachedWidget { Id = 3, Name = "written-to-archive" });

        Assert.Equal("archive-one,archive-two,written-to-archive", NamesIn("archive.widgets"));
        Assert.Equal("main-decoy", NamesIn("main.widgets"));
    }

    [Fact]
    public void Update_TargetsTheAttachedSchema()
    {
        int affected = _connection.Update(new AttachedWidget { Id = 1, Name = "renamed-in-archive" });

        Assert.Equal(1, affected);
        Assert.Equal("archive-two,renamed-in-archive", NamesIn("archive.widgets"));
        Assert.Equal("main-decoy", NamesIn("main.widgets"));
    }

    [Fact]
    public void Delete_TargetsTheAttachedSchema()
    {
        int affected = _connection.Delete<AttachedWidget>(new AttachedWidget { Id = 1, Name = "archive-one" });

        Assert.Equal(1, affected);
        Assert.Equal("archive-two", NamesIn("archive.widgets"));
        Assert.Equal("main-decoy", NamesIn("main.widgets"));
    }

    [Fact]
    public void AnUpdateThatMatchesOnlyTheDecoyAffectsNothing()
    {
        Execute(_connection, "INSERT INTO main.widgets (id, name) VALUES (9, 'main-only')");

        int affected = _connection.Update(new AttachedWidget { Id = 9, Name = "should-not-apply" });

        Assert.Equal(0, affected);
        Assert.Equal("main-decoy,main-only", NamesIn("main.widgets"));
    }

    private string NamesIn(string qualifiedTable)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT group_concat(name) FROM (SELECT name FROM {qualifiedTable} ORDER BY name)";
        return Convert.ToString(cmd.ExecuteScalar()) ?? string.Empty;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(Path.GetDirectoryName(_mainPath)!, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
