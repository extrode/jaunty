using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Dialects;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class TablePromoterTests : IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly DuckDbDialect _dialect;

    public TablePromoterTests()
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();
        _dialect = DuckDbDialect.Instance;

        // Create a test view
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE VIEW test_view AS SELECT 1 as id, 'test' as name";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public void EnsurePromotedToTable_PromotesViewToTable()
    {
        // Arrange
        var source = new TestFileSource("test_view");

        // Act
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        // Assert
        Assert.True(source.IsPromotedToTable);

        // Verify it's now a table, not a view. information_schema.tables in DuckDB lists both
        // tables and views (distinguished by table_type), so the type must be checked explicitly
        // rather than just checking for a matching row.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'test_view'";
        var result = cmd.ExecuteScalar();
        Assert.Equal("BASE TABLE", result);
    }

    /// <summary>
    /// AUD-R26-069: this used to assert the opposite - that a source arriving with
    /// <c>IsPromotedToTable = true</c> suppressed promotion - and that assertion was the defect.
    /// The flag is state on the <em>file source</em> describing something that happened on
    /// <em>one connection</em>. A source added to two <c>FlatFileOptions</c> ("the same CSV, two
    /// databases") therefore got promoted on the first connection and skipped on the second, which
    /// then issued its UPDATE/DELETE against a view - and DuckDB rejects that. Promotion is now
    /// tracked per connection, so a flag set elsewhere no longer speaks for this one.
    /// </summary>
    [Fact]
    public void EnsurePromotedToTable_FlagSetByAnotherConnection_StillPromotesHere()
    {
        var source = new TestFileSource("test_view") { IsPromotedToTable = true };

        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        Assert.True(source.IsPromotedToTable);
        Assert.Equal("BASE TABLE", TableTypeOf(_connection, "test_view"));
    }

    /// <summary>
    /// Preloaded sources are a different case and must still short-circuit: there is no view to
    /// promote, so the promotion SQL would fail rather than be redundant.
    /// </summary>
    [Fact]
    public void EnsurePromotedToTable_Preloaded_NoOp()
    {
        var source = new TestFileSource("test_view") { IsPreloaded = true };

        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        Assert.Equal("VIEW", TableTypeOf(_connection, "test_view"));
    }

    /// <summary>
    /// And the per-connection record still does its job: promoting twice on the same connection
    /// runs the SQL once. The second run would fail on <c>DROP VIEW</c> if it did not, since by then
    /// the view is gone.
    /// </summary>
    [Fact]
    public void EnsurePromotedToTable_TwiceOnOneConnection_PromotesOnce()
    {
        var source = new TestFileSource("test_view");

        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        Assert.Equal("BASE TABLE", TableTypeOf(_connection, "test_view"));
    }

    /// <summary>
    /// The scenario the finding describes, end to end: one source instance, two connections. Both
    /// must end up with a real table.
    /// </summary>
    [Fact]
    public void EnsurePromotedToTable_OneSourceTwoConnections_PromotesOnBoth()
    {
        var source = new TestFileSource("test_view");

        using var second = new DuckDBConnection("DataSource=:memory:");
        second.Open();
        using (DuckDBCommand cmd = second.CreateCommand())
        {
            cmd.CommandText = "CREATE VIEW test_view AS SELECT 1 as id, 'test' as name";
            cmd.ExecuteNonQuery();
        }

        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);
        TablePromoter.EnsurePromotedToTable(second, source, _dialect);

        Assert.Equal("BASE TABLE", TableTypeOf(_connection, "test_view"));
        Assert.Equal("BASE TABLE", TableTypeOf(second, "test_view"));
    }

    private static object? TableTypeOf(DuckDBConnection connection, string name)
    {
        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT table_type FROM information_schema.tables WHERE table_name = '{name}'";
        return cmd.ExecuteScalar();
    }

    [Fact]
    public async Task EnsurePromotedToTableAsync_PromotesViewToTable()
    {
        // Arrange
        var source = new TestFileSource("test_view2");

        // Create another test view
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "CREATE VIEW test_view2 AS SELECT 1 as id, 'test' as name";
            cmd.ExecuteNonQuery();
        }

        // Act
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, default);

        // Assert
        Assert.True(source.IsPromotedToTable);

        // Verify it's now a table, not a view. information_schema.tables in DuckDB lists both
        // tables and views (distinguished by table_type), so the type must be checked explicitly
        // rather than just checking for a matching row.
        using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'test_view2'";
        var result = checkCmd.ExecuteScalar();
        Assert.Equal("BASE TABLE", result);
    }

    /// <inheritdoc cref="EnsurePromotedToTable_FlagSetByAnotherConnection_StillPromotesHere"/>
    [Fact]
    public async Task EnsurePromotedToTableAsync_FlagSetByAnotherConnection_StillPromotesHere()
    {
        var source = new TestFileSource("test_view") { IsPromotedToTable = true };

        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, default);

        Assert.True(source.IsPromotedToTable);
        Assert.Equal("BASE TABLE", TableTypeOf(_connection, "test_view"));
    }

    private sealed class TestFileSource : IFileSource
    {
        public string TableName { get; }
        public string FilePath => "test";
        public IReadOnlyList<string> FilePaths => new[] { FilePath };
        public string Format => "TEST";
        public Type EntityType => typeof(object);
        public bool IsPromotedToTable { get; set; }
        public bool IsPreloaded { get; set; }
        public string DuckDbFormatName => "TEST";

        public TestFileSource(string tableName)
        {
            TableName = tableName;
        }

        public string GenerateReadFunction(string pathExpression) => "SELECT 1";
        public string? GenerateCopyToOptions() => null;
    }
}