using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>Integration tests for QueryAsync&lt;T1,T2,T3&gt; — arity-3 async multi-entity mapping.</summary>
public class QueryMultiEntityAsyncN3Tests : IClassFixture<DialectFixture>
{
    internal sealed class Am3Author
    {
        public long AuthorId { get; set; }

        [Column("author_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Am3Book
    {
        public long BookId { get; set; }

        public long BookAuthorId { get; set; }

        [Column("book_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Am3Tag
    {
        public long TagId { get; set; }

        public long TagBookId { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE amultimap3_authors (
                author_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_name TEXT NOT NULL
            );
            CREATE TABLE amultimap3_books (
                book_id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_id INTEGER NOT NULL,
                book_name TEXT NOT NULL
            );
            CREATE TABLE amultimap3_tags (
                tag_id INTEGER PRIMARY KEY AUTOINCREMENT,
                book_id INTEGER NOT NULL,
                label TEXT NOT NULL
            );
            INSERT INTO amultimap3_authors (author_name) VALUES ('Ada Lovelace');
            INSERT INTO amultimap3_books (author_id, book_name) VALUES (1, 'Notes on the Analytical Engine');
            INSERT INTO amultimap3_tags (book_id, label) VALUES (1, 'history');");
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
            a.author_id     AS AuthorId,
            a.author_name,
            b.book_id       AS BookId,
            b.author_id     AS BookAuthorId,
            b.book_name,
            t.tag_id        AS TagId,
            t.book_id       AS TagBookId,
            t.label         AS Label
        FROM amultimap3_authors a
        JOIN amultimap3_books  b ON b.author_id = a.author_id
        JOIN amultimap3_tags   t ON t.book_id   = b.book_id";

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_ThreeEntities_MapsAllTypesAndDisambiguatesColumnAttributes(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<Am3Author, Am3Book, Am3Tag>(Sql);

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

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_ThreeEntities_ReturnsFirstOrNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<Am3Author, Am3Book, Am3Tag>(Sql);

        Assert.NotNull(result);
        var (author, _, tag) = result.Value;
        Assert.Equal("Ada Lovelace", author.Name);
        Assert.Equal("history", tag.Label);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_ThreeEntities_NoRows_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        Execute(connection, "DELETE FROM amultimap3_tags;");

        var result = await connection.QueryFirstOrDefaultAsync<Am3Author, Am3Book, Am3Tag>(Sql);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_ThreeEntities_MoreThanOneRow_Throws(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        Execute(connection, "INSERT INTO amultimap3_tags (book_id, label) VALUES (1, 'second-tag');");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connection.QuerySingleAsync<Am3Author, Am3Book, Am3Tag>(Sql).AsTask());
    }
}
