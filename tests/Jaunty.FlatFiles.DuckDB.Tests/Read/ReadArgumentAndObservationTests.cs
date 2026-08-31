using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.DuckDB.Tests.Unit;
using Jaunty.Interceptors;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-255 and AUD-R35-257. The sync read described its parameters twice - once for the
/// interceptor and again for the logger - where the async twin already described once and threaded
/// the result through, so every parameterised sync read allocated two dictionaries and the audit
/// record could disagree with what was bound. And a null <c>parameters</c> argument failed
/// differently across the twins: the sync path bound a null array and threw
/// <see cref="NullReferenceException"/> from the binding loop, the async path threw
/// <see cref="ArgumentNullException"/> from <c>ToArray</c>. Neither named the argument.
/// </summary>
[Collection(GlobalInterceptorStateCollection.Name)]
public class ReadArgumentAndObservationTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private readonly RecordingInterceptor _interceptor = new();
    private readonly List<object?> _logged = [];

    public ReadArgumentAndObservationTests()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(Path.Combine(DataDir, "csv", "sales.csv"));
        _db = new DuckDb(options);

        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(_interceptor);
        JauntyConfig.Logger = (_, parameters) => _logged.Add(parameters);
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class RecordingInterceptor : ICommandInterceptor
    {
        public List<object?> Parameters { get; } = [];

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Parameters.Add(context.Parameters);
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
            => default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
            => default;
    }

    private const string Sql = "SELECT * FROM \"sales\" WHERE \"region\" = $region";

    [Fact]
    public void Query_HandsTheInterceptorAndTheLoggerTheSameParameterSet()
    {
        _db.Query<SalesRecord>(Sql, ("region", "Northeast"));

        Assert.Same(Assert.Single(_interceptor.Parameters), Assert.Single(_logged));
    }

    [Fact]
    public async Task QueryAsync_StillSharesItsParameterSetToo()
    {
        await _db.QueryAsync<SalesRecord>(Sql, [("region", "Northeast")]);

        Assert.Same(Assert.Single(_interceptor.Parameters), Assert.Single(_logged));
    }

    [Fact]
    public void TheSharedSet_StillDescribesTheParameter()
    {
        _db.Query<SalesRecord>(Sql, ("region", "Northeast"));

        var described = (IReadOnlyDictionary<string, object?>)Assert.Single(_logged)!;

        Assert.Equal("Northeast", Assert.Single(described.Values));
    }

    [Fact]
    public void TheRowsStillComeBackFiltered()
    {
        List<SalesRecord> rows = _db.Query<SalesRecord>(Sql, ("region", "Northeast"));

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal("Northeast", r.Region));
    }

    [Fact]
    public void Query_NullParameters_ThrowsArgumentNullExceptionNamingTheArgument()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => _db.Query<SalesRecord>("SELECT * FROM \"sales\"", (ValueTuple<string, object?>[])null!));

        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void Query_NullParametersWithOptions_ThrowsToo()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => _db.Query<SalesRecord>(
                "SELECT * FROM \"sales\"", default(CommandOptions), (ValueTuple<string, object?>[])null!));

        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public async Task QueryAsync_NullParameters_ThrowsTheSameWay()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.QueryAsync<SalesRecord>(
                "SELECT * FROM \"sales\"", (IEnumerable<(string, object?)>)null!).AsTask());

        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public async Task QueryAsync_NullParametersWithOptions_ThrowsTheSameWay()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.QueryAsync<SalesRecord>(
                "SELECT * FROM \"sales\"", (IEnumerable<(string, object?)>)null!, default(CommandOptions)).AsTask());

        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void AnEmptyParameterArray_IsStillFine()
        => Assert.NotEmpty(_db.Query<SalesRecord>("SELECT * FROM \"sales\""));
}
