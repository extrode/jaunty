using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R33-004. Every multi-entity core built its mapping from
/// <c>MultiEntityMapper&lt;...&gt;.Build(reader)</c> and never looked at <c>options.Mapper</c>, so a
/// mapper supplied through <see cref="CommandOptions{T}.WithMapper"/> - which the public overloads
/// accept and their XML docs advertise - was silently discarded and the reflection mapping ran
/// instead. Same failure mode as AUD-R26's scalar <c>Mapper</c>.
/// <para>
/// The mapper here deliberately produces values the reflection mapper cannot: it uppercases the
/// names and swaps nothing else. If the fix regresses, the assertions see the raw column values
/// rather than an exception, which is exactly why "the query returned rows" was never evidence.
/// </para>
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class MultiEntityCustomMapperTests : IClassFixture<DialectFixture>
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

    private const string Sql = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        ORDER BY b.id";

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE custommap_authors (id INTEGER PRIMARY KEY, author_name TEXT);
            CREATE TABLE custommap_books (id INTEGER PRIMARY KEY, author_id INTEGER, book_name TEXT);
            INSERT INTO custommap_authors VALUES (1, 'Isaac Asimov');
            INSERT INTO custommap_books VALUES (1, 1, 'Foundation');
            INSERT INTO custommap_books VALUES (2, 1, 'Robots');";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static CommandOptions<(Author, Book)> ShoutingMapper() =>
        CommandOptions<(Author, Book)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() }));

    [Theory]
    [SystemSqlite]
    public void Query_WithCustomMapper_UsesItRatherThanTheReflectionMapping(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book)> results = connection.Query<Author, Book>(Sql, ShoutingMapper());

        Assert.Equal(2, results.Count);
        Assert.Equal("ISAAC ASIMOV", results[0].Item1.Name);
        Assert.Equal("FOUNDATION", results[0].Item2.Name);
        Assert.Equal("ROBOTS", results[1].Item2.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author author, Book book) = connection.QueryFirst<Author, Book>(Sql, ShoutingMapper());

        Assert.Equal("ISAAC ASIMOV", author.Name);
        Assert.Equal("FOUNDATION", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = connection.QueryFirstOrDefault<Author, Book>(Sql, ShoutingMapper());

        Assert.NotNull(result);
        Assert.Equal("ISAAC ASIMOV", result!.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author author, Book book) = connection.QuerySingle<Author, Book>($"{Sql} LIMIT 1", ShoutingMapper());

        Assert.Equal("ISAAC ASIMOV", author.Name);
        Assert.Equal("FOUNDATION", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = connection.QuerySingleOrDefault<Author, Book>($"{Sql} LIMIT 1", ShoutingMapper());

        Assert.NotNull(result);
        Assert.Equal("FOUNDATION", result!.Value.Item2.Name);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book)> streamed = connection.QueryStream<Author, Book>(Sql, ShoutingMapper()).ToList();

        Assert.Equal(2, streamed.Count);
        Assert.Equal("ISAAC ASIMOV", streamed[0].Item1.Name);
        Assert.Equal("ROBOTS", streamed[1].Item2.Name);
    }

    /// <summary>
    /// Without a mapper the reflection mapping must still be what runs - this is the case that
    /// would break every existing multi-entity caller if the new branch were entered wrongly.
    /// </summary>
    [Theory]
    [SystemSqlite]
    public void Query_WithoutCustomMapper_StillUsesTheReflectionMapping(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book)> results = connection.Query<Author, Book>(Sql);

        Assert.Equal(2, results.Count);
        Assert.Equal("Isaac Asimov", results[0].Item1.Name);
        Assert.Equal("Foundation", results[0].Item2.Name);
    }
}
