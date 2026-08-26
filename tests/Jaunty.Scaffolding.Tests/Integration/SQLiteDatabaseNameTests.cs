using Microsoft.Data.Sqlite;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R35-269 (the parsing) and AUD-R35-274 (the coverage). <c>ExtractDatabaseName</c> is the one
/// place in the four readers that read the connection string as raw text - a
/// <c>Data Source=([^;]+)</c> regex matched anywhere, values included, which is the pattern AUD-R26
/// moved <c>Scaffolder.DetectProvider</c> off. It also broke on the SQLite URI form the fixtures
/// use, answering "file:app" for <c>file:app.db?mode=memory&amp;cache=shared</c>. Nothing asserted
/// <c>DatabaseSchema.DatabaseName</c> on this path at all; the MySQL reader has
/// <c>ReadSchemaAsync_DatabaseName_MatchesConnectionDatabase</c> and SQLite had no counterpart.
/// </summary>
public class SQLiteDatabaseNameTests : IDisposable
{
    private readonly List<SqliteConnection> _open = [];

    public void Dispose()
    {
        foreach (SqliteConnection connection in _open)
            connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<string> ReadDatabaseNameAsync(string connectionString)
    {
        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
        _open.Add(keepAlive);

        using (SqliteCommand cmd = keepAlive.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE t (id INTEGER PRIMARY KEY)";
            cmd.ExecuteNonQuery();
        }

        var reader = new SQLiteSchemaReader();
        DatabaseSchema schema = await reader.ReadSchemaAsync(connectionString, new SchemaReaderOptions());
        return schema.DatabaseName;
    }

    [Fact]
    public async Task TheDatabaseNameIsTheDataSource_ForTheSharedCacheForm()
    {
        var name = $"NamedSchemaTest_{Guid.NewGuid():N}";

        var databaseName = await ReadDatabaseNameAsync($"Data Source={name};Mode=Memory;Cache=Shared");

        Assert.Equal(name, databaseName);
    }

    /// <summary>
    /// The URI form: the "file:" prefix and the query string are not part of the name. Before the
    /// fix this answered "file:app_&lt;guid&gt;", the query having been taken for the extension.
    /// </summary>
    [Fact]
    public async Task TheDatabaseNameStripsTheUriPrefixAndQuery()
    {
        var stem = $"app_{Guid.NewGuid():N}";

        var databaseName = await ReadDatabaseNameAsync($"Data Source=file:{stem}.db?mode=memory&cache=shared");

        Assert.Equal(stem, databaseName);
    }

    /// <summary>
    /// The reader's own connection is a real file here, so this also proves the name comes from the
    /// path rather than from <c>connection.Database</c>, which SQLite reports as "main".
    /// </summary>
    [Fact]
    public async Task TheDatabaseNameIsTheFileStem_ForAFileBackedDatabase()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"JauntySQLiteName_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "inventory.db");

        try
        {
            var databaseName = await ReadDatabaseNameAsync($"Data Source={path}");

            Assert.Equal("inventory", databaseName);
            Assert.NotEqual("main", databaseName);
        }
        finally
        {
            foreach (SqliteConnection connection in _open)
                connection.Dispose();
            _open.Clear();
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// The shapes that never reach a successful open, asserted against the helper directly.
    /// </summary>
    [Theory]
    [InlineData("Data Source=:memory:", "memory")]
    [InlineData("Filename=reports.sqlite", "reports")]
    [InlineData("DataSource=archive.db", "archive")]
    [InlineData("Data Source=/var/data/sales.sqlite3;Cache=Shared", "sales")]
    [InlineData("Data Source=file:/var/data/sales.db?mode=ro", "sales")]
    [InlineData("Cache=Shared", "SQLite")]
    [InlineData("Data Source=", "SQLite")]
    [InlineData("", "SQLite")]
    public void TheNameIsReadFromTheKeywordValuePairs(string connectionString, string expected) =>
        Assert.Equal(expected, SQLiteSchemaReader.ExtractDatabaseName(connectionString));

    /// <summary>
    /// The regex this replaced matched <c>Data Source=</c> anywhere in the string, so a value that
    /// merely contained the phrase answered for the real one.
    /// </summary>
    [Fact]
    public void AValueContainingTheKeywordIsNotMistakenForIt() =>
        Assert.Equal("real", SQLiteSchemaReader.ExtractDatabaseName(
            "Password=\"Data Source=decoy.db\";Data Source=real.db"));

    /// <summary>
    /// A string the builder cannot parse is not a crash: the connection will fail on it anyway, and
    /// this method only supplies a display name.
    /// </summary>
    [Fact]
    public void AnUnparseableConnectionStringAnswersTheGenericName() =>
        Assert.Equal("SQLite", SQLiteSchemaReader.ExtractDatabaseName("Data Source=\"unterminated"));
}
