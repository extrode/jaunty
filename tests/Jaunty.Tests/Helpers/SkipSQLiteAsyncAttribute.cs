namespace Jaunty.Tests.Helpers;

/// <summary>
/// Skips test when using SQLite with async operations that have known limitations
/// This addresses the SQLite async DataReader GetName() limitation.
/// </summary>
public sealed class SkipSQLiteAsyncFactAttribute : FactAttribute
{
    public SkipSQLiteAsyncFactAttribute()
    {
        Skip = "SQLite async DataReader limitation: GetName() not supported in async contexts. Use sync methods for QueryMultiple operations.";
    }
}

/// <summary>
/// Skips async theory when using SQLite with known limitations
/// </summary>
public sealed class SkipSQLiteAsyncTheoryAttribute : TheoryAttribute
{
    public SkipSQLiteAsyncTheoryAttribute()
    {
        Skip = "SQLite async DataReader limitation: GetName() not supported in async contexts. Use sync methods for QueryMultiple operations.";
    }
}
