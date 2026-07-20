using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>Integration tests for Query&lt;T1,T2,T3,T4&gt; — arity-4 multi-entity mapping.</summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityN4Tests : IClassFixture<DialectFixture>
{
    internal sealed class Author
    {
        public long Id { get; set; }
        [Column("author_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Book
    {
        public long Id { get; set; }
        public long AuthorId { get; set; }
        [Column("book_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Chapter
    {
        public long Id { get; set; }
        public long BookId { get; set; }
        [Column("chapter_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Section
    {
        public long Id { get; set; }
        public long ChapterId { get; set; }
        [Column("section_name")]
        public string Name { get; set; } = string.Empty;
    }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE multimap4_authors (id INTEGER PRIMARY KEY, author_name TEXT);
            CREATE TABLE multimap4_books (id INTEGER PRIMARY KEY, author_id INTEGER, book_name TEXT);
            CREATE TABLE multimap4_chapters (id INTEGER PRIMARY KEY, book_id INTEGER, chapter_name TEXT);
            CREATE TABLE multimap4_sections (id INTEGER PRIMARY KEY, chapter_id INTEGER, section_name TEXT);
            INSERT INTO multimap4_authors VALUES (1, 'Isaac Asimov');
            INSERT INTO multimap4_books VALUES (1, 1, 'Foundation');
            INSERT INTO multimap4_chapters VALUES (1, 1, 'The Psychohistorians');
            INSERT INTO multimap4_sections VALUES (1, 1, 'Introduction');");
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
    public void Query_FourEntities_MapsFourTypesCorrectly(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT a.id, a.author_name, b.id, b.author_id, b.book_name, c.id, c.book_id, c.chapter_name, s.id, s.chapter_id, s.section_name
            FROM multimap4_authors a
            JOIN multimap4_books b ON b.author_id = a.id
            JOIN multimap4_chapters c ON c.book_id = b.id
            JOIN multimap4_sections s ON s.chapter_id = c.id";

        var results = connection.Query<Author, Book, Chapter, Section>(sql);
        Assert.Single(results);
        var (author, book, chapter, section) = results[0];
        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Foundation", book.Name);
        Assert.Equal("The Psychohistorians", chapter.Name);
        Assert.Equal("Introduction", section.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FourEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT a.id, a.author_name, b.id, b.author_id, b.book_name, c.id, c.book_id, c.chapter_name, s.id, s.chapter_id, s.section_name
            FROM multimap4_authors a
            JOIN multimap4_books b ON b.author_id = a.id
            JOIN multimap4_chapters c ON c.book_id = b.id
            JOIN multimap4_sections s ON s.chapter_id = c.id";

        var (author, book, chapter, section) = connection.QueryFirst<Author, Book, Chapter, Section>(sql);
        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Foundation", book.Name);
        Assert.Equal("The Psychohistorians", chapter.Name);
        Assert.Equal("Introduction", section.Name);
    }

    // ---------------------------------------------------------------------------
    // CommandOptions<(T1..T4)> / MultiEntityCommandOptions<T1..T4> overload coverage, plus
    // QuerySingle/QueryFirstOrDefault/QuerySingleOrDefault/QueryStream at every overload
    // (AUD-R11 batch-01: all previously untested at arity 4).
    // ---------------------------------------------------------------------------

    private const string JoinSql = @"
            SELECT a.id, a.author_name, b.id, b.author_id, b.book_name, c.id, c.book_id, c.chapter_name, s.id, s.chapter_id, s.section_name
            FROM multimap4_authors a
            JOIN multimap4_books b ON b.author_id = a.id
            JOIN multimap4_chapters c ON c.book_id = b.id
            JOIN multimap4_sections s ON s.chapter_id = c.id";

    [Theory]
    [SystemSqlite]
    public void Query_FourEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap4_authors SET author_name = 'TXN-SENTINEL' WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(Author, Book, Chapter, Section)>(transaction: txn);
        var results = connection.Query<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.Name);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var results = connection.Query<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Single(results);
        Assert.Equal("Isaac Asimov", results[0].Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void Query_FourEntities_WithMultiEntityCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap4_authors SET author_name = 'TXN-SENTINEL' WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>(transaction: txn);
        var results = connection.Query<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.Name);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var results = connection.Query<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Single(results);
        Assert.Equal("Isaac Asimov", results[0].Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FourEntities_WithCommandOptions_ReturnsFirstRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var (author, _, _, _) = connection.QueryFirst<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.Equal("Isaac Asimov", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var (author, _, _, section) = connection.QueryFirst<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Introduction", section.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FourEntities_WithMultiEntityCommandOptions_ReturnsFirstRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var (author, _, _, _) = connection.QueryFirst<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.Equal("Isaac Asimov", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var (author, _, _, section) = connection.QueryFirst<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Introduction", section.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FourEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, book, chapter, section) = connection.QuerySingle<Author, Book, Chapter, Section>(JoinSql);

        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Foundation", book.Name);
        Assert.Equal("The Psychohistorians", chapter.Name);
        Assert.Equal("Introduction", section.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FourEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (author, _, _, _) = connection.QuerySingle<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 });

        Assert.Equal("Isaac Asimov", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FourEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var (author, book, _, _) = connection.QuerySingle<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Foundation", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FourEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var (author, _, _, _) = connection.QuerySingle<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Isaac Asimov", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FourEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var (author, book, _, _) = connection.QuerySingle<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.Equal("Isaac Asimov", author.Name);
        Assert.Equal("Foundation", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var (author, _, _, _) = connection.QuerySingle<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options);

        Assert.Equal("Isaac Asimov", author.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FourEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<Author, Book, Chapter, Section>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FourEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FourEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var result = connection.QueryFirstOrDefault<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FourEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var result = connection.QueryFirstOrDefault<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FourEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var result = connection.QueryFirstOrDefault<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_FourEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var result = connection.QueryFirstOrDefault<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FourEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<Author, Book, Chapter, Section>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FourEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FourEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var result = connection.QuerySingleOrDefault<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FourEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var result = connection.QuerySingleOrDefault<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FourEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var result = connection.QuerySingleOrDefault<Author, Book, Chapter, Section>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal("Isaac Asimov", result.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_FourEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var result = connection.QuerySingleOrDefault<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FourEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryStream<Author, Book, Chapter, Section>(JoinSql).ToList();

        Assert.Single(results);
        Assert.Equal("Isaac Asimov", results[0].Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FourEntities_WithParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryStream<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FourEntities_WithCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var results = connection.QueryStream<Author, Book, Chapter, Section>(JoinSql, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FourEntities_WithParametersAndCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Author, Book, Chapter, Section)>();

        var results = connection.QueryStream<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FourEntities_WithMultiEntityCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var results = connection.QueryStream<Author, Book, Chapter, Section>(JoinSql, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_FourEntities_WithParametersAndMultiEntityCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section>();

        var results = connection.QueryStream<Author, Book, Chapter, Section>(
            $"{JoinSql} WHERE a.id = @AuthorId", new { AuthorId = 1 }, options).ToList();

        Assert.Single(results);
    }
}
