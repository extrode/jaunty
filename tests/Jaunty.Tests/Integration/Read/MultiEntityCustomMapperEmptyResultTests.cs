using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R34-004. The AUD-R33-004 / AUD-R34-001 fixes route a multi-entity call with
/// <c>options.Mapper</c> set through the single-entity <c>QueryFirstOrDefaultCore</c> /
/// <c>QuerySingleOrDefaultCore</c>, whose return type is <c>T?</c> over a type parameter constrained
/// only by <c>new()</c>. At runtime that <c>T</c> is a plain <c>ValueTuple</c>, so an empty result
/// set returned <c>default</c> - a tuple of nulls - and the implicit conversion to
/// <c>(T1, T2)?</c> wrapped it with <c>HasValue == true</c>. The `OrDefault` methods therefore
/// returned a non-null tuple of nulls instead of <c>null</c>, and the `null` guard in
/// <c>QueryFirst</c>/<c>QuerySingle</c> never fired, so those returned <c>(null, null)</c> instead
/// of throwing. The same public API had two different empty-result contracts depending on whether a
/// mapper was supplied.
/// <para>
/// The mapper-honouring tests could not have caught it - they all query a seeded, non-empty table.
/// Every case here is the empty one, and each has a no-mapper control proving the two paths agree.
/// </para>
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class MultiEntityCustomMapperEmptyResultTests : IClassFixture<DialectFixture>
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

    private const string EmptySql = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name
        FROM emptymap_authors a
        JOIN emptymap_books b ON b.author_id = a.id
        WHERE a.id = -1";

    private const string TwoRowSql = @"
        SELECT a.id, a.author_name, b.id, b.author_id, b.book_name
        FROM emptymap_authors a
        JOIN emptymap_books b ON b.author_id = a.id
        ORDER BY b.id";

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE emptymap_authors (id INTEGER PRIMARY KEY, author_name TEXT);
            CREATE TABLE emptymap_books (id INTEGER PRIMARY KEY, author_id INTEGER, book_name TEXT);
            INSERT INTO emptymap_authors VALUES (1, 'Isaac Asimov');
            INSERT INTO emptymap_books VALUES (1, 1, 'Foundation');
            INSERT INTO emptymap_books VALUES (2, 1, 'Robots');";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static CommandOptions<(Author, Book)> ShoutingMapper() =>
        CommandOptions<(Author, Book)>.WithMapper(reader => (
            new Author { Id = reader.GetInt64(0), Name = reader.GetString(1).ToUpperInvariant() },
            new Book { Id = reader.GetInt64(2), AuthorId = reader.GetInt64(3), Name = reader.GetString(4).ToUpperInvariant() }));

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_WithMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = connection.QueryFirstOrDefault<Author, Book>(EmptySql, ShoutingMapper());

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_WithoutMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = connection.QueryFirstOrDefault<Author, Book>(EmptySql);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_WithMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = connection.QuerySingleOrDefault<Author, Book>(EmptySql, ShoutingMapper());

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_WithoutMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = connection.QuerySingleOrDefault<Author, Book>(EmptySql);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_WithMapper_OnEmptyResult_Throws(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        Assert.Throws<InvalidOperationException>(() =>
            connection.QueryFirst<Author, Book>(EmptySql, ShoutingMapper()));
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_WithMapper_OnEmptyResult_Throws(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        Assert.Throws<InvalidOperationException>(() =>
            connection.QuerySingle<Author, Book>(EmptySql, ShoutingMapper()));
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_WithMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result =
            await connection.QueryFirstOrDefaultAsync<Author, Book>(EmptySql, ShoutingMapper());

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_WithoutMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result = await connection.QueryFirstOrDefaultAsync<Author, Book>(
            EmptySql, default(CommandOptions<(Author, Book)>));

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_WithMapper_OnEmptyResult_ReturnsNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        (Author, Book)? result =
            await connection.QuerySingleOrDefaultAsync<Author, Book>(EmptySql, ShoutingMapper());

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_WithMapper_OnEmptyResult_Throws(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.QueryFirstAsync<Author, Book>(EmptySql, ShoutingMapper()));
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_WithMapper_OnEmptyResult_Throws(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.QuerySingleAsync<Author, Book>(EmptySql, ShoutingMapper()));
    }

    /// <summary>
    /// The mapper path reports the tuple's element types the same way the reflection path does.
    /// <c>typeof</c> on the tuple itself would say <c>ValueTuple`2</c>, so the wording is supplied
    /// by the caller; these pin that the two paths still agree.
    /// </summary>
    [Theory]
    [SystemSqlite]
    public void QuerySingle_WithMapper_OnMoreThanOneRow_ThrowsWithTheSameWordingAsTheReflectionPath(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var withMapper = Assert.Throws<InvalidOperationException>(() =>
            connection.QuerySingle<Author, Book>(TwoRowSql, ShoutingMapper()));
        var withoutMapper = Assert.Throws<InvalidOperationException>(() =>
            connection.QuerySingle<Author, Book>(TwoRowSql));

        Assert.Contains("(Author, Book)", withMapper.Message, StringComparison.Ordinal);
        Assert.Equal(withoutMapper.Message, withMapper.Message);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_WithMapper_OnMoreThanOneRow_ThrowsWithTheSameWordingAsTheReflectionPath(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var withMapper = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.QuerySingleAsync<Author, Book>(TwoRowSql, ShoutingMapper()));
        var withoutMapper = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.QuerySingleAsync<Author, Book>(TwoRowSql, default(CommandOptions<(Author, Book)>)));

        Assert.Contains("(Author, Book)", withMapper.Message, StringComparison.Ordinal);
        Assert.Equal(withoutMapper.Message, withMapper.Message);
    }
}
