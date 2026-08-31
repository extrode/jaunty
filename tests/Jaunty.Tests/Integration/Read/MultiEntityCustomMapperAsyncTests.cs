using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R34-001. The AUD-R33-004 fix - dispatch to the single-entity core when
/// <c>options.Mapper</c> is set - landed on <c>QueryCore.cs</c> only. <c>QueryCoreAsync.cs</c> had no
/// such branch at any arity, so the async half of the same public API went on discarding the mapper
/// and running the reflection mapping.
/// <para>
/// The round-33 regression test could not have caught it: it is sync-only and arity-2 only. These
/// are its async counterparts, plus arity 4 to show the branch was added across the arities rather
/// than at the one the test happens to use. The mapper uppercases, so a regression shows up as the
/// raw column values rather than an exception - "the query returned rows" is not evidence here
/// either.
/// </para>
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class MultiEntityCustomMapperAsyncTests : IClassFixture<DialectFixture>
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

    internal sealed class Publisher
    {
        public long Id { get; set; }
        [Column("publisher_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Series
    {
        public long Id { get; set; }
        [Column("series_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Translator
    {
        public long Id { get; set; }
        [Column("translator_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Editor
    {
        public long Id { get; set; }
        [Column("editor_name")]
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Imprint
    {
        public long Id { get; set; }
        [Column("imprint_name")]
        public string Name { get; set; } = string.Empty;
    }

    private const string Sql = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        ORDER BY b.id";

    private const string Sql4 = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name,
               p.id, p.publisher_name, s.id, s.series_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        JOIN custommap_publishers p ON p.id = 1
        JOIN custommap_series s ON s.id = 1
        ORDER BY b.id";

    private const string Sql3 = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name,
               p.id, p.publisher_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        JOIN custommap_publishers p ON p.id = 1
        ORDER BY b.id";

    private const string Sql5 = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name,
               p.id, p.publisher_name, s.id, s.series_name,
               t.id, t.translator_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        JOIN custommap_publishers p ON p.id = 1
        JOIN custommap_series s ON s.id = 1
        JOIN custommap_translators t ON t.id = 1
        ORDER BY b.id";

    private const string Sql6 = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name,
               p.id, p.publisher_name, s.id, s.series_name,
               t.id, t.translator_name, e.id, e.editor_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        JOIN custommap_publishers p ON p.id = 1
        JOIN custommap_series s ON s.id = 1
        JOIN custommap_translators t ON t.id = 1
        JOIN custommap_editors e ON e.id = 1
        ORDER BY b.id";

    private const string Sql7 = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name,
               p.id, p.publisher_name, s.id, s.series_name,
               t.id, t.translator_name, e.id, e.editor_name,
               i.id, i.imprint_name
        FROM custommap_authors a
        JOIN custommap_books b ON b.author_id = a.id
        JOIN custommap_publishers p ON p.id = 1
        JOIN custommap_series s ON s.id = 1
        JOIN custommap_translators t ON t.id = 1
        JOIN custommap_editors e ON e.id = 1
        JOIN custommap_imprints i ON i.id = 1
        ORDER BY b.id";

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE custommap_authors (id INTEGER PRIMARY KEY, author_name TEXT);
            CREATE TABLE custommap_books (id INTEGER PRIMARY KEY, author_id INTEGER, book_name TEXT);
            CREATE TABLE custommap_publishers (id INTEGER PRIMARY KEY, publisher_name TEXT);
            CREATE TABLE custommap_series (id INTEGER PRIMARY KEY, series_name TEXT);
            CREATE TABLE custommap_translators (id INTEGER PRIMARY KEY, translator_name TEXT);
            CREATE TABLE custommap_editors (id INTEGER PRIMARY KEY, editor_name TEXT);
            CREATE TABLE custommap_imprints (id INTEGER PRIMARY KEY, imprint_name TEXT);
            INSERT INTO custommap_translators VALUES (1, 'Jean Rosenthal');
            INSERT INTO custommap_editors VALUES (1, 'John Campbell');
            INSERT INTO custommap_imprints VALUES (1, 'Panther Books');
            INSERT INTO custommap_authors VALUES (1, 'Isaac Asimov');
            INSERT INTO custommap_books VALUES (1, 1, 'Foundation');
            INSERT INTO custommap_books VALUES (2, 1, 'Robots');
            INSERT INTO custommap_publishers VALUES (1, 'Gnome Press');
            INSERT INTO custommap_series VALUES (1, 'Foundation Saga');";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static CommandOptions<(Author, Book)> ShoutingMapper() =>
        CommandOptions<(Author, Book)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() }));

    private static CommandOptions<(Author, Book, Publisher, Series)> ShoutingMapper4() =>
        CommandOptions<(Author, Book, Publisher, Series)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() },
            new Publisher { Id = reader.GetInt64(5), Name = reader.GetString(6).ToUpperInvariant() },
            new Series { Id = reader.GetInt64(7), Name = reader.GetString(8).ToUpperInvariant() }));

    private static CommandOptions<(Author, Book, Publisher)> ShoutingMapper3() =>
        CommandOptions<(Author, Book, Publisher)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() },
            new Publisher { Id = reader.GetInt64(5), Name = reader.GetString(6).ToUpperInvariant() }));

    private static CommandOptions<(Author, Book, Publisher, Series, Translator)> ShoutingMapper5() =>
        CommandOptions<(Author, Book, Publisher, Series, Translator)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() },
            new Publisher { Id = reader.GetInt64(5), Name = reader.GetString(6).ToUpperInvariant() },
            new Series { Id = reader.GetInt64(7), Name = reader.GetString(8).ToUpperInvariant() },
            new Translator { Id = reader.GetInt64(9), Name = reader.GetString(10).ToUpperInvariant() }));

    private static CommandOptions<(Author, Book, Publisher, Series, Translator, Editor)> ShoutingMapper6() =>
        CommandOptions<(Author, Book, Publisher, Series, Translator, Editor)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() },
            new Publisher { Id = reader.GetInt64(5), Name = reader.GetString(6).ToUpperInvariant() },
            new Series { Id = reader.GetInt64(7), Name = reader.GetString(8).ToUpperInvariant() },
            new Translator { Id = reader.GetInt64(9), Name = reader.GetString(10).ToUpperInvariant() },
            new Editor { Id = reader.GetInt64(11), Name = reader.GetString(12).ToUpperInvariant() }));

    private static CommandOptions<(Author, Book, Publisher, Series, Translator, Editor, Imprint)> ShoutingMapper7() =>
        CommandOptions<(Author, Book, Publisher, Series, Translator, Editor, Imprint)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() },
            new Publisher { Id = reader.GetInt64(5), Name = reader.GetString(6).ToUpperInvariant() },
            new Series { Id = reader.GetInt64(7), Name = reader.GetString(8).ToUpperInvariant() },
            new Translator { Id = reader.GetInt64(9), Name = reader.GetString(10).ToUpperInvariant() },
            new Editor { Id = reader.GetInt64(11), Name = reader.GetString(12).ToUpperInvariant() },
            new Imprint { Id = reader.GetInt64(13), Name = reader.GetString(14).ToUpperInvariant() }));

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_WithCustomMapper_UsesItRatherThanTheReflectionMapping(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book)> results = await connection.QueryAsync<Author, Book>(Sql, ShoutingMapper());

        Assert.Equal(2, results.Count);
        Assert.Equal("ISAAC ASIMOV", results[0].Item1.Name);
        Assert.Equal("FOUNDATION", results[0].Item2.Name);
        Assert.Equal("ROBOTS", results[1].Item2.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author author, Book book) = await connection.QueryFirstAsync<Author, Book>(Sql, ShoutingMapper());

        Assert.Equal("ISAAC ASIMOV", author.Name);
        Assert.Equal("FOUNDATION", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = await connection.QueryFirstOrDefaultAsync<Author, Book>(Sql, ShoutingMapper());

        Assert.NotNull(result);
        Assert.Equal("ISAAC ASIMOV", result!.Value.Item1.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author author, Book book) = await connection.QuerySingleAsync<Author, Book>($"{Sql} LIMIT 1", ShoutingMapper());

        Assert.Equal("ISAAC ASIMOV", author.Name);
        Assert.Equal("FOUNDATION", book.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = await connection.QuerySingleOrDefaultAsync<Author, Book>($"{Sql} LIMIT 1", ShoutingMapper());

        Assert.NotNull(result);
        Assert.Equal("FOUNDATION", result!.Value.Item2.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var streamed = new List<(Author, Book)>();
        await foreach ((Author, Book) row in connection.QueryStreamAsync<Author, Book>(Sql, ShoutingMapper()))
            streamed.Add(row);

        Assert.Equal(2, streamed.Count);
        Assert.Equal("ISAAC ASIMOV", streamed[0].Item1.Name);
        Assert.Equal("ROBOTS", streamed[1].Item2.Name);
    }

    /// <summary>
    /// Arity 4, because the defect was per-core and a fix applied only where the arity-2 test looks
    /// would leave the other five arities exactly as they were.
    /// </summary>
    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_Arity4_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book, Publisher, Series)> results =
            await connection.QueryAsync<Author, Book, Publisher, Series>(Sql4, ShoutingMapper4());

        Assert.Equal(2, results.Count);
        Assert.Equal("ISAAC ASIMOV", results[0].Item1.Name);
        Assert.Equal("GNOME PRESS", results[0].Item3.Name);
        Assert.Equal("FOUNDATION SAGA", results[0].Item4.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_Arity4_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author author, Book book, Publisher publisher, Series series) =
            await connection.QueryFirstAsync<Author, Book, Publisher, Series>(Sql4, ShoutingMapper4());

        Assert.Equal("ISAAC ASIMOV", author.Name);
        Assert.Equal("FOUNDATION", book.Name);
        Assert.Equal("GNOME PRESS", publisher.Name);
        Assert.Equal("FOUNDATION SAGA", series.Name);
    }

    /// <summary>
    /// AUD-R35-125. The AUD-R34-001 delegation branch went into twenty-four cores and was exercised
    /// at two arities. These reach the four that no test supplied a mapper to, and take
    /// <c>QuerySingleOrDefaultAsync</c> and <c>QueryStreamAsync</c> above arity 2 - the two entry
    /// points whose mapper handling was arity-2-only. The mapper uppercases, so a core that quietly
    /// dropped it returns rows with the raw column values rather than throwing.
    /// </summary>
    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_Arity3_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book, Publisher)> results =
            await connection.QueryAsync<Author, Book, Publisher>(Sql3, ShoutingMapper3());

        Assert.Equal(2, results.Count);
        Assert.Equal("ISAAC ASIMOV", results[0].Item1.Name);
        Assert.Equal("FOUNDATION", results[0].Item2.Name);
        Assert.Equal("GNOME PRESS", results[0].Item3.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_Arity5_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book, Publisher, Series, Translator)> results =
            await connection.QueryAsync<Author, Book, Publisher, Series, Translator>(Sql5, ShoutingMapper5());

        Assert.Equal(2, results.Count);
        Assert.Equal("ISAAC ASIMOV", results[0].Item1.Name);
        Assert.Equal("JEAN ROSENTHAL", results[0].Item5.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_Arity6_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book, Publisher, Series, Translator, Editor)> results =
            await connection.QueryAsync<Author, Book, Publisher, Series, Translator, Editor>(Sql6, ShoutingMapper6());

        Assert.Equal(2, results.Count);
        Assert.Equal("FOUNDATION SAGA", results[0].Item4.Name);
        Assert.Equal("JOHN CAMPBELL", results[0].Item6.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_Arity7_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        List<(Author, Book, Publisher, Series, Translator, Editor, Imprint)> results =
            await connection.QueryAsync<Author, Book, Publisher, Series, Translator, Editor, Imprint>(Sql7, ShoutingMapper7());

        Assert.Equal(2, results.Count);
        Assert.Equal("JOHN CAMPBELL", results[0].Item6.Name);
        Assert.Equal("PANTHER BOOKS", results[0].Item7.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_Arity7_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book, Publisher, Series, Translator, Editor, Imprint)? result =
            await connection.QueryFirstOrDefaultAsync<Author, Book, Publisher, Series, Translator, Editor, Imprint>(Sql7, ShoutingMapper7());

        Assert.NotNull(result);
        Assert.Equal("ISAAC ASIMOV", result!.Value.Item1.Name);
        Assert.Equal("PANTHER BOOKS", result.Value.Item7.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_Arity3_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book, Publisher)? result =
            await connection.QuerySingleOrDefaultAsync<Author, Book, Publisher>($"{Sql3} LIMIT 1", ShoutingMapper3());

        Assert.NotNull(result);
        Assert.Equal("FOUNDATION", result!.Value.Item2.Name);
        Assert.Equal("GNOME PRESS", result.Value.Item3.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_Arity5_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author author, Book book, Publisher publisher, Series series, Translator translator) =
            await connection.QuerySingleAsync<Author, Book, Publisher, Series, Translator>($"{Sql5} LIMIT 1", ShoutingMapper5());

        Assert.Equal("ISAAC ASIMOV", author.Name);
        Assert.Equal("FOUNDATION", book.Name);
        Assert.Equal("GNOME PRESS", publisher.Name);
        Assert.Equal("FOUNDATION SAGA", series.Name);
        Assert.Equal("JEAN ROSENTHAL", translator.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_Arity3_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var streamed = new List<(Author, Book, Publisher)>();
        await foreach ((Author, Book, Publisher) row in connection.QueryStreamAsync<Author, Book, Publisher>(Sql3, ShoutingMapper3()))
            streamed.Add(row);

        Assert.Equal(2, streamed.Count);
        Assert.Equal("ISAAC ASIMOV", streamed[0].Item1.Name);
        Assert.Equal("ROBOTS", streamed[1].Item2.Name);
        Assert.Equal("GNOME PRESS", streamed[1].Item3.Name);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_Arity6_WithCustomMapper_UsesIt(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var streamed = new List<(Author, Book, Publisher, Series, Translator, Editor)>();
        await foreach ((Author, Book, Publisher, Series, Translator, Editor) row in
            connection.QueryStreamAsync<Author, Book, Publisher, Series, Translator, Editor>(Sql6, ShoutingMapper6()))
        {
            streamed.Add(row);
        }

        Assert.Equal(2, streamed.Count);
        Assert.Equal("JOHN CAMPBELL", streamed[0].Item6.Name);
        Assert.Equal("ROBOTS", streamed[1].Item2.Name);
    }

    /// <summary>
    /// The control: with no mapper the reflection mapping must still run, so the new branch cannot
    /// have been entered for every caller.
    /// </summary>
    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_WithoutCustomMapper_StillUsesTheReflectionMapping(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        // The options overload with a default struct, not the bare (sql) one - that call is
        // ambiguous between the CancellationToken and legacy CommandOptions overloads.
        List<(Author, Book)> results =
            await connection.QueryAsync<Author, Book>(Sql, default(CommandOptions<(Author, Book)>));

        Assert.Equal(2, results.Count);
        Assert.Equal("Isaac Asimov", results[0].Item1.Name);
        Assert.Equal("Foundation", results[0].Item2.Name);
    }
}
