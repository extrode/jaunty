using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>Integration tests for Query&lt;T1,T2,T3,T4,T5&gt; — arity-5 multi-entity mapping with custom mappers.</summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityN5Tests : IClassFixture<DialectFixture>
{
    internal sealed class Author { public long Id { get; set; } }
    internal sealed class Book { public long Id { get; set; } }
    internal sealed class Chapter { public long Id { get; set; } }
    internal sealed class Section { public long Id { get; set; } }
    internal sealed class Metadata
    {
        // Intentionally keyless - uses custom mapper only
        public string Description { get; set; } = string.Empty;
    }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, @"
            CREATE TABLE multimap5_authors (id INTEGER PRIMARY KEY);
            CREATE TABLE multimap5_books (id INTEGER PRIMARY KEY, author_id INTEGER);
            CREATE TABLE multimap5_chapters (id INTEGER PRIMARY KEY, book_id INTEGER);
            CREATE TABLE multimap5_sections (id INTEGER PRIMARY KEY, chapter_id INTEGER);
            CREATE TABLE multimap5_metadata (description TEXT);
            INSERT INTO multimap5_authors VALUES (1);
            INSERT INTO multimap5_books VALUES (1, 1);
            INSERT INTO multimap5_chapters VALUES (1, 1);
            INSERT INTO multimap5_sections VALUES (1, 1);
            INSERT INTO multimap5_metadata VALUES ('A great work');");
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
    public void Query_FiveEntities_WithCustomMetadataMapper(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT a.id, b.id, b.author_id, c.id, c.book_id, s.id, s.chapter_id, m.description
            FROM multimap5_authors a
            CROSS JOIN multimap5_books b
            CROSS JOIN multimap5_chapters c
            CROSS JOIN multimap5_sections s
            CROSS JOIN multimap5_metadata m";

        // Custom mapper for Metadata (position 5) that reads description
        var options = new MultiEntityCommandOptions<Author, Book, Chapter, Section, Metadata>(
            mapper1: null,
            mapper2: null,
            mapper3: null,
            mapper4: null,
            mapper5: reader =>
            {
                var meta = new Metadata();
                meta.Description = reader["description"]?.ToString() ?? string.Empty;
                return meta;
            }
        );

        var results = connection.Query<Author, Book, Chapter, Section, Metadata>(sql, options);
        Assert.Single(results);
        var (_, _, _, _, metadata) = results[0];
        Assert.Equal("A great work", metadata.Description);
    }
}
