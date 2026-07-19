using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
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
}
