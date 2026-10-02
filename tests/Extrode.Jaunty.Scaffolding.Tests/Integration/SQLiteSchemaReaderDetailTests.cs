using Microsoft.Data.Sqlite;

using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Schema;

namespace Extrode.Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// Details of what the SQLite reader reports: the synthesized key
/// constraint names, a foreign key to a non-key parent column, and database names for data sources
/// with no file name.
/// </summary>
public class SQLiteSchemaReaderDetailTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SQLiteSchemaReaderDetailTests()
    {
        _connectionString = $"Data Source=ReaderDetail_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }

    private void Exec(string sql)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private async Task<TableSchema> ReadAsync(string table)
    {
        DatabaseSchema schema = await new SQLiteSchemaReader().ReadSchemaAsync(
            _connectionString, new SchemaReaderOptions { IncludeForeignKeys = true });
        return schema.Tables.Single(t => t.TableName == table);
    }

    [Fact]
    public async Task KeyConstraintNames_AreSynthesizedFromTheTableAndColumn()
    {
        Exec("CREATE TABLE parent (id INTEGER PRIMARY KEY, code TEXT UNIQUE)");
        Exec("CREATE TABLE child (id INTEGER PRIMARY KEY, parent_id INTEGER REFERENCES parent(id))");

        TableSchema child = await ReadAsync("child");

        Assert.Equal("pk_child", child.PrimaryKey!.ConstraintName);
        Assert.Equal("fk_child_parent_id", Assert.Single(child.ForeignKeys).ConstraintName);
    }

    [Fact]
    public async Task AForeignKeyToANonKeyColumn_KeepsThatColumn()
    {
        Exec("CREATE TABLE parent (id INTEGER PRIMARY KEY, code TEXT UNIQUE)");
        Exec("CREATE TABLE child (id INTEGER PRIMARY KEY, parent_code TEXT REFERENCES parent(code))");

        TableSchema child = await ReadAsync("child");

        Assert.Equal("code", Assert.Single(child.ForeignKeys).ReferencedColumn);
    }

    [Theory]
    [InlineData("Data Source=?x.db", "SQLite")]
    [InlineData("Data Source=somedir/", "SQLite")]
    public void ADataSourceWithNoFileName_UsesTheGenericName(string connectionString, string expected)
        => Assert.Equal(expected, SQLiteSchemaReader.ExtractDatabaseName(connectionString));
}
