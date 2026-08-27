using System.Data;

using Jaunty.Configuration;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Jaunty.Interceptors;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26 (batch 5, high/security; part of the interception cluster). Jaunty.Fluent contained
/// <b>zero</b> occurrences of <c>InterceptorPipeline</c> and zero of <c>JauntyConfig.Logger</c>.
/// Thirty methods across twelve files built, bound and executed a command inline, and not one
/// consulted either hook.
///
/// <para>
/// Whether a fluent call was auditable was therefore decided by an implementation detail the caller
/// could not see: terminals that delegate to a core extension method were intercepted because
/// <em>core</em> intercepts, and terminals that ran their own command were not. The split ran
/// <em>inside a single builder</em> - <c>.Select()</c> on a joined query was audited and
/// <c>.SelectBoth()</c> on the identical query was not.
/// </para>
///
/// <para>
/// Every fluent write was unseen. Someone registering <c>Jaunty.Diagnostics.AuditInterceptor</c> for
/// a compliance requirement and writing through the fluent API got an empty audit log, while
/// <see cref="ICommandInterceptor"/>'s own remarks promised "<i>as do all write operations</i>".
/// </para>
///
/// <para>
/// These cases are the finding's own measurement table, turned into assertions.
/// </para>
/// </summary>
public class FluentObservabilityTests : IClassFixture<FluentDatabaseFixture>, IDisposable
{
    private readonly FluentDatabaseFixture _fixture;
    private readonly CountingInterceptor _interceptor = new();
    private readonly List<string> _logged = [];

    public FluentObservabilityTests(FluentDatabaseFixture fixture)
    {
        _fixture = fixture;

        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(_interceptor);
        JauntyConfig.Logger = (sql, _) => _logged.Add(sql);
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        GC.SuppressFinalize(this);
    }

    private sealed class CountingInterceptor : ICommandInterceptor
    {
        public int Executing { get; private set; }
        public int Executed { get; private set; }
        public string? LastSql { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executing++;
            LastSql = context.CommandText;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed++;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    private void AssertObserved(string what)
    {
        Assert.True(_interceptor.Executing > 0,
            $"'{what}' executed a command that no registered ICommandInterceptor ever saw. " +
            "Fluent terminals that run their own command used to bypass the pipeline entirely.");

        Assert.True(_interceptor.Executed > 0,
            $"'{what}' reported Executing but never Executed - the pipeline did not wrap the whole operation.");

        Assert.NotNull(_interceptor.LastSql);
    }

    private IDbConnection Connection => _fixture.Connection;

    // ------------------------------------------------------------------
    // Reads that were already seen - controls, must not regress
    // ------------------------------------------------------------------

    [Fact]
    public void APlainSelectIsObserved()
    {
        _ = Connection.From<Product>().Select();
        AssertObserved("From<Product>().Select()");
    }

    [Fact]
    public void AJoinedSelectIsObserved()
    {
        _ = Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .Select();

        AssertObserved("From<Product>().InnerJoin<Category>().On(..).Select()");
    }

    // ------------------------------------------------------------------
    // The split inside a single builder - .Select() was seen, .SelectBoth()
    // on the identical query was not
    // ------------------------------------------------------------------

    [Fact]
    public void SelectBothOnTheSameJoinIsObservedToo()
    {
        _ = Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .SelectBoth();

        AssertObserved("From<Product>().InnerJoin<Category>().On(..).SelectBoth()");
    }

    [Fact]
    public void SelectPartialOnAJoinIsObserved()
    {
        _ = Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .SelectPartial("products.product_id, categories.category_name");

        AssertObserved("SelectPartial on a join");
    }

    // ------------------------------------------------------------------
    // Grouped reads - previously unseen
    // ------------------------------------------------------------------

    [Fact]
    public void AGroupedSelectIsObserved()
    {
        _ = Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { Key = g.Key, Count = g.Count() });

        AssertObserved("From<Product>().GroupBy(..).Select(..)");
    }

    // ------------------------------------------------------------------
    // Writes - the whole fluent write surface was unseen
    // ------------------------------------------------------------------

    [Fact]
    public void AFluentInsertIsObserved()
    {
        _ = Connection.Into<Product>()
            .Value(p => p.ProductName, "observability-probe")
            .Insert();

        AssertObserved("Into<Product>().Value(..).Insert()");
        Assert.Contains(_logged, sql => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AFluentUpdateIsObserved()
    {
        _ = Connection.Into<Product>()
            .Value(p => p.ProductName, "observability-update-seed")
            .Insert();

        _ = Connection.From<Product>()
            .Set(p => p.ProductName, "observability-updated")
            .Where(p => p.ProductName == "observability-update-seed")
            .Update();

        AssertObserved("From<Product>().Set(..).Where(..).Update()");
        Assert.Contains(_logged, sql => sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AFluentDeleteIsObserved()
    {
        _ = Connection.Into<Product>()
            .Value(p => p.ProductName, "observability-delete-seed")
            .Insert();

        _ = Connection.From<Product>()
            .Where(p => p.ProductName == "observability-delete-seed")
            .Delete();

        AssertObserved("From<Product>().Where(..).Delete()");
        Assert.Contains(_logged, sql => sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    // Async twins
    // ------------------------------------------------------------------

    [Fact]
    public async Task AFluentInsertAsyncIsObserved()
    {
        _ = await Connection.Into<Product>()
            .Value(p => p.ProductName, "observability-probe-async")
            .InsertAsync(TestContext.Current.CancellationToken);

        AssertObserved("Into<Product>().Value(..).InsertAsync()");
    }

    [Fact]
    public async Task SelectPartialAsyncIsObserved()
    {
        _ = await Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .SelectPartialAsync(
                "products.product_id, categories.category_name",
                TestContext.Current.CancellationToken);

        AssertObserved("SelectPartialAsync on a join");
    }

    // ------------------------------------------------------------------
    // The logger was the other half of the same gap
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // AUD-R31: what is reported must be what executes
    // ------------------------------------------------------------------

    [Fact]
    public void AJoinedMappedSelectFirstReportsThePagedSqlItActuallyExecutes()
    {
        // SelectWithMapper applied the limit inside the callback CommandObservation.Execute
        // wraps, so the pipeline was handed the un-paginated text while the command that ran
        // carried LIMIT.
        _ = Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .SelectFirst(r => r.GetInt32(0));

        AssertObserved("SelectFirst(mapper) on a join");
        Assert.Contains("LIMIT", _interceptor.LastSql!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AJoinedMappedSelectFirstAsyncReportsThePagedSqlItActuallyExecutes()
    {
        _ = await Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .SelectFirstAsync(r => r.GetInt32(0), TestContext.Current.CancellationToken);

        AssertObserved("SelectFirstAsync(mapper) on a join");
        Assert.Contains("LIMIT", _interceptor.LastSql!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheLoggerSeesFluentCommandsToo()
    {
        JauntyConfig.ClearInterceptors();

        _ = Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .SelectBoth();

        Assert.NotEmpty(_logged);
    }
}
