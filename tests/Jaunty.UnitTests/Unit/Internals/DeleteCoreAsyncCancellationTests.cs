using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression test for the write-path <c>DeleteByIdSimpleCoreAsync</c> cleanup: the
/// non-NET8_0_OR_GREATER <c>Task.Run(() =&gt; dbConnection.Close())</c> call in its
/// <c>finally</c> block must not observe the operation's cancellation token, otherwise a
/// canceled token masks the original exception with a <see cref="TaskCanceledException"/> and
/// the connection is never closed.
/// </summary>
public class DeleteCoreAsyncCancellationTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;

    public DeleteCoreAsyncCancellationTests()
    {
        _realConnection = new SQLiteConnection("Data Source=:memory:");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    [Table("simple_items")]
    public class SimpleItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }

    [Fact]
    public async Task DeleteByIdSimpleCoreAsync_TokenCanceledDuringExecute_StillClosesAndPropagatesOriginalException()
    {
        using var cts = new CancellationTokenSource();
        var wrapper = new ThrowingDbConnection(_realConnection)
        {
            // Simulate the token being canceled by the time cleanup runs in `finally`,
            // immediately before the real (non-cancellation) failure propagates.
            OnExecute = _ =>
            {
                cts.Cancel();
                throw new InvalidOperationException("boom");
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            global::Jaunty.Jaunty.DeleteByIdSimpleCoreAsync<SimpleItem>(
                wrapper,
                1,
                default,
                cts.Token).AsTask());

        Assert.Equal("boom", ex.Message);
        Assert.Equal(ConnectionState.Closed, _realConnection.State);
    }
}
