using System.Data;
using System.Data.SQLite;

using Jaunty.StoredProcedure;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// Regression test for <c>ExecuteWithOutputParametersAsync</c>'s reader-close cleanup, which
/// (unlike most other cleanup sites in the codebase) does not run inside a <c>finally</c> block:
/// it runs after the handler returns successfully, right before the method returns its result.
/// The non-NET8_0_OR_GREATER <c>Task.Run(() =&gt; reader.Close())</c> call there must not observe
/// the operation's cancellation token, otherwise a token canceled by the handler replaces a
/// perfectly successful result with a spurious <see cref="TaskCanceledException"/> and leaks the
/// reader (never closed).
/// </summary>
public class ExecuteWithOutputParametersAsyncCancellationTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;

    public ExecuteWithOutputParametersAsyncCancellationTests()
    {
        _realConnection = new SQLiteConnection("Data Source=:memory:");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    [Fact]
    public async Task ExecuteWithOutputParametersAsync_TokenCanceledByHandler_StillClosesReaderAndReturnsResult()
    {
        using var cts = new CancellationTokenSource();
        var wrapper = new ThrowingDbConnection(_realConnection);

        Task<int> Handler(IDataReader reader, SpParameters parameters, CancellationToken ct)
        {
            // Simulate the token being canceled by the time the post-handler reader-close
            // cleanup runs, immediately after a perfectly successful handler result.
            cts.Cancel();
            return Task.FromResult(42);
        }

        int result = await global::Jaunty.Jaunty.ExecuteWithOutputParametersAsync<int>(
            wrapper,
            "SELECT 1",
            new SpParameters(),
            default,
            Handler,
            cts.Token);

        Assert.Equal(42, result);
        Assert.NotNull(wrapper.LastCommand?.LastReader);
        Assert.True(wrapper.LastCommand!.LastReader!.IsClosed);
    }
}
