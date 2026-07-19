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
        // No other test in the suite currently registers interceptors via JauntyConfig
        // (see JauntyConfigInterceptorTests), so it is safe to fully clear the pipeline here.
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
}
