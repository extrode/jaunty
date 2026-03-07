using DuckDB.NET.Data;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers;

/// <summary>
/// Test fixture that provides a DuckDB in-memory connection for tests.
/// </summary>
public class TestFixture : IDisposable
{
    private readonly DuckDBConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Gets the DuckDB connection for tests.
    /// </summary>
    public DuckDBConnection Connection => _connection;

    public TestFixture()
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}
