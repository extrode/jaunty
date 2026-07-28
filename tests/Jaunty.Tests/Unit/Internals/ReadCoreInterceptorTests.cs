using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression tests proving <c>GetAllCore</c>/<c>GetAllCoreAsync</c> and
/// <c>ExecuteQueryMultiple</c>/<c>ExecuteQueryMultipleAsync</c> now participate in the registered
/// <see cref="ICommandInterceptor"/> pipeline, the same way <c>QueryCore</c>/<c>QueryCoreAsync</c>
/// already did before this fix.
/// </summary>
/// <remarks>
/// Shares the "Logging Extensions" collection with <see cref="Jaunty.Tests.Unit.Interceptors.JauntyLoggingExtensionsTests"/>
/// and <see cref="Jaunty.Tests.Unit.Configuration.JauntyConfigInterceptorTests"/> — all three mutate
/// the same process-wide <see cref="JauntyConfig.InterceptorPipeline"/> static state and must run
/// serialized against each other.
/// </remarks>
[Collection("Logging Extensions")]
public class ReadCoreInterceptorTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public ReadCoreInterceptorTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();

        using var create = _connection.CreateCommand();
        create.CommandText = "CREATE TABLE read_intercept_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL)";
        create.ExecuteNonQuery();

        using var insert = _connection.CreateCommand();
        insert.CommandText = "INSERT INTO read_intercept_test (name) VALUES ('Row1')";
        insert.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Other tests in the "Logging Extensions" collection also register interceptors via
        // JauntyConfig, but the [Collection] attribute serializes them against this class, so
        // it's safe to fully clear the pipeline here.
        JauntyConfig.ClearInterceptors();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Table("read_intercept_test")]
    private class Entity
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RecordingInterceptor : ICommandInterceptor
    {
        public int ExecutingCount;
        public int ExecutedCount;

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ExecutingCount);
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ExecutedCount);
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
            => default;
    }

    [Fact]
    public void GetAll_WithRegisteredInterceptor_InvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = _connection.GetAll<Entity>();

        Assert.Single(rows);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task GetAllAsync_WithRegisteredInterceptor_InvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = await _connection.GetAllAsync<Entity>();

        Assert.Single(rows);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public void ExecuteQueryMultiple_WithRegisteredInterceptor_InvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        // A single SELECT is enough to exercise the ExecuteQueryMultiple interceptor wiring
        // without depending on System.Data.SQLite's multi-statement batch behavior.
        using var grid = _connection.QueryMultiple("SELECT * FROM read_intercept_test");

        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task ExecuteQueryMultipleAsync_WithRegisteredInterceptor_InvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        using var grid = await _connection.QueryMultipleAsync("SELECT * FROM read_intercept_test");

        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    // AUD-R24 batch-3: every Stream* path skips the InterceptorPipeline while its non-streaming
    // counterpart above invokes it. That is intentional - a lazy iterator can't be wrapped by
    // ExecuteWithInterception without materializing every row, which is the whole point of
    // streaming - but nothing documented or pinned it, so the divergence read as an oversight
    // and a future "fix" could silently make every streaming query buffer. These tests pin the
    // contract stated on ICommandInterceptor; if streaming ever does gain interceptor support,
    // they should fail loudly rather than let it happen by accident.

    [Fact]
    public void QueryStream_WithRegisteredInterceptor_DoesNotInvokeInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = _connection.QueryStream<Entity>("SELECT * FROM read_intercept_test").ToList();

        // The query really did run - this is a deliberate divergence, not a broken query.
        Assert.Single(rows);
        Assert.Equal(0, interceptor.ExecutingCount);
        Assert.Equal(0, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task QueryStreamAsync_WithRegisteredInterceptor_DoesNotInvokeInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = new List<Entity>();
        await foreach (Entity row in _connection.QueryStreamAsync<Entity>("SELECT * FROM read_intercept_test"))
            rows.Add(row);

        Assert.Single(rows);
        Assert.Equal(0, interceptor.ExecutingCount);
        Assert.Equal(0, interceptor.ExecutedCount);
    }

    [Fact]
    public void GetAllStream_WithRegisteredInterceptor_DoesNotInvokeInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = _connection.GetAllStream<Entity>().ToList();

        Assert.Single(rows);
        Assert.Equal(0, interceptor.ExecutingCount);
        Assert.Equal(0, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task GetAllStreamAsync_WithRegisteredInterceptor_DoesNotInvokeInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = new List<Entity>();
        await foreach (Entity row in _connection.GetAllStreamAsync<Entity>())
            rows.Add(row);

        Assert.Single(rows);
        Assert.Equal(0, interceptor.ExecutingCount);
        Assert.Equal(0, interceptor.ExecutedCount);
    }

    // The same query through the non-streaming API must still be intercepted: this is what makes
    // the four tests above a statement about *streaming* rather than about this SQL or entity.
    [Fact]
    public void Query_SameSqlAsQueryStream_StillInvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var rows = _connection.Query<Entity>("SELECT * FROM read_intercept_test").ToList();

        Assert.Single(rows);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }
}
