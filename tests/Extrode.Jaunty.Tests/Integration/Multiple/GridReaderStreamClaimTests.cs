using Extrode.Jaunty.Tests.Helpers.Dialects;

namespace Extrode.Jaunty.Tests.Integration.Multiple;

/// <summary>
/// AUD-R38-050: a stream left the grid on its result set until the iterator finished, so a read in
/// the meantime mapped that set as the wrong type and the stream then read the next one.
/// </summary>
public class GridReaderStreamClaimTests : IClassFixture<DialectFixture>
{
    private const string ThreeSets = "SELECT 1 AS Id; SELECT 2 AS Id; SELECT 3 AS Id";

    private readonly DialectFixture _fixture;

    public GridReaderStreamClaimTests(DialectFixture fixture) => _fixture = fixture;

    public sealed class Row
    {
        public long Id { get; set; }
    }

    private static long[] Ids(IEnumerable<Row> rows) => [.. rows.Select(r => r.Id)];

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ReadBeforeTheStreamIsEnumerated_Throws_AndTheStreamStillGetsItsOwnSet(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var grid = connection.QueryMultiple(ThreeSets);

        IEnumerable<Row> first = grid.ReadStream<Row>();

        var ex = Assert.Throws<InvalidOperationException>(() => grid.Read<Row>());
        Assert.Contains("still being streamed", ex.Message);
        Assert.Equal([1L], Ids(first));
        Assert.Equal([2L], Ids(grid.Read<Row>()));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ReadInsideAForeach_Throws_AndNoResultSetIsSkipped(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var grid = connection.QueryMultiple(ThreeSets);

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (Row _ in grid.ReadPartialStream<Row>())
                grid.ReadPartial<Row>();
        });

        Assert.Equal([2L], Ids(grid.Read<Row>()));
        Assert.Equal([3L], Ids(grid.Read<Row>()));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void AnotherStreamWhileOneIsOpen_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var grid = connection.QueryMultiple(ThreeSets);

        IEnumerable<Row> first = grid.ReadStream<Row>();

        Assert.Throws<InvalidOperationException>(() => grid.ReadStream<Row>());
        Assert.Equal([1L], Ids(first));
        Assert.Equal([2L], Ids(grid.ReadStream<Row>()));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DisposingAStartedEnumerator_ReleasesTheSet(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var grid = connection.QueryMultiple(ThreeSets);

        using (IEnumerator<Row> e = grid.ReadStream<Row>().GetEnumerator())
            Assert.True(e.MoveNext());

        Assert.Equal([2L], Ids(grid.Read<Row>()));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void EnumeratingAStreamTwice_Throws_WithoutReadingTheNextSet(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var grid = connection.QueryMultiple(ThreeSets);

        IEnumerable<Row> first = grid.ReadStream<Row>();
        Assert.Equal([1L], Ids(first));

        var ex = Assert.Throws<InvalidOperationException>(() => Ids(first));
        Assert.Contains("only once", ex.Message);
        Assert.Equal([2L], Ids(grid.Read<Row>()));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task AsyncStream_ReadBeforeEnumeration_Throws_AndTheStreamStillGetsItsOwnSet(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        await using var grid = await connection.QueryMultipleAsync(ThreeSets);

        IAsyncEnumerable<Row> first = grid.ReadStreamAsync<Row>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => grid.ReadAsync<Row>());
        Assert.Throws<InvalidOperationException>(() => grid.ReadPartialStreamAsync<Row>());

        var ids = new List<long>();
        await foreach (Row row in first)
            ids.Add(row.Id);

        Assert.Equal([1L], ids);
        long[] next = Ids(await grid.ReadAsync<Row>());
        Assert.Equal([2L], next);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task AsyncPartialStream_EnumeratedTwice_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        await using var grid = await connection.QueryMultipleAsync(ThreeSets);

        IAsyncEnumerable<Row> first = grid.ReadPartialStreamAsync<Row>();
        await foreach (Row _ in first)
        {
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (Row _ in first)
            {
            }
        });
        long[] next = Ids(await grid.ReadAsync<Row>());
        Assert.Equal([2L], next);
    }
}
