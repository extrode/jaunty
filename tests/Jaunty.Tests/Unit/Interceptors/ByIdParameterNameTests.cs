using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Interceptors;
using Jaunty.Interfaces;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R26 (batch 3, low/consistency). The by-id read and delete paths reported a synthetic
/// <c>new { Id = id }</c> to the interceptor pipeline and to <c>JauntyConfig.Logger</c>, while the
/// parameter actually bound to the command is named after the <em>primary key column</em>
/// (<c>param.ParameterName = "@" + primaryKey.ColumnName</c>).
///
/// <para>
/// For any entity whose key column is not literally called <c>Id</c> - <c>ProductID</c>,
/// <c>order_id</c>, <c>CustomerCode</c> - an audit record or a log line named a parameter that does
/// not exist in the executed statement, while the one that does exist went unreported. The SQL text
/// handed over alongside it is the real <c>SelectByIdSql</c>/<c>DeleteByIdSql</c>, so the record
/// contradicted itself: it showed <c>WHERE "product_id" = @product_id</c> beside a parameter called
/// <c>Id</c>. Same "what interceptors are actually told" theme as the batch-2 write-path finding.
/// </para>
///
/// <para>
/// An audit trail that names the wrong parameter is worse than no audit trail, because it reads as
/// authoritative. These tests pin the reported name against the column the statement actually uses,
/// on all four by-id shapes (sync/async x object-id/typed-id) for both Get and Delete.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class ByIdParameterNameTests : IDisposable
{
    public ByIdParameterNameTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>The key column is deliberately not called "Id" - that is the whole finding.</summary>
    [Table("widgets")]
    public class Widget : IEntity<int>
    {
        [Key]
        [Column("product_id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    private sealed class CapturingInterceptor : ICommandInterceptor
    {
        public string? Sql { get; private set; }
        public object? Parameters { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Sql = context.CommandText;
            Parameters = context.Parameters;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE widgets (product_id INTEGER PRIMARY KEY, name TEXT);" +
            "INSERT INTO widgets VALUES (7, 'sprocket');" +
            "INSERT INTO widgets VALUES (8, 'flange');";
        create.ExecuteNonQuery();

        return connection;
    }

    private static CapturingInterceptor Register()
    {
        var interceptor = new CapturingInterceptor();
        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(interceptor);
        return interceptor;
    }

    /// <summary>
    /// The reported parameter set has to name the column the SQL references. Asserted against the
    /// SQL the interceptor was handed in the same call, so the two cannot drift apart again.
    /// </summary>
    private static void AssertNamesTheKeyColumn(CapturingInterceptor interceptor, object expectedValue)
    {
        Assert.NotNull(interceptor.Sql);
        Assert.Contains("product_id", interceptor.Sql!, StringComparison.Ordinal);

        var parameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(interceptor.Parameters);

        Assert.True(parameters.ContainsKey("product_id"),
            $"Expected the reported parameter set to name the key column the statement uses; " +
            $"got [{string.Join(", ", parameters.Keys)}] for SQL '{interceptor.Sql}'.");

        Assert.Equal(expectedValue, parameters["product_id"]);
        Assert.False(parameters.ContainsKey("Id"), "The synthetic 'Id' name must be gone.");
    }

    // ------------------------------------------------------------------
    // Get by id
    // ------------------------------------------------------------------

    [Fact]
    public void GetByObjectId_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Widget? widget = connection.Get<Widget>(7);

        Assert.Equal("sprocket", widget?.Name);
        AssertNamesTheKeyColumn(interceptor, 7);
    }

    [Fact]
    public void GetByTypedId_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Widget? widget = connection.Get<Widget, int>(8);

        Assert.Equal("flange", widget?.Name);
        AssertNamesTheKeyColumn(interceptor, 8);
    }

    [Fact]
    public async Task GetByObjectIdAsync_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Widget? widget = await connection.GetAsync<Widget>(7, TestContext.Current.CancellationToken);

        Assert.Equal("sprocket", widget?.Name);
        AssertNamesTheKeyColumn(interceptor, 7);
    }

    [Fact]
    public async Task GetByTypedIdAsync_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Widget? widget = await connection.GetAsync<Widget, int>(8, TestContext.Current.CancellationToken);

        Assert.Equal("flange", widget?.Name);
        AssertNamesTheKeyColumn(interceptor, 8);
    }

    // ------------------------------------------------------------------
    // Delete by id
    // ------------------------------------------------------------------

    [Fact]
    public void DeleteByObjectId_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Assert.Equal(1, connection.Delete<Widget>(7));
        AssertNamesTheKeyColumn(interceptor, 7);
    }

    [Fact]
    public void DeleteByTypedId_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Assert.Equal(1, connection.Delete<Widget, int>(8));
        AssertNamesTheKeyColumn(interceptor, 8);
    }

    [Fact]
    public async Task DeleteByObjectIdAsync_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Assert.Equal(1, await connection.DeleteAsync<Widget>(7, TestContext.Current.CancellationToken));
        AssertNamesTheKeyColumn(interceptor, 7);
    }

    [Fact]
    public async Task DeleteByTypedIdAsync_ReportsTheKeyColumnName()
    {
        CapturingInterceptor interceptor = Register();
        using SqliteConnection connection = Seed();

        Assert.Equal(1, await connection.DeleteAsync<Widget, int>(8, TestContext.Current.CancellationToken));
        AssertNamesTheKeyColumn(interceptor, 8);
    }

    // ------------------------------------------------------------------
    // The logger is the other half - it reported the same wrong name
    // ------------------------------------------------------------------

    /// <summary>
    /// The logger sites sit inside the *Direct methods and were a separate set of eight. They are
    /// reached when no interceptor is registered, so this test deliberately registers none.
    /// </summary>
    [Fact]
    public void TheLoggerReportsTheKeyColumnNameToo()
    {
        JauntyConfig.ClearInterceptors();

        object? logged = null;
        JauntyConfig.Logger = (_, parameters) => logged = parameters;

        using SqliteConnection connection = Seed();
        _ = connection.Get<Widget>(7);

        var parameters = Assert.IsAssignableFrom<IDictionary<string, object?>>(logged);
        Assert.True(parameters.ContainsKey("product_id"),
            $"Expected the logged parameter set to name the key column; got [{string.Join(", ", parameters.Keys)}].");
        Assert.Equal(7, parameters["product_id"]);
    }
}
