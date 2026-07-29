using Microsoft.Data.Sqlite;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R26 regressions for two <see cref="SQLiteSchemaReader"/> defects that the existing
/// fixture in <see cref="SQLiteSchemaReaderTests"/> does not reach, because every table it
/// creates spells its foreign keys with an explicit parent column and none of them use table
/// options.
/// </summary>
public sealed class SQLiteSchemaReaderEdgeCaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public SQLiteSchemaReaderEdgeCaseTests()
    {
        _connectionString = $"Data Source=SQLiteEdgeCases_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    private void Exec(string sql)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
    }

    // ------------------------------------------------------------------
    // Foreign keys that name no parent column
    // ------------------------------------------------------------------

    /// <summary>
    /// Before the fix this threw <c>InvalidOperationException: The data is NULL at ordinal 4</c>
    /// out of <c>reader.GetString(4)</c>. Because foreign keys are read inside the per-table
    /// loop, the whole scaffold failed - not just this one table.
    /// </summary>
    [Fact]
    public async Task ImplicitReference_DoesNotThrow_AndResolvesToTheParentPrimaryKey()
    {
        Exec("CREATE TABLE parent (id INTEGER PRIMARY KEY, code TEXT)");
        Exec("CREATE TABLE child (id INTEGER PRIMARY KEY, pid INTEGER REFERENCES parent)");

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions { IncludeForeignKeys = true });

        TableSchema child = schema.Tables.Single(t => t.TableName == "child");
        ForeignKeyInfo fk = Assert.Single(child.ForeignKeys);
        Assert.Equal("pid", fk.ForeignKeyColumn);
        Assert.Equal("parent", fk.ReferencedTable);
        Assert.Equal("id", fk.ReferencedColumn);
    }

    /// <summary>
    /// The implicit spelling must produce exactly what the explicit spelling produces. The
    /// parent's key is declared (y, x) while its columns are laid out (x, y), so a reader that
    /// took the key in physical order instead of declaration order would map a-&gt;x, b-&gt;y and
    /// this would catch it.
    /// </summary>
    [Fact]
    public async Task CompositeImplicitReference_MatchesTheExplicitSpelling_InDeclarationOrder()
    {
        Exec("CREATE TABLE p2 (x TEXT, y TEXT, PRIMARY KEY (y, x))");
        Exec("CREATE TABLE c_implicit (a TEXT, b TEXT, FOREIGN KEY (a, b) REFERENCES p2)");
        Exec("CREATE TABLE c_explicit (a TEXT, b TEXT, FOREIGN KEY (a, b) REFERENCES p2 (y, x))");

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions { IncludeForeignKeys = true });

        var implicitPairs = Pairs(schema, "c_implicit");
        var explicitPairs = Pairs(schema, "c_explicit");

        Assert.Equal([("a", "y"), ("b", "x")], implicitPairs);
        Assert.Equal(explicitPairs, implicitPairs);

        static List<(string From, string To)> Pairs(DatabaseSchema schema, string table) =>
            schema.Tables.Single(t => t.TableName == table).ForeignKeys
                .Select(f => (f.ForeignKeyColumn, f.ReferencedColumn))
                .ToList();
    }

    /// <summary>
    /// SQLite lets you declare a reference to a table with no primary key; it only rejects it at
    /// DML time, with "foreign key mismatch". There is no column to point at, so the reader must
    /// neither throw nor invent a target.
    /// </summary>
    [Fact]
    public async Task ImplicitReferenceToAKeylessParent_IsDroppedRatherThanInvented()
    {
        Exec("CREATE TABLE keyless (z TEXT)");
        Exec("CREATE TABLE refs_keyless (a TEXT REFERENCES keyless)");

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Empty(schema.Tables.Single(t => t.TableName == "refs_keyless").ForeignKeys);
    }

    [Fact]
    public async Task ExplicitReferences_AreStillReadUnchanged()
    {
        Exec("CREATE TABLE parent (id INTEGER PRIMARY KEY, code TEXT)");
        Exec("CREATE TABLE child (id INTEGER PRIMARY KEY, pid INTEGER REFERENCES parent (id))");

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions { IncludeForeignKeys = true });

        ForeignKeyInfo fk = Assert.Single(schema.Tables.Single(t => t.TableName == "child").ForeignKeys);
        Assert.Equal("id", fk.ReferencedColumn);
    }

    // ------------------------------------------------------------------
    // WITHOUT ROWID detection
    // ------------------------------------------------------------------

    /// <summary>
    /// A WITHOUT ROWID table gets no rowid aliasing, so its INTEGER PRIMARY KEY is not
    /// auto-generated. Reporting IsIdentity here would tell callers to omit a value the database
    /// will never supply. The old end-anchored pattern got both option orderings wrong.
    /// </summary>
    [Theory]
    [InlineData("CREATE TABLE t (a INTEGER PRIMARY KEY, b TEXT NOT NULL) WITHOUT ROWID")]
    [InlineData("CREATE TABLE t (a INTEGER PRIMARY KEY, b TEXT NOT NULL) STRICT, WITHOUT ROWID")]
    [InlineData("CREATE TABLE t (a INTEGER PRIMARY KEY, b TEXT NOT NULL) WITHOUT ROWID, STRICT")]
    [InlineData("CREATE TABLE t (a INTEGER PRIMARY KEY, b TEXT NOT NULL) without rowid")]
    public async Task WithoutRowIdTable_IsNeverReportedAsIdentity(string createSql)
    {
        Exec(createSql);

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions());

        ColumnSchema key = schema.Tables.Single(t => t.TableName == "t").Columns.Single(c => c.ColumnName == "a");
        Assert.True(key.IsPrimaryKey);
        Assert.False(key.IsIdentity);
    }

    [Fact]
    public async Task PlainRowIdTable_IsStillReportedAsIdentity()
    {
        Exec("CREATE TABLE t (a INTEGER PRIMARY KEY, b TEXT)");

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions());

        ColumnSchema key = schema.Tables.Single(t => t.TableName == "t").Columns.Single(c => c.ColumnName == "a");
        Assert.True(key.IsIdentity);
    }

    /// <summary>
    /// The reason the fix skips the column-definition body rather than simply relaxing the
    /// anchor: a bare <c>\bWITHOUT\s+ROWID\b</c> search over the whole statement matches this
    /// perfectly ordinary rowid table.
    /// </summary>
    [Fact]
    public async Task ColumnNamedLikeTheTableOption_DoesNotSuppressIdentity()
    {
        Exec("CREATE TABLE t (a INTEGER PRIMARY KEY, \"without rowid\" TEXT)");

        DatabaseSchema schema = await ReadAsync(new SchemaReaderOptions());

        ColumnSchema key = schema.Tables.Single(t => t.TableName == "t").Columns.Single(c => c.ColumnName == "a");
        Assert.True(key.IsIdentity);
    }

    // ------------------------------------------------------------------
    // The parser directly, on shapes that are awkward to build a table from
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("CREATE TABLE t (a INT) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) STRICT, WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT) WITHOUT ROWID, STRICT", true)]
    [InlineData("CREATE TABLE t (a INT, b INT, CHECK (b IN (1, 2, 3))) WITHOUT ROWID", true)]
    [InlineData("CREATE TABLE t (a INT)", false)]
    [InlineData("CREATE TABLE t (a INT) STRICT", false)]
    [InlineData("CREATE TABLE t (a INT, \"without rowid\" TEXT)", false)]
    [InlineData("CREATE TABLE t (a INT, `without rowid` TEXT)", false)]
    [InlineData("CREATE TABLE t (a INT, [without rowid] TEXT)", false)]
    [InlineData("CREATE TABLE t (a INT DEFAULT 'without rowid')", false)]
    [InlineData("CREATE TABLE t (a INT, \"weird)name\" TEXT) WITHOUT ROWID", true)]
    // The option named in a comment declares nothing. SQLite drops a trailing comment before
    // storing the statement in sqlite_master, so the reader never actually sees the first of
    // these; an interior comment before a real option is preserved, and is the case that matters.
    [InlineData("CREATE TABLE t (a INT) -- WITHOUT ROWID", false)]
    [InlineData("CREATE TABLE t (a INT) /* WITHOUT ROWID */", false)]
    [InlineData("CREATE TABLE t (a INT) /* note */ WITHOUT ROWID", true)]
    public void IsWithoutRowId_ReadsTheTableOptionsTailOnly(string createSql, bool expected)
        => Assert.Equal(expected, SQLiteSchemaReader.IsWithoutRowId(createSql));

    /// <summary>
    /// An escaped quote inside an identifier must not end the quoted run - if it did, the parser
    /// would fall out of the string mid-body and start counting parentheses that belong to a
    /// column name.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_HandlesDoubledQuotesInsideIdentifiers()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT, \"order''s (notes)\" TEXT)"));
        Assert.True(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT, \"order''s (notes)\" TEXT) WITHOUT ROWID"));
    }

    private async Task<DatabaseSchema> ReadAsync(SchemaReaderOptions options)
        => await new SQLiteSchemaReader().ReadSchemaAsync(_connectionString, options, TestContext.Current.CancellationToken);
}
