using System.Data.SQLite;
using Jaunty.Attributes;

namespace Jaunty.Fluent.Tests.Integration;

[Table("widgets", "archive")]
public partial class FluentAttachedWidget
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

[Table("widgets")]
public partial class FluentMainWidget
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// The Fluent half of the SQLite schema tests. A decoy table of the same name sits in main, so a
/// query that loses the schema reads the decoy and the assertion fails rather than passing on the
/// wrong rows. See SqliteSchemaQualificationTests in Jaunty.Tests for the core CRUD half.
/// </summary>
public sealed class FluentSqliteSchemaQualificationTests : IDisposable
{
    private readonly string _directory;
    private readonly SQLiteConnection _connection;

    public FluentSqliteSchemaQualificationTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"jaunty_fluent_schema_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        string mainPath = Path.Combine(_directory, "main.db");
        string archivePath = Path.Combine(_directory, "archive.db");

        using (var seed = new SQLiteConnection($"Data Source={archivePath}"))
        {
            seed.Open();
            Execute(seed, "CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
            Execute(seed, "INSERT INTO widgets (id, name) VALUES (1, 'archive-one')");
            Execute(seed, "INSERT INTO widgets (id, name) VALUES (2, 'archive-two')");
        }

        _connection = new SQLiteConnection($"Data Source={mainPath}");
        _connection.Open();
        Execute(_connection, "CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        Execute(_connection, "INSERT INTO widgets (id, name) VALUES (1, 'main-decoy')");
        Execute(_connection, $"ATTACH DATABASE '{archivePath.Replace("'", "''")}' AS archive");
    }

    [Fact]
    public void ToSql_QualifiesTheFromTableWithItsSchema()
    {
        string sql = _connection.From<FluentAttachedWidget>().ToSql();

        Assert.Contains("FROM archive.widgets", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_ReadsTheAttachedSchemaRatherThanMain()
    {
        var widgets = _connection.From<FluentAttachedWidget>().Select().ToList();

        Assert.Equal(2, widgets.Count);
        Assert.DoesNotContain(widgets, w => w.Name == "main-decoy");
    }

    [Fact]
    public void Where_AgainstTheAttachedSchemaFiltersItsOwnRows()
    {
        var widgets = _connection.From<FluentAttachedWidget>()
            .Where(w => w.Id == 2)
            .Select()
            .ToList();

        Assert.Equal("archive-two", Assert.Single(widgets).Name);
    }

    [Fact]
    public void AJoinAcrossSchemasQualifiesBothSides()
    {
        string sql = _connection.From<FluentAttachedWidget>()
            .InnerJoin<FluentMainWidget>()
            .On((a, m) => a.Id == m.Id)
            .ToSql();

        Assert.Contains("FROM archive.widgets", sql, StringComparison.Ordinal);
        Assert.Contains("JOIN widgets", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AJoinAcrossSchemasReturnsTheRowThatExistsInBoth()
    {
        var rows = _connection.From<FluentAttachedWidget>()
            .InnerJoin<FluentMainWidget>()
            .On((a, m) => a.Id == m.Id)
            .Select()
            .ToList();

        Assert.Equal("archive-one", Assert.Single(rows).Name);
    }

    [Fact]
    public void AnUnqualifiedEntityStillReadsMain()
    {
        var widgets = _connection.From<FluentMainWidget>().Select().ToList();

        Assert.Equal("main-decoy", Assert.Single(widgets).Name);
    }

    private static void Execute(SQLiteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
