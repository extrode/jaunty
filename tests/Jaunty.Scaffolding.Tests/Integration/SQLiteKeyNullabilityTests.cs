using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R35-044. SQLite is the only provider where PRIMARY KEY does not imply NOT NULL, and the
/// reader forced <c>IsNullable = false</c> on every key column anyway. Each case here creates the
/// table, proves what SQLite actually allows by inserting into it, and only then asserts what the
/// reader reports - so the assertion is anchored to the engine rather than to the old code.
/// </summary>
public class SQLiteKeyNullabilityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SQLiteKeyNullabilityTests()
    {
        _connectionString = $"Data Source=KeyNullability_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
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

    private bool NullKeyIsAccepted(string insert)
    {
        try
        {
            Execute(insert);
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private async Task<ColumnSchema> ReadColumnAsync(string table, string column)
    {
        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions());

        TableSchema tableSchema = schema.Tables.Single(t => t.TableName == table);
        return tableSchema.Columns.Single(c => c.ColumnName == column);
    }

    /// <summary>
    /// The defect: a TEXT primary key in a rowid table accepts NULL, and the reader called it
    /// non-nullable - so the generator emitted <c>string Id = string.Empty;</c> and a genuine
    /// NULL came back as "" rather than as null.
    /// </summary>
    [Fact]
    public async Task ATextKeyThatAcceptsNullIsReportedNullable()
    {
        Execute("CREATE TABLE text_key (id TEXT PRIMARY KEY, note TEXT)");

        Assert.True(NullKeyIsAccepted("INSERT INTO text_key (id, note) VALUES (NULL, 'a')"));

        ColumnSchema column = await ReadColumnAsync("text_key", "id");

        Assert.True(column.IsPrimaryKey);
        Assert.True(column.IsNullable);
    }

    /// <summary>
    /// The same column declared NOT NULL is genuinely non-nullable, and still reported so.
    /// </summary>
    [Fact]
    public async Task ATextKeyDeclaredNotNullIsReportedNonNullable()
    {
        Execute("CREATE TABLE strict_key (id TEXT NOT NULL PRIMARY KEY, note TEXT)");

        Assert.False(NullKeyIsAccepted("INSERT INTO strict_key (id, note) VALUES (NULL, 'a')"));

        ColumnSchema column = await ReadColumnAsync("strict_key", "id");

        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// INTEGER PRIMARY KEY is the rowid alias: inserting NULL assigns the next rowid rather than
    /// storing one, so the column can never read back null and stays non-nullable.
    /// </summary>
    [Fact]
    public async Task ARowidAliasStaysNonNullable()
    {
        Execute("CREATE TABLE rowid_key (id INTEGER PRIMARY KEY, note TEXT)");

        Assert.True(NullKeyIsAccepted("INSERT INTO rowid_key (id, note) VALUES (NULL, 'a')"));

        using (SqliteCommand cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM rowid_key WHERE id IS NULL";
            Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
        }

        ColumnSchema column = await ReadColumnAsync("rowid_key", "id");

        Assert.True(column.IsPrimaryKey);
        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// WITHOUT ROWID is the case SQLite does enforce, for any key column type.
    /// </summary>
    [Fact]
    public async Task AWithoutRowidKeyStaysNonNullable()
    {
        Execute("CREATE TABLE wr_key (id TEXT PRIMARY KEY, note TEXT) WITHOUT ROWID");

        Assert.False(NullKeyIsAccepted("INSERT INTO wr_key (id, note) VALUES (NULL, 'a')"));

        ColumnSchema column = await ReadColumnAsync("wr_key", "id");

        Assert.False(column.IsNullable);
    }

    /// <summary>
    /// A composite key in a rowid table is not the rowid alias, so its undeclared columns accept
    /// NULL - and one of them declared NOT NULL still does not.
    /// </summary>
    [Fact]
    public async Task ACompositeKeyReportsEachColumnAsDeclared()
    {
        Execute(
            "CREATE TABLE composite_key (a INTEGER NOT NULL, b INTEGER, note TEXT, PRIMARY KEY (a, b))");

        Assert.True(NullKeyIsAccepted("INSERT INTO composite_key (a, b, note) VALUES (1, NULL, 'x')"));

        ColumnSchema a = await ReadColumnAsync("composite_key", "a");
        ColumnSchema b = await ReadColumnAsync("composite_key", "b");

        Assert.False(a.IsNullable);
        Assert.True(b.IsNullable);
    }

    /// <summary>
    /// Non-key columns are untouched by this - the control.
    /// </summary>
    [Fact]
    public async Task ANonKeyColumnIsUnaffected()
    {
        Execute("CREATE TABLE plain (id INTEGER PRIMARY KEY, note TEXT, tag TEXT NOT NULL)");

        Assert.True((await ReadColumnAsync("plain", "note")).IsNullable);
        Assert.False((await ReadColumnAsync("plain", "tag")).IsNullable);
    }

    /// <summary>
    /// End to end: the nullable key reaches the generated entity as a nullable property, which is
    /// the thing that was actually wrong about it.
    /// </summary>
    [Fact]
    public async Task ANullableKeyScaffoldsANullableProperty()
    {
        Execute("CREATE TABLE loose_key (id TEXT PRIMARY KEY, note TEXT)");

        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions());

        string code = new EntityCodeGenerator(new SQLiteTypeMapper())
            .GenerateEntity(
                schema.Tables.Single(t => t.TableName == "loose_key"),
                new CodeGeneratorOptions { Namespace = "N" });

        Assert.Contains("public string? Id", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Id { get; set; } = string.Empty;", code, StringComparison.Ordinal);
    }
}
