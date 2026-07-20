using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Integration tests for QueryAsync&lt;T1,T2,T3,T4,T5&gt; — arity-5 async multi-entity mapping.
/// AUD-R11 batch-01: this arity previously had no async test file at all.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityAsyncN5Tests : IClassFixture<DialectFixture>
{
    internal sealed class A5Author
    {
        public long AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
    }

    internal sealed class A5Book
    {
        public long BookId { get; set; }
        public long BookAuthorId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
    }

    internal sealed class A5Chapter
    {
        public long ChapterId { get; set; }
        public long ChapterBookId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
    }

    internal sealed class A5Section
    {
        public long SectionId { get; set; }
        public long SectionChapterId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
    }

    internal sealed class A5Metadata
    {
        public long MetadataId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE amultimap5_authors (
                author_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_name TEXT NOT NULL
            );
            CREATE TABLE amultimap5_books (
                book_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_id INTEGER NOT NULL,
                book_title TEXT NOT NULL
            );
            CREATE TABLE amultimap5_chapters (
                chapter_id INTEGER PRIMARY KEY AUTOINCREMENT,
                book_id INTEGER NOT NULL,
                chapter_title TEXT NOT NULL
            );
            CREATE TABLE amultimap5_sections (
                section_id INTEGER PRIMARY KEY AUTOINCREMENT,
                chapter_id INTEGER NOT NULL,
                section_title TEXT NOT NULL
            );
            CREATE TABLE amultimap5_metadata (
                metadata_id INTEGER PRIMARY KEY AUTOINCREMENT,
                description TEXT NOT NULL
            );
            INSERT INTO amultimap5_authors (author_name) VALUES ('Ada Lovelace');
            INSERT INTO amultimap5_books (author_id, book_title) VALUES (1, 'Notes on the Analytical Engine');
            INSERT INTO amultimap5_chapters (book_id, chapter_title) VALUES (1, 'Chapter One');
            INSERT INTO amultimap5_sections (chapter_id, section_title) VALUES (1, 'Section One');
            INSERT INTO amultimap5_metadata (description) VALUES ('A great work');");
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private const string Sql = @"
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
        FROM amultimap5_authors a
        CROSS JOIN amultimap5_books b
        CROSS JOIN amultimap5_chapters c
        CROSS JOIN amultimap5_sections s
        CROSS JOIN amultimap5_metadata m";

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FiveEntities_MapsAllTypesViaOrdinalClaiming(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql);

        Assert.Single(results);
        var (author, book, chapter, section, metadata) = results[0];
        Assert.Equal("Ada Lovelace", author.AuthorName);
        Assert.Equal("Notes on the Analytical Engine", book.BookTitle);
        Assert.Equal("Chapter One", chapter.ChapterTitle);
        Assert.Equal("Section One", section.SectionTitle);
        Assert.Equal("A great work", metadata.Description);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FiveEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(
            $"{Sql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 });

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FiveEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE amultimap5_authors SET author_name = 'TXN-SENTINEL' WHERE author_id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>(transaction: txn);
        var results = await connection.QueryAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.AuthorName);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FiveEntities_WithMultiEntityCommandOptions_ReturnsMappedTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>();

        var results = await connection.QueryAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Ada Lovelace", results[0].Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FiveEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = await connection.QueryFirstAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FiveEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = await connection.QueryFirstAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(
            $"{Sql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FiveEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();

        var (author, _, _, _, _) = await connection.QueryFirstAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FiveEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>();

        var (author, _, _, _, _) = await connection.QueryFirstAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FiveEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql);

        Assert.NotNull(result);
        Assert.Equal("Ada Lovelace", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FiveEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(
            $"{Sql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FiveEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();

        var result = await connection.QueryFirstOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FiveEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>();

        var result = await connection.QueryFirstOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FiveEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = await connection.QuerySingleAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FiveEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _, _) = await connection.QuerySingleAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(
            $"{Sql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FiveEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();

        var (author, _, _, _, _) = await connection.QuerySingleAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FiveEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>();

        var (author, _, _, _, _) = await connection.QuerySingleAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.Equal("Ada Lovelace", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FiveEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QuerySingleOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FiveEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QuerySingleOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(
            $"{Sql} WHERE a.author_id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FiveEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();

        var result = await connection.QuerySingleOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FiveEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>();

        var result = await connection.QuerySingleOrDefaultAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FiveEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();
        await foreach (var row in connection.QueryStreamAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FiveEntities_WithParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();
        await foreach (var row in connection.QueryStreamAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(
            $"{Sql} WHERE a.author_id = @AuthorId", new { AuthorId = 1 }))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FiveEntities_WithCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();

        var seen = new List<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();
        await foreach (var row in connection.QueryStreamAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FiveEntities_WithMultiEntityCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>();

        var seen = new List<(A5Author, A5Book, A5Chapter, A5Section, A5Metadata)>();
        await foreach (var row in connection.QueryStreamAsync<A5Author, A5Book, A5Chapter, A5Section, A5Metadata>(Sql, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }
}
