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

    [Fact]
    public void EnsurePromotedToTable_AlreadyTable_NoOp()
    {
        // Arrange
        var source = new TestFileSource("test_view") { IsPromotedToTable = true };

        // Act
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        // Assert
        Assert.True(source.IsPromotedToTable);

        // No promotion SQL should have run: test_view must still be a view, not a table.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'test_view'";
        var result = cmd.ExecuteScalar();
        Assert.Equal("VIEW", result);
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

    [Fact]
    public async Task EnsurePromotedToTableAsync_AlreadyTable_NoOp()
    {
        // Arrange
        var source = new TestFileSource("test_view") { IsPromotedToTable = true };

        // Act
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, default);

        // Assert
        Assert.True(source.IsPromotedToTable);

        // No promotion SQL should have run: test_view must still be a view, not a table.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'test_view'";
        var result = cmd.ExecuteScalar();
        Assert.Equal("VIEW", result);
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