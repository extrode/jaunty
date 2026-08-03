using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R35-078. <c>ReadColumnsAsync</c> read <c>PRAGMA table_info</c>, which omits generated columns
/// entirely, and then hard-coded <c>IsComputed = false</c> - so a GENERATED ALWAYS AS column was
/// dropped from the scaffolded entity with no diagnostic, where the SQL Server twin reads
/// <c>c.is_computed</c> and reports it. Each case proves what SQLite actually returns from both
/// pragmas before asserting what the reader reports, so the assertions are anchored to the engine.
/// </summary>
public class SQLiteGeneratedColumnTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SQLiteGeneratedColumnTests()
    {
        _connectionString = $"Data Source=GeneratedColumns_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        Execute("""
            CREATE TABLE g(
                a TEXT NOT NULL,
                b TEXT GENERATED ALWAYS AS (a || 'x') STORED,
                c TEXT GENERATED ALWAYS AS (a || 'y') VIRTUAL)
            """);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Execute(string sql)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private List<string> PragmaColumnNames(string pragma)
    {
        var names = new List<string>();
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA {pragma}('g')";
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(1));
        return names;
    }

    private async Task<TableSchema> ReadTableAsync()
    {
        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions());

        return schema.Tables.Single(t => t.TableName == "g");
    }

    // ------------------------------------------------------------------
    // What the engine actually does.
    // ------------------------------------------------------------------

    [Fact]
    public void TableInfo_OmitsGeneratedColumns()
        => Assert.Equal(["a"], PragmaColumnNames("table_info"));

    [Fact]
    public void TableXInfo_ReportsGeneratedColumns()
        => Assert.Equal(["a", "b", "c"], PragmaColumnNames("table_xinfo"));

    [Fact]
    public void TheHiddenColumn_IsAtOrdinalSix_AndIsThreeForStoredAndTwoForVirtual()
    {
        var hidden = new Dictionary<string, int>();
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_xinfo('g')";
        using (SqliteDataReader reader = cmd.ExecuteReader())
        {
            Assert.Equal("hidden", reader.GetName(6));
            while (reader.Read())
                hidden[reader.GetString(1)] = reader.GetInt32(6);
        }

        Assert.Equal(0, hidden["a"]);
        Assert.Equal(3, hidden["b"]);
        Assert.Equal(2, hidden["c"]);
    }

    [Fact]
    public void TheGeneratedColumns_AreReadable()
    {
        Execute("INSERT INTO g(a) VALUES ('v')");

        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT b, c FROM g";
        using SqliteDataReader reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("vx", reader.GetString(0));
        Assert.Equal("vy", reader.GetString(1));
    }

    // ------------------------------------------------------------------
    // What the reader reports.
    // ------------------------------------------------------------------

    [Fact]
    public async Task TheReader_ReturnsTheGeneratedColumns()
    {
        TableSchema table = await ReadTableAsync();

        Assert.Equal(["a", "b", "c"], table.Columns.Select(c => c.ColumnName).ToArray());
    }

    [Fact]
    public async Task AStoredGeneratedColumn_IsComputed()
    {
        TableSchema table = await ReadTableAsync();

        Assert.True(table.Columns.Single(c => c.ColumnName == "b").IsComputed);
    }

    [Fact]
    public async Task AVirtualGeneratedColumn_IsComputed()
    {
        TableSchema table = await ReadTableAsync();

        Assert.True(table.Columns.Single(c => c.ColumnName == "c").IsComputed);
    }

    [Fact]
    public async Task AnOrdinaryColumn_IsNotComputed()
    {
        TableSchema table = await ReadTableAsync();

        Assert.False(table.Columns.Single(c => c.ColumnName == "a").IsComputed);
    }

    [Fact]
    public async Task AGeneratedColumn_KeepsItsDeclaredTypeAndNullability()
    {
        ColumnSchema b = (await ReadTableAsync()).Columns.Single(c => c.ColumnName == "b");

        Assert.Equal("TEXT", b.DataType);
        Assert.True(b.IsNullable);
        Assert.False(b.IsPrimaryKey);
        Assert.False(b.IsIdentity);
    }

    // ------------------------------------------------------------------
    // A virtual table's hidden columns (hidden == 1) stay omitted, exactly as table_info had them.
    // ------------------------------------------------------------------

    [Fact]
    public async Task AVirtualTablesHiddenColumns_AreStillOmitted()
    {
        Execute("CREATE VIRTUAL TABLE vt USING fts5(body)");

        var hidden = new List<(string Name, int Hidden)>();
        using (SqliteCommand cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_xinfo('vt')";
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
                hidden.Add((reader.GetString(1), reader.GetInt32(6)));
        }

        // fts5 exposes body plus hidden==1 shadow columns; if it ever stops, this test is telling
        // us the premise changed rather than that the reader regressed.
        Assert.Contains(hidden, h => h.Hidden == 1);

        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions());

        TableSchema? vt = schema.Tables.SingleOrDefault(t => t.TableName == "vt");
        if (vt is not null)
        {
            foreach ((string name, int h) in hidden.Where(h => h.Hidden == 1))
                Assert.DoesNotContain(vt.Columns, c => c.ColumnName == name);
        }
    }

    // ------------------------------------------------------------------
    // Controls: the ordinary path is untouched by the pragma swap.
    // ------------------------------------------------------------------

    [Fact]
    public async Task APlainTable_StillReadsItsColumnsKeysAndNullability()
    {
        Execute("CREATE TABLE p(id INTEGER PRIMARY KEY, name TEXT NOT NULL, note TEXT DEFAULT 'n')");

        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions());
        TableSchema p = schema.Tables.Single(t => t.TableName == "p");

        Assert.Equal(["id", "name", "note"], p.Columns.Select(c => c.ColumnName).ToArray());

        ColumnSchema id = p.Columns.Single(c => c.ColumnName == "id");
        Assert.True(id.IsPrimaryKey);
        Assert.True(id.IsIdentity);
        Assert.False(id.IsComputed);

        Assert.False(p.Columns.Single(c => c.ColumnName == "name").IsNullable);
        Assert.True(p.Columns.Single(c => c.ColumnName == "note").IsNullable);
        Assert.Equal("'n'", p.Columns.Single(c => c.ColumnName == "note").DefaultValue);
    }

    [Fact]
    public async Task ACompositeKey_StillReportsBothColumnsInDeclarationOrder()
    {
        Execute("CREATE TABLE ck(x INTEGER NOT NULL, y INTEGER NOT NULL, PRIMARY KEY (y, x))");

        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions());
        TableSchema ck = schema.Tables.Single(t => t.TableName == "ck");

        Assert.Equal(["y", "x"], ck.PrimaryKey!.Columns.ToArray());
        Assert.Equal(["x", "y"], ck.Columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName).ToArray());
    }
}
