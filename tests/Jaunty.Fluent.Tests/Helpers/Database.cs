using System.Data;
using System.Data.SQLite;
using System.IO;

namespace Jaunty.Fluent.Tests.Helpers;

public class Database : IDisposable
{
    private readonly SQLiteConnection _connection;
    private readonly string _dbFilePath;
    private bool _disposed;

    public IDbConnection Connection => _connection;

    public bool IsSQLite => true;

    public Database()
    {
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
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
            File.Delete(_dbFilePath);
        }
        _disposed = true;
    }
}