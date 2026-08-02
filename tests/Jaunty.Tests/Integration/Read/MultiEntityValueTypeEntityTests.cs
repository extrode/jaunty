using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R34-015. <c>T1..T7</c> on the multi-entity APIs are constrained only to <c>new()</c>, so a
/// struct entity compiles - and silently produces nothing. At arities 3-7 the apply closures are
/// <c>(t, r) =&gt; delegates[i](t!, r)</c> over <c>Action&lt;object, IDataRecord&gt;</c>, so the
/// target is boxed and the setters run against the throwaway box; arity 2 passes the struct by
/// value and loses the writes the same way. The caller got an all-default entity with no
/// exception. These assert the loud rejection that replaced it.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class MultiEntityValueTypeEntityTests : IClassFixture<DialectFixture>
{
    internal struct ValueAuthor
    {
        public ValueAuthor() { }

        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class RefBook
    {
        public long Id { get; set; }
        public long AuthorId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private const string TwoTableSql = @"
        SELECT a.id, a.name, b.id, b.author_id, b.name
        FROM struct_authors a
        JOIN struct_books b ON b.author_id = a.id";

    private const string ThreeTableSql = @"
        SELECT a.id, a.name, b.id, b.author_id, b.name, c.id, c.author_id, c.name
        FROM struct_authors a
        JOIN struct_books b ON b.author_id = a.id
        JOIN struct_books c ON c.author_id = a.id";

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE struct_authors (id INTEGER PRIMARY KEY, name TEXT);
            CREATE TABLE struct_books (id INTEGER PRIMARY KEY, author_id INTEGER, name TEXT);
            INSERT INTO struct_authors VALUES (1, 'Isaac Asimov');
            INSERT INTO struct_books VALUES (1, 1, 'Foundation');";
        cmd.ExecuteNonQuery();
        return connection;
    }

    [Theory]
    [SystemSqlite]
    public void QueryArity2_WithAStructEntity_IsRejected(DialectInfo _)
    {
        using IDbConnection connection = CreateAndSeed();

        var thrown = Assert.Throws<NotSupportedException>(() =>
            connection.Query<ValueAuthor, RefBook>(TwoTableSql).ToList());

        Assert.Contains("ValueAuthor", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("value type", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [SystemSqlite]
    public void QueryArity3_WithAStructEntity_IsRejected(DialectInfo _)
    {
        using IDbConnection connection = CreateAndSeed();

        var thrown = Assert.Throws<NotSupportedException>(() =>
            connection.Query<ValueAuthor, RefBook, RefBook>(ThreeTableSql).ToList());

        Assert.Contains("ValueAuthor", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [SystemSqlite]
    public void QueryArity3_WithAStructInTheLastPosition_IsRejected(DialectInfo _)
    {
        using IDbConnection connection = CreateAndSeed();

        var thrown = Assert.Throws<NotSupportedException>(() =>
            connection.Query<RefBook, RefBook, ValueAuthor>(ThreeTableSql).ToList());

        Assert.Contains("entity 3 of 3", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsyncArity2_WithAStructEntity_IsRejected(DialectInfo _)
    {
        using IDbConnection connection = CreateAndSeed();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await connection.QueryAsync<ValueAuthor, RefBook>(TwoTableSql));
    }

    [Theory]
    [SystemSqlite]
    public void QueryArity2_WithReferenceEntities_StillMaps(DialectInfo _)
    {
        using IDbConnection connection = CreateAndSeed();

        List<(RefBook, RefBook)> rows = connection.Query<RefBook, RefBook>(TwoTableSql).ToList();

        Assert.Single(rows);
    }
}
