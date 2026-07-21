using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Smoke test for Query&lt;T1,T2,T3&gt; — arity-3 multi-entity mapping wired through the
/// reflection extension. Verifies ordinal-claiming and [Column] attribute disambiguation.
/// Uses an isolated in-memory SQLite database with three private tables.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityN3Tests : IClassFixture<DialectFixture>
{
    // ---------------------------------------------------------------------------
    // Local entity types — not shared with any other test.
    //
    // Both Mm3Author and Mm3Book have a property called "Name", but each binds to
    // a different SQL alias via a [Column("...")] attribute. This is the key
    // disambiguation feature being tested: [Column("author_name")] on Mm3Author.Name
    // and [Column("book_name")] on Mm3Book.Name prevent the ordinal-claiming algorithm
    // from assigning both properties to the first "name-like" column it sees.
    // ---------------------------------------------------------------------------

    internal sealed class Mm3Author
    {
        public long AuthorId { get; set; }

        [Column("author_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Mm3Book
    {
        public long BookId { get; set; }

        public long BookAuthorId { get; set; }

        [Column("book_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Mm3Tag
    {
        public long TagId { get; set; }

        public long TagBookId { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    // ---------------------------------------------------------------------------
    // Schema helpers
    // ---------------------------------------------------------------------------

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE multimap3_authors (
                author_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_name TEXT NOT NULL
            );
            CREATE TABLE multimap3_books (
                book_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_id INTEGER NOT NULL,
                book_name TEXT NOT NULL
            );
            CREATE TABLE multimap3_tags (
                tag_id INTEGER PRIMARY KEY AUTOINCREMENT,
                book_id INTEGER NOT NULL,
                label TEXT NOT NULL
            );
            INSERT INTO multimap3_authors (author_name) VALUES ('Ada Lovelace');
            INSERT INTO multimap3_books (author_id, book_name) VALUES (1, 'Notes on the Analytical Engine');
            INSERT INTO multimap3_tags (book_id, label) VALUES (1, 'history');");
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Theory]
    [SystemSqlite]
    public void Query_ThreeEntities_MapsAllTypesAndDisambiguatesColumnAttributes(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        // SQL uses unique per-type column aliases throughout.
        // author_name / book_name are resolved via [Column("...")] attributes on the
        // Name property of each entity type — demonstrating [Column] disambiguation.
        const string sql = @"
            SELECT
                a.author_id     AS AuthorId,
                a.author_name,
                b.book_id       AS BookId,
                b.author_id     AS BookAuthorId,
                b.book_name,
                t.tag_id        AS TagId,
                t.book_id       AS TagBookId,
                t.label         AS Label
            FROM multimap3_authors a
            JOIN multimap3_books  b ON b.author_id = a.author_id
            JOIN multimap3_tags   t ON t.book_id   = b.book_id";

        var results = connection.Query<Mm3Author, Mm3Book, Mm3Tag>(sql);

        Assert.Single(results);

        var (author, book, tag) = results[0];

        Assert.Equal(1L, author.AuthorId);
        Assert.Equal("Ada Lovelace", author.Name);

        Assert.Equal(1L, book.BookId);
        Assert.Equal(1L, book.BookAuthorId);
        Assert.Equal("Notes on the Analytical Engine", book.Name);

        Assert.Equal(1L, tag.TagId);
        Assert.Equal(1L, tag.TagBookId);
        Assert.Equal("history", tag.Label);
    }

    // ---------------------------------------------------------------------------
    // CommandOptions<(T1,T2,T3)> / MultiEntityCommandOptions<T1,T2,T3> overload coverage
    // (AUD-R11 batch-01: Query/QueryFirst/QuerySingle options overloads, and
    // QueryFirstOrDefault/QuerySingleOrDefault at every overload, were previously untested).
    // ---------------------------------------------------------------------------

    private const string JoinSql = @"
        SELECT
            a.author_id     AS AuthorId,
            a.author_name,
            b.book_id       AS BookId,
            b.author_id     AS BookAuthorId,
            b.book_name,
            t.tag_id        AS TagId,
            t.book_id       AS TagBookId,
            t.label         AS Label
        FROM multimap3_authors a
        JOIN multimap3_books  b ON b.author_id = a.author_id
        JOIN multimap3_tags   t ON t.book_id   = b.book_id";

    [Theory]
    [SystemSqlite]
    public void Query_ThreeEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap3_authors SET author_name = 'TXN-SENTINEL' WHERE author_id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>(transaction: txn);
        var results = connection.Query<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.Name);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_ThreeEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var results = connection.Query<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Single(results);
        Assert.Equal("Ada Lovelace", results[0].Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void Query_ThreeEntities_WithMultiEntityCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap3_authors SET author_name = 'TXN-SENTINEL' WHERE author_id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>(transaction: txn);
        var results = connection.Query<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.Name);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_ThreeEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var results = connection.Query<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Single(results);
        Assert.Equal("Ada Lovelace", results[0].Item1.Name);
    }

    // AUD-R13: QueryMultiEntityCore<T1,T2,T3..T7> hardcoded JauntyConfig.QueryResultCapacity
    // for the results list instead of honoring options.ExpectedRowCount, matching the arity-2
    // bug already fixed under AUD-R12 (see QueryMultiEntityCommandOptionsTests). This overload
    // previously could not even accept a tuple-typed CommandOptions<(T1,T2,T3)> with
    // ExpectedRowCount set, because MultiEntityCommandOptions<T1,T2,T3>'s implicit conversion
    // targeted the non-generic CommandOptions type.
    [Theory]
    [SystemSqlite]
    public void Query_ThreeEntities_WithExpectedRowCount_PreSizesListCapacity(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>.WithExpectedRowCount(500);

        var results = connection.Query<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Single(results);
        Assert.True(results.Capacity >= 500);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_ThreeEntities_WithCommandOptions_ReturnsFirstRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var (author, _, _) = connection.QueryFirst<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_ThreeEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var (author, _, tag) = connection.QueryFirst<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.Name);
        Assert.Equal("history", tag.Label);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_ThreeEntities_WithMultiEntityCommandOptions_ReturnsFirstRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var (author, _, _) = connection.QueryFirst<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_ThreeEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var (author, _, tag) = connection.QueryFirst<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.Name);
        Assert.Equal("history", tag.Label);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_ThreeEntities_WithCommandOptions_ReturnsSingleRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var (author, book, _) = connection.QuerySingle<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.Name);
        Assert.Equal("Notes on the Analytical Engine", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_ThreeEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var (author, _, _) = connection.QuerySingle<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_ThreeEntities_WithMultiEntityCommandOptions_ReturnsSingleRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var (author, book, _) = connection.QuerySingle<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.Name);
        Assert.Equal("Notes on the Analytical Engine", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_ThreeEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var (author, _, _) = connection.QuerySingle<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_ThreeEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<Mm3Author, Mm3Book, Mm3Tag>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_ThreeEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_ThreeEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var result = connection.QueryFirstOrDefault<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_ThreeEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var result = connection.QueryFirstOrDefault<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_ThreeEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var result = connection.QueryFirstOrDefault<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_ThreeEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var result = connection.QueryFirstOrDefault<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_ThreeEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<Mm3Author, Mm3Book, Mm3Tag>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_ThreeEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_ThreeEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var result = connection.QuerySingleOrDefault<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_ThreeEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm3Author, Mm3Book, Mm3Tag)>();

        var result = connection.QuerySingleOrDefault<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_ThreeEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var result = connection.QuerySingleOrDefault<Mm3Author, Mm3Book, Mm3Tag>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_ThreeEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm3Author, Mm3Book, Mm3Tag>();

        var result = connection.QuerySingleOrDefault<Mm3Author, Mm3Book, Mm3Tag>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }
}
