using System.Data;
using System.Data.SQLite;

namespace Jaunty.Fluent.Tests.Helpers;

public class Database : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public IDbConnection Connection => _connection;

    public bool IsSQLite => true;

    public Database()
    {
        _connection = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Dispose();
        _disposed = true;
    }
}
