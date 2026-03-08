using System.Data;
using System.Data.SQLite;
using System.IO;

namespace Jaunty.Fluent.Tests.Helpers;

/// <summary>
/// Shared database fixture for Fluent API tests.
/// Creates a single in-memory SQLite database shared across all test classes.
/// </summary>
public class FluentDatabaseFixture : IDisposable
{
    private readonly SQLiteConnection _connection;
    private readonly string _dbFilePath;
    private bool _disposed;

    public IDbConnection Connection => _connection;

    public FluentDatabaseFixture()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"jaunty_fluent_{Guid.NewGuid()}.db");
        _connection = new SQLiteConnection($"Data Source={_dbFilePath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        string scriptPath = Path.Combine(AppContext.BaseDirectory, "data", "initialize_fluent_test_db.sql");
        string sql = File.ReadAllText(scriptPath);

        // Split by semicolon and execute individually to ensure all statements are run
        var statements = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            using var command = _connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Close();
        _connection?.Dispose();

        if (File.Exists(_dbFilePath))
        {
            try
            {
                File.Delete(_dbFilePath);
            }
            catch
            {
                // Ignore deletion errors on cleanup
            }
        }
        _disposed = true;
    }
}