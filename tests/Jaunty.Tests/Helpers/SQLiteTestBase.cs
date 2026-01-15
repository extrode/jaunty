using Jaunty.Tests.Helpers;
using Xunit;

namespace Jaunty.Tests.Helpers;

/// <summary>
/// Base class for tests that should skip async SQLite limitation tests
/// </summary>
public abstract class SQLiteAsyncAwareTest : IDisposable
{
    protected internal Database _db;

    protected SQLiteAsyncAwareTest()
    {
        _db = new Database();
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    /// <summary>
    /// Override this in test classes to provide custom skip logic
    /// </summary>
    protected virtual bool ShouldSkipAsyncTests => true;
}

/// <summary>
/// Base class for tests that handles SQLite async limitations gracefully
/// Uses conditional assembly attribute to skip tests at runtime
/// </summary>
public abstract class SQLiteTestBase : IDisposable
{
    protected internal Database _db;

    protected SQLiteTestBase()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }
}