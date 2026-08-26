using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-009. <c>QueryFirstCore</c> delegated to <c>QueryFirstOrDefaultCore</c> and decided "no
/// rows" with <c>entity is null</c>. <c>T</c> is constrained only by <c>new()</c>, so for a value
/// type the empty-result sentinel is <c>default(T)</c> - never null - and an empty result set
/// returned a zeroed value rather than throwing. The async twin reads the reader itself and threw
/// all along, so the same query behaved differently on the two APIs.
/// </summary>
public class QueryFirstEmptyResultTests
{
    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private const string NoRows = "SELECT 1 AS Id, 'x' AS Name WHERE 1 = 0";

    [Fact]
    public void QueryFirst_ValueTuple_EmptyResult_Throws()
    {
        using SqliteConnection connection = Open();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<(int Id, string Name)>(NoRows));

        Assert.Contains("Sequence contains no elements", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryFirstAsync_ValueTuple_EmptyResult_Throws()
    {
        using SqliteConnection connection = Open();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await connection.QueryFirstAsync<(int Id, string Name)>(NoRows));

        Assert.Contains("Sequence contains no elements", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFirst_ScalarValueType_EmptyResult_Throws()
    {
        using SqliteConnection connection = Open();

        Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<int>("SELECT 1 WHERE 1 = 0"));
    }

    [Fact]
    public void QueryFirst_ValueTuple_WithRows_StillReturnsTheFirst()
    {
        using SqliteConnection connection = Open();

        (int Id, string Name) first = connection.QueryFirst<(int Id, string Name)>(
            "SELECT 1 AS Id, 'a' AS Name UNION ALL SELECT 2, 'b'");

        Assert.Equal(1, first.Id);
        Assert.Equal("a", first.Name);
    }

    [Fact]
    public void QueryFirstOrDefault_ValueTuple_EmptyResult_StillReturnsTheDefault()
    {
        using SqliteConnection connection = Open();

        (int Id, string Name) result = connection.QueryFirstOrDefault<(int Id, string Name)>(NoRows);

        Assert.Equal(0, result.Id);
        Assert.Null(result.Name);
    }

    [Fact]
    public void QueryFirst_ReferenceType_EmptyResult_StillThrows()
    {
        using SqliteConnection connection = Open();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<EmptyProbe>(NoRows));

        Assert.Contains("EmptyProbe", ex.Message, StringComparison.Ordinal);
    }

    public class EmptyProbe
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
