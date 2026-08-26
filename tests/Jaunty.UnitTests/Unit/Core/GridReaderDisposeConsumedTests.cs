using Jaunty.Core;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// R27 batch 6. <c>_consumed</c> was set only by <c>Advance</c>/<c>AdvanceAsync</c>, never by
/// <c>Dispose</c>/<c>DisposeAsync</c>, so a Read* after an explicit early dispose passed
/// <c>EnsureNotConsumed</c> and failed inside the provider's disposed reader instead of with the
/// documented "All result sets have already been consumed."
/// </summary>
public class GridReaderDisposeConsumedTests
{
    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    [Fact]
    public void Read_AfterExplicitDispose_ThrowsTheDocumentedConsumedException()
    {
        using var connection = Open();
        GridReader grid = connection.QueryMultiple("SELECT 1; SELECT 2;");
        grid.Dispose();

        var ex = Assert.Throws<InvalidOperationException>(() => grid.Read<int>());
        Assert.Contains("already been consumed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_AfterExplicitDisposeAsync_ThrowsTheDocumentedConsumedException()
    {
        using var connection = Open();
        GridReader grid = connection.QueryMultiple("SELECT 1; SELECT 2;");
        await grid.DisposeAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => grid.ReadAsync<int>());
        Assert.Contains("already been consumed", ex.Message, StringComparison.Ordinal);
    }
}
