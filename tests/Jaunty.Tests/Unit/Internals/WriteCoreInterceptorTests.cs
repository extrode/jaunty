using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression tests proving <c>InsertCore</c>/<c>UpdateCore</c>/<c>DeleteCore</c>/<c>GetCore</c>
/// (and their async counterparts) now participate in the registered <see cref="ICommandInterceptor"/>
/// pipeline, the same way <c>QueryCore</c>/<c>GetAllCore</c> already did before this fix (round 5
/// audit: Insert/Update/Delete/GetById silently bypassed registered interceptors).
/// </summary>
/// <remarks>
/// Shares the "Logging Extensions" collection with <see cref="ReadCoreInterceptorTests"/>,
/// <see cref="Jaunty.Tests.Unit.Interceptors.JauntyLoggingExtensionsTests"/>, and
/// <see cref="Jaunty.Tests.Unit.Configuration.JauntyConfigInterceptorTests"/> — all mutate the same
/// process-wide <see cref="JauntyConfig.InterceptorPipeline"/> static state and must run serialized
/// against each other.
/// </remarks>
[Collection("Logging Extensions")]
public class WriteCoreInterceptorTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public WriteCoreInterceptorTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();

        using var create = _connection.CreateCommand();
        create.CommandText = "CREATE TABLE write_intercept_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL)";
        create.ExecuteNonQuery();
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

    [Table("write_intercept_test")]
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
    public void Insert_WithRegisteredInterceptor_InvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var entity = new Entity { Name = "Row1" };
        long id = _connection.Insert(entity);

        Assert.True(id > 0);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task InsertAsync_WithRegisteredInterceptor_InvokesInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        var entity = new Entity { Name = "Row1" };
        long id = await _connection.InsertAsync(entity);

        Assert.True(id > 0);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public void Update_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        seed.Id = id;
        seed.Name = "Updated";
        // SQLiteConnection declares its own "Update" event, which hides the Jaunty.Update<T>
        // extension method from normal instance-member lookup - call it via the explicit static
        // form (namespace Jaunty, class Jaunty) instead of _connection.Update(seed).
        int affected = global::Jaunty.Jaunty.Update(_connection, seed);

        Assert.Equal(1, affected);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task UpdateAsync_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        seed.Id = id;
        seed.Name = "Updated";
        int affected = await global::Jaunty.Jaunty.UpdateAsync(_connection, seed);

        Assert.Equal(1, affected);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public void Delete_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);
        seed.Id = id;

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        int affected = _connection.Delete(seed);

        Assert.Equal(1, affected);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task DeleteAsync_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);
        seed.Id = id;

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        int affected = await _connection.DeleteAsync(seed);

        Assert.Equal(1, affected);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public void DeleteById_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        int affected = _connection.Delete<Entity>(id);

        Assert.Equal(1, affected);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public void GetById_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        Entity? found = _connection.Get<Entity>(id);

        Assert.NotNull(found);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithRegisteredInterceptor_InvokesInterceptor()
    {
        var seed = new Entity { Name = "Row1" };
        long id = _connection.Insert(seed);

        JauntyConfig.ClearInterceptors();
        var interceptor = new RecordingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        Entity? found = await _connection.GetAsync<Entity>(id);

        Assert.NotNull(found);
        Assert.Equal(1, interceptor.ExecutingCount);
        Assert.Equal(1, interceptor.ExecutedCount);
    }
}
