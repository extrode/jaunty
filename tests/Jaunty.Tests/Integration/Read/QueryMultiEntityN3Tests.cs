using System.Data.SQLite;

using Jaunty.Attributes;
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
}
