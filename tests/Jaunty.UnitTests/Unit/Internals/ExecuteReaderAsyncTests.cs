using System.Data;
using System.Data.SQLite;

using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression tests for the non-DbConnection fallback branches of the internal
/// <c>ExecuteReaderAsync</c> helper: the cleanup <c>Task.Run(() =&gt; connection.Close())</c>
/// call must not observe the operation's cancellation token, otherwise a canceled token
/// masks the original exception with a <see cref="TaskCanceledException"/> and the
/// connection is never closed.
/// </summary>
public class ExecuteReaderAsyncTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public ExecuteReaderAsyncTests()
    {
        // Deliberately left closed: ExecuteReaderAsync only closes connections it opened itself.
        _realConnection = new SQLiteConnection("Data Source=:memory:");
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    [Fact]
    public async Task ExecuteReaderAsync_NonDbConnection_TokenCanceledBeforeFinally_StillClosesAndPropagatesOriginalException()
    {
        using var cts = new CancellationTokenSource();

        Task<int> ThrowingHandler(IDataReader reader, CancellationToken ct)
        {
            // Simulate the token being canceled by the time cleanup runs in `finally`.
            cts.Cancel();
            throw new InvalidOperationException("boom");
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            global::Jaunty.Jaunty.ExecuteReaderAsync<int>(
                _wrapper,
                "SELECT 1",
                null,
                default,
                ThrowingHandler,
                cts.Token).AsTask());

        Assert.Equal("boom", ex.Message);
        Assert.Equal(ConnectionState.Closed, _realConnection.State);
    }
}
