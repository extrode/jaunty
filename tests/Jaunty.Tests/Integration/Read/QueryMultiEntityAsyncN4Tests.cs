using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Integration tests for QueryAsync&lt;T1,T2,T3,T4&gt; — arity-4 async multi-entity mapping.
/// AUD-R11 batch-01: this arity previously had no async test file at all.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityAsyncN4Tests : IClassFixture<DialectFixture>
{
    internal sealed class A4Author
    {
        public long Id { get; set; }
        [Column("author_name")]
        public string AuthorName { get; set; } = string.Empty;
    }

    internal sealed class A4Book
    {
        public long Id { get; set; }
        public long AuthorId { get; set; }
        [Column("book_name")]
        public string BookName { get; set; } = string.Empty;
    }

    internal sealed class A4Chapter
    {
        public long Id { get; set; }
        public long BookId { get; set; }
        [Column("chapter_name")]
        public string ChapterName { get; set; } = string.Empty;
    }

    internal sealed class A4Section
    {
        public long Id { get; set; }
        public long ChapterId { get; set; }
        [Column("section_name")]
        public string SectionName { get; set; } = string.Empty;
    }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE amultimap4_authors (id INTEGER PRIMARY KEY, author_name TEXT);
            CREATE TABLE amultimap4_books (id INTEGER PRIMARY KEY, author_id INTEGER, book_name TEXT);
            CREATE TABLE amultimap4_chapters (id INTEGER PRIMARY KEY, book_id INTEGER, chapter_name TEXT);
            CREATE TABLE amultimap4_sections (id INTEGER PRIMARY KEY, chapter_id INTEGER, section_name TEXT);
            INSERT INTO amultimap4_authors VALUES (1, 'Isaac Asimov');
            INSERT INTO amultimap4_books VALUES (1, 1, 'Foundation');
            INSERT INTO amultimap4_chapters VALUES (1, 1, 'The Psychohistorians');
            INSERT INTO amultimap4_sections VALUES (1, 1, 'Introduction');");
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private const string Sql = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name, c.id, c.book_id, c.chapter_name, s.id, s.chapter_id, s.section_name
        FROM amultimap4_authors a
        JOIN amultimap4_books b ON b.author_id = a.id
        JOIN amultimap4_chapters c ON c.book_id = b.id
        JOIN amultimap4_sections s ON s.chapter_id = c.id";

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FourEntities_MapsFourTypesCorrectly(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql);

        Assert.Single(results);
        var (author, book, chapter, section) = results[0];
        Assert.Equal("Isaac Asimov", author.AuthorName);
        Assert.Equal("Foundation", book.BookName);
        Assert.Equal("The Psychohistorians", chapter.ChapterName);
        Assert.Equal("Introduction", section.SectionName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FourEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 });

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FourEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE amultimap4_authors SET author_name = 'TXN-SENTINEL' WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>(transaction: txn);
        var results = await connection.QueryAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.AuthorName);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FourEntities_WithMultiEntityCommandOptions_ReturnsMappedTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var results = await connection.QueryAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Isaac Asimov", results[0].Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var results = await connection.QueryAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var results = await connection.QueryAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FourEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _) = await connection.QueryFirstAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FourEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _) = await connection.QueryFirstAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FourEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var (author, _, _, _) = await connection.QueryFirstAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FourEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var (author, _, _, _) = await connection.QueryFirstAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var (author, _, _, _) = await connection.QueryFirstAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var (author, _, _, _) = await connection.QueryFirstAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FourEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FourEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FourEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var result = await connection.QueryFirstOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FourEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var result = await connection.QueryFirstOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var result = await connection.QueryFirstOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var result = await connection.QueryFirstOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FourEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _) = await connection.QuerySingleAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FourEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _) = await connection.QuerySingleAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FourEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var (author, _, _, _) = await connection.QuerySingleAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FourEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var (author, _, _, _) = await connection.QuerySingleAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var (author, _, _, _) = await connection.QuerySingleAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var (author, _, _, _) = await connection.QuerySingleAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.Equal("Isaac Asimov", author.AuthorName);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FourEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QuerySingleOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FourEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QuerySingleOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FourEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var result = await connection.QuerySingleOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FourEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var result = await connection.QuerySingleOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var result = await connection.QuerySingleOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var result = await connection.QuerySingleOrDefaultAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FourEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(A4Author, A4Book, A4Chapter, A4Section)>();
        await foreach (var row in connection.QueryStreamAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FourEntities_WithParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(A4Author, A4Book, A4Chapter, A4Section)>();
        await foreach (var row in connection.QueryStreamAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FourEntities_WithCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var seen = new List<(A4Author, A4Book, A4Chapter, A4Section)>();
        await foreach (var row in connection.QueryStreamAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FourEntities_WithMultiEntityCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var seen = new List<(A4Author, A4Book, A4Chapter, A4Section)>();
        await foreach (var row in connection.QueryStreamAsync<A4Author, A4Book, A4Chapter, A4Section>(Sql, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(A4Author, A4Book, A4Chapter, A4Section)>();

        var seen = new List<(A4Author, A4Book, A4Chapter, A4Section)>();
        await foreach (var row in connection.QueryStreamAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<A4Author, A4Book, A4Chapter, A4Section>();

        var seen = new List<(A4Author, A4Book, A4Chapter, A4Section)>();
        await foreach (var row in connection.QueryStreamAsync<A4Author, A4Book, A4Chapter, A4Section>(
            $"{Sql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }
}
