using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Unit;
using Extrode.Jaunty.Interceptors;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Write;

[Collection(GlobalInterceptorStateCollection.Name)]
public class BatchInsertCancellationTests : IDisposable
{
    private const int RowsPerChunk = 32768 / 2;

    private static int s_reads;

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_cancel_{Guid.NewGuid():N}");
    private readonly DuckDb _db;
    private readonly CancellationTokenSource _cts = new();

    [Table("cancelling")]
    private sealed class CancellingRow
    {
        public long Id { get; set; }

        public string Amount
        {
            get
            {
                s_reads++;
                return "1";
            }
            set { }
        }
    }

    private sealed class CancelAfterInsert(CancellationTokenSource cts) : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            if (context.CommandText.StartsWith("INSERT", StringComparison.Ordinal))
                cts.Cancel();

            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    public BatchInsertCancellationTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "cancelling.csv");
        File.WriteAllText(csvPath, "Id,Amount\n1,10\n");

        var options = new FlatFileOptions();
        options.AddCsv<CancellingRow>(csvPath);
        _db = new DuckDb(options);

        s_reads = 0;
        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(new CancelAfterInsert(_cts));
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        _db.Dispose();
        _cts.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ACancellationAfterTheFirstChunk_StopsBeforeTheSecondChunkIsBuilt()
    {
        var rows = Enumerable.Range(0, RowsPerChunk + 1).Select(i => new CancellingRow { Id = i + 100 }).ToList();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _db.InsertAsync<CancellingRow>(rows, _cts.Token));

        Assert.Equal(RowsPerChunk, s_reads);
    }
}
