using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Smoke test for Query&lt;T1,T2,T3,T4,T5&gt; — arity-5 multi-entity mapping wired through the
/// reflection extension. Verifies ordinal-claiming across five types with distinct column
/// aliases. Uses an isolated in-memory SQLite database with five private tables.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityN5Tests : IClassFixture<DialectFixture>
{
    internal sealed class Mm5Author
    {
        public long AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
    }

    internal sealed class Mm5Book
    {
        public long BookId { get; set; }
        public long BookAuthorId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
    }

    internal sealed class Mm5Chapter
    {
        public long ChapterId { get; set; }
        public long ChapterBookId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
    }

    internal sealed class Mm5Section
    {
        public long SectionId { get; set; }
        public long SectionChapterId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
    }

    internal sealed class Mm5Metadata
    {
        public long MetadataId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE multimap5_authors (
                author_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_name TEXT NOT NULL
            );
            CREATE TABLE multimap5_books (
                book_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_id INTEGER NOT NULL,
                book_title TEXT NOT NULL
            );
            CREATE TABLE multimap5_chapters (
                chapter_id INTEGER PRIMARY KEY AUTOINCREMENT,
                book_id INTEGER NOT NULL,
                chapter_title TEXT NOT NULL
            );
            CREATE TABLE multimap5_sections (
                section_id INTEGER PRIMARY KEY AUTOINCREMENT,
                chapter_id INTEGER NOT NULL,
                section_title TEXT NOT NULL
            );
            CREATE TABLE multimap5_metadata (
                metadata_id INTEGER PRIMARY KEY AUTOINCREMENT,
                description TEXT NOT NULL
            );
            INSERT INTO multimap5_authors (author_name) VALUES ('Ada Lovelace');
            INSERT INTO multimap5_books (author_id, book_title) VALUES (1, 'Notes on the Analytical Engine');
            INSERT INTO multimap5_chapters (book_id, chapter_title) VALUES (1, 'Chapter One');
            INSERT INTO multimap5_sections (chapter_id, section_title) VALUES (1, 'Section One');
            INSERT INTO multimap5_metadata (description) VALUES ('A great work');");
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Theory]
    [SystemSqlite]
    public void Query_FiveEntities_MapsAllTypesViaOrdinalClaiming(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT
                a.author_id      AS AuthorId,
                a.author_name    AS AuthorName,
                b.book_id        AS BookId,
                b.author_id      AS BookAuthorId,
                b.book_title     AS BookTitle,
                c.chapter_id     AS ChapterId,
                c.book_id        AS ChapterBookId,
                c.chapter_title  AS ChapterTitle,
                s.section_id     AS SectionId,
                s.chapter_id     AS SectionChapterId,
                s.section_title  AS SectionTitle,
                m.metadata_id    AS MetadataId,
                m.description    AS Description
            FROM multimap5_authors a
            CROSS JOIN multimap5_books b
            CROSS JOIN multimap5_chapters c
            CROSS JOIN multimap5_sections s
            CROSS JOIN multimap5_metadata m";

        var results = connection.Query<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(sql);

        Assert.Single(results);
        var (author, book, chapter, section, metadata) = results[0];

        Assert.Equal(1L, author.AuthorId);
        Assert.Equal("Ada Lovelace", author.AuthorName);

        Assert.Equal(1L, book.BookId);
        Assert.Equal(1L, book.BookAuthorId);
        Assert.Equal("Notes on the Analytical Engine", book.BookTitle);

        Assert.Equal(1L, chapter.ChapterId);
        Assert.Equal(1L, chapter.ChapterBookId);
        Assert.Equal("Chapter One", chapter.ChapterTitle);

        Assert.Equal(1L, section.SectionId);
        Assert.Equal(1L, section.SectionChapterId);
        Assert.Equal("Section One", section.SectionTitle);

        Assert.Equal(1L, metadata.MetadataId);
        Assert.Equal("A great work", metadata.Description);
    }

    // ---------------------------------------------------------------------------
    // CommandOptions<(T1..T5)> / MultiEntityCommandOptions<T1..T5> overload coverage, plus
    // QueryFirst/QueryFirstOrDefault/QuerySingle/QuerySingleOrDefault/QueryStream at every
    // overload (AUD-R11 batch-01: all previously untested at arity 5).
    // ---------------------------------------------------------------------------

    private const string JoinSql = @"
            SELECT
                a.author_id      AS AuthorId,
                a.author_name    AS AuthorName,
                b.book_id        AS BookId,
                b.author_id      AS BookAuthorId,
                b.book_title     AS BookTitle,
                c.chapter_id     AS ChapterId,
                c.book_id        AS ChapterBookId,
                c.chapter_title  AS ChapterTitle,
                s.section_id     AS SectionId,
                s.chapter_id     AS SectionChapterId,
                s.section_title  AS SectionTitle,
                m.metadata_id    AS MetadataId,
                m.description    AS Description
            FROM multimap5_authors a
            CROSS JOIN multimap5_books b
            CROSS JOIN multimap5_chapters c
            CROSS JOIN multimap5_sections s
            CROSS JOIN multimap5_metadata m";

    [Theory]
    [SystemSqlite]
    public void Query_FiveEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap5_authors SET author_name = 'TXN-SENTINEL' WHERE author_id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>(transaction: txn);
        var results = connection.Query<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.AuthorName);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_FiveEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var results = connection.Query<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Single(results);
        Assert.Equal("Ada Lovelace", results[0].Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void Query_FiveEntities_WithMultiEntityCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap5_authors SET author_name = 'TXN-SENTINEL' WHERE author_id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(transaction: txn);
        var results = connection.Query<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.AuthorName);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_FiveEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var results = connection.Query<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Single(results);
        Assert.Equal("Ada Lovelace", results[0].Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FiveEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = connection.QueryFirst<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FiveEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, metadata) = connection.QueryFirst<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Ada Lovelace", author.AuthorName);
        Assert.Equal("A great work", metadata.Description);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FiveEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var (author, _, _, _, _) = connection.QueryFirst<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FiveEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var (author, _, _, _, _) = connection.QueryFirst<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FiveEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var (author, _, _, _, _) = connection.QueryFirst<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FiveEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var (author, _, _, _, _) = connection.QueryFirst<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FiveEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FiveEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FiveEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var result = connection.QueryFirstOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FiveEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var result = connection.QueryFirstOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FiveEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var result = connection.QueryFirstOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FiveEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var result = connection.QueryFirstOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FiveEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = connection.QuerySingle<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FiveEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = connection.QuerySingle<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FiveEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var (author, _, _, _, _) = connection.QuerySingle<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FiveEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var (author, _, _, _, _) = connection.QuerySingle<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FiveEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var (author, _, _, _, _) = connection.QuerySingle<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FiveEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var (author, _, _, _, _) = connection.QuerySingle<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FiveEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FiveEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FiveEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var result = connection.QuerySingleOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FiveEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var result = connection.QuerySingleOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FiveEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var result = connection.QuerySingleOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FiveEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var result = connection.QuerySingleOrDefault<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FiveEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryStream<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql).ToList();

        Assert.Single(results);
        Assert.Equal("Ada Lovelace", results[0].Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FiveEntities_WithParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryStream<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FiveEntities_WithCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var results = connection.QueryStream<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FiveEntities_WithParametersAndCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata)>();

        var results = connection.QueryStream<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FiveEntities_WithMultiEntityCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var results = connection.QueryStream<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(JoinSql, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FiveEntities_WithParametersAndMultiEntityCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>();

        var results = connection.QueryStream<Mm5Author, Mm5Book, Mm5Chapter, Mm5Section, Mm5Metadata>(
            $"{JoinSql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }, options).ToList();

        Assert.Single(results);
    }
}
