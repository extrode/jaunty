using DuckDB.NET.Data;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.FlatFiles.DuckDB.Dialects;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Unit;
using Extrode.Jaunty.FlatFiles.Interfaces;
using Extrode.Jaunty.Interceptors;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

[Collection(GlobalInterceptorStateCollection.Name)]
public sealed class TablePromoterObservationTests : IDisposable
{
    private readonly DuckDBConnection _connection = new("DataSource=:memory:");
    private readonly DuckDbDialect _dialect = DuckDbDialect.Instance;
    private readonly List<string> _logged = [];

    public TablePromoterObservationTests()
    {
        _connection.Open();
        using DuckDBCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE VIEW promoted_view AS SELECT 1 AS id";
        cmd.ExecuteNonQuery();

        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = (sql, _) => _logged.Add(sql);
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        _connection.Dispose();
    }

    private sealed class ViewSource(string tableName) : IFileSource
    {
        public string TableName { get; } = tableName;
        public string FilePath => "test";
        public IReadOnlyList<string> FilePaths => [FilePath];
        public string Format => "TEST";
        public Type EntityType => typeof(object);
        public bool IsPromotedToTable { get; set; }
        public bool IsPreloaded { get; set; }
        public string DuckDbFormatName => "TEST";
        public string GenerateReadFunction(string pathExpression) => "SELECT 1";
        public string? GenerateCopyToOptions() => null;
    }

    private sealed class SlowInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken) => new(Task.Delay(30, cancellationToken));

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken) => new(Task.Delay(30, cancellationToken));

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    [Fact]
    public void ThePromotionStatement_IsLoggedOnce()
    {
        var source = new ViewSource("promoted_view");

        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        Assert.Equal([_dialect.GeneratePromoteToTableSql(source)], _logged);
    }

    [Fact]
    public async Task ThePromotionStatement_IsLoggedOnceOnTheAsyncPathToo()
    {
        var source = new ViewSource("promoted_view");

        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, default);

        Assert.Equal([_dialect.GeneratePromoteToTableSql(source)], _logged);
    }

    [Fact]
    public void AFailedPromotion_Propagates_AndLeavesTheSourceUnpromoted()
    {
        var source = new ViewSource("no_such_view");

        Assert.ThrowsAny<DuckDBException>(() => TablePromoter.EnsurePromotedToTable(_connection, source, _dialect));

        Assert.False(source.IsPromotedToTable);
    }

    [Fact]
    public async Task AFailedPromotion_PropagatesOnTheAsyncPathToo()
    {
        var source = new ViewSource("no_such_view");

        await Assert.ThrowsAnyAsync<DuckDBException>(() => TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, default).AsTask());

        Assert.False(source.IsPromotedToTable);
    }

    [Fact]
    public void AnObservedAsyncPromotion_NeverReturnsToTheCallersContext()
    {
        JauntyConfig.AddInterceptor(new SlowInterceptor());
        var source = new ViewSource("promoted_view");
        var context = new CountingSynchronizationContext();

        SynchronizationContext? previous = SynchronizationContext.Current;
        Task promotion;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            promotion = TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, default).AsTask();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        promotion.GetAwaiter().GetResult();

        Assert.True(source.IsPromotedToTable);
        Assert.Equal(0, context.Posts);
    }
}
