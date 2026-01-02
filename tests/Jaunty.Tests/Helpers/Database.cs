using System.Data;
using System.Data.SQLite;

namespace Jaunty.Tests.Helpers;

internal class Database : IDisposable
{
    private readonly SQLiteConnection _connection;
    private bool _disposed;

    public IDbConnection Connection => _connection;

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
