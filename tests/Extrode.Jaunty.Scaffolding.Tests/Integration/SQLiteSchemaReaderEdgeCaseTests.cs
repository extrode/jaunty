using Microsoft.Data.Sqlite;

using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Schema;

using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Integration;

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
    /// An unescaped, different quote character inside a quoted identifier is just ordinary text -
    /// no escaping question arises since it isn't the delimiter.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_ToleratesADifferentQuoteCharacterInsideAnIdentifier()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT, \"order''s (notes)\" TEXT)"));
        Assert.True(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT, \"order''s (notes)\" TEXT) WITHOUT ROWID"));
    }

    /// <summary>
    /// coverage-gaps-2026-09-20: the test above doubles a single quote inside a double-quoted
    /// identifier, which SQLite doesn't treat as an escape at all (the delimiter is what must be
    /// doubled) -- it never actually exercised the <c>createSql[i+1] == quote</c> escape branch.
    /// This doubles the identifier's own delimiter (SQLite's real escaping rule for a literal
    /// <c>"</c> inside a <c>"</c>-quoted name). If the escape check were wrong, the quoted run
    /// would end at the first embedded <c>"</c>, and the stray <c>s (notes)"</c> that follows
    /// would mis-count a paren and desynchronize depth tracking for the rest of the statement.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_HandlesADoubledDelimiterInsideAnIdentifier()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT, \"order\"\"s (notes)\" TEXT)"));
        Assert.True(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT, \"order\"\"s (notes)\" TEXT) WITHOUT ROWID"));
    }

    // ------------------------------------------------------------------
    // mutation-gaps-2026-09-21: live mutation testing showed 77 of this file's 104 survivors
    // cluster in IsWithoutRowId/TailDeclaresWithoutRowId's character-by-character scan (roughly
    // lines 276-412) - the bracket-identifier, in-body comment, paren-depth, and "sawWithout"
    // word-tracking logic were exercised by existing tests (so mutants are "Survived", not
    // "NoCoverage") but nothing asserted a result that actually depended on getting those branches
    // right. Each test below is built so a specific operator flip (&&/||, !=/==, </<=,  the initial
    // "sawWithout = false", or an off-by-one on the scan index) changes the boolean it returns.
    // ------------------------------------------------------------------

    /// <summary>
    /// A bracket-quoted identifier's content is opaque: an embedded <c>)</c> must not be seen as
    /// the real column-body close, and embedded "WITHOUT ROWID" text must not reach the tail
    /// scanner. If the bracket-skip loop (<c>while (... createSql[i] != ']') i++;</c>) under- or
    /// over-runs, the embedded <c>)</c> is treated as the real close and the text after it - here,
    /// "WITHOUT ROWID" - leaks into what the private tail scanner reads, flipping a table that
    /// never declares the option to one that appears to.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_BracketQuotedIdentifierWithEmbeddedParenAndKeyword_DoesNotLeakIntoTail()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId(
            "CREATE TABLE t (a INT, [bad)WITHOUT ROWID] TEXT)"));
        Assert.True(SQLiteSchemaReader.IsWithoutRowId(
            "CREATE TABLE t (a INT, [bad)name] TEXT) WITHOUT ROWID"));
    }

    /// <summary>
    /// A line comment inside the column-definition body (before the body's closing paren) is
    /// handled by <see cref="SQLiteSchemaReader.IsWithoutRowId"/>'s own comment check, not by
    /// <c>TailDeclaresWithoutRowId</c>'s - a distinct code path from the existing
    /// <c>-- WITHOUT ROWID</c>/<c>/* WITHOUT ROWID */</c> cases above, which only cover a comment
    /// in the table-options tail after the body has already closed. A stray <c>)</c> and
    /// "WITHOUT ROWID" text placed inside an in-body comment must not desynchronize paren depth
    /// or be seen by the tail scanner.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_LineCommentInsideTheColumnBody_SkipsParenAndKeywordsInsideIt()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId(
            "CREATE TABLE t (a INT, -- has a paren ) WITHOUT ROWID\n b INT)"));
    }

    /// <summary>
    /// The block-comment counterpart of the test above, targeting the same in-body comment check
    /// but for <c>/* ... */</c> rather than <c>--</c>.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_BlockCommentInsideTheColumnBody_SkipsParenAndKeywordsInsideIt()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId(
            "CREATE TABLE t (a INT, /* has a paren ) WITHOUT ROWID */ b INT)"));
    }

    /// <summary>
    /// Two independent, non-nested paren groups inside the body (not one nested inside the other,
    /// unlike the existing <c>CHECK (b IN (1, 2, 3))</c> case) exercise the open/close depth
    /// bookkeeping enough times in a row that an off-by-one on either branch's index advance would
    /// desynchronize which <c>)</c> is "the" body-closing one.
    /// </summary>
    [Theory]
    [InlineData("CREATE TABLE t (a INT, b INT, CHECK ((b > 0) AND (b < 100)))", false)]
    [InlineData("CREATE TABLE t (a INT, b INT, CHECK ((b > 0) AND (b < 100))) WITHOUT ROWID", true)]
    public void IsWithoutRowId_AdjacentNestedParenGroups_StillTracksDepthToTheRealClose(string createSql, bool expected)
        => Assert.Equal(expected, SQLiteSchemaReader.IsWithoutRowId(createSql));

    /// <summary>
    /// "ROWID" appearing in the tail without a preceding "WITHOUT" must not match - targets both
    /// the initial <c>sawWithout = false</c> (a mutant that starts it <c>true</c> would match on
    /// the very first "ROWID"-equal word) and the <c>sawWithout &amp;&amp; word.Equals("ROWID")</c>
    /// check itself (a mutant that turns <c>&amp;&amp;</c> into <c>||</c> would match regardless of
    /// <c>sawWithout</c>).
    /// </summary>
    [Fact]
    public void IsWithoutRowId_RowidWordWithoutAPrecedingWithoutKeyword_IsNotWithoutRowid()
    {
        Assert.False(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT) ROWID"));
        Assert.False(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT) FOO ROWID"));
    }

    /// <summary>
    /// A "WITHOUT" not immediately followed by "ROWID" must reset the tracked state rather than
    /// stay latched, so a real "WITHOUT ROWID" pair later in the tail is still found.
    /// </summary>
    [Fact]
    public void IsWithoutRowId_WithoutKeywordNotImmediatelyFollowedByRowid_ResetsAndStillFindsTheRealPair()
    {
        Assert.True(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT) WITHOUT FOO WITHOUT ROWID"));

        // The positive case above passes even under a latched flag that never clears after the
        // first WITHOUT, since a second WITHOUT ROWID follows anyway. This is what actually
        // requires the reset: FOO must clear the flag so the later, unrelated ROWID does not match.
        Assert.False(SQLiteSchemaReader.IsWithoutRowId("CREATE TABLE t (a INT) WITHOUT FOO ROWID"));
    }

    private async Task<DatabaseSchema> ReadAsync(SchemaReaderOptions options)
        => await new SQLiteSchemaReader().ReadSchemaAsync(_connectionString, options, TestContext.Current.CancellationToken);
}
