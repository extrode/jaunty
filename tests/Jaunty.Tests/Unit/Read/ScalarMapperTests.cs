using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 1, low/consistency). All eight <c>CommandOptions&lt;T&gt;</c> overloads across
/// <c>QueryScalar</c>, <c>QueryScalarAsync</c>, <c>ExecuteScalar</c> and <c>ExecuteScalarAsync</c>
/// accepted an options value whose <c>WithMapper</c> factory was silently discarded - no compile
/// error, no runtime error, the mapper simply never called. The generic parameter on
/// <c>CommandOptions&lt;T&gt;</c> exists only to carry that mapper, so the signatures advertised a
/// capability they did not have.
///
/// <para>
/// Both cores have two code paths - a fast path and an interceptor-pipeline path - and the mapper
/// was ignored on all four. Each is exercised here, because a fix applied to the fast path alone
/// would pass any test that did not register an interceptor.
/// </para>
/// </summary>
// The same collection WriteObservabilityTests and SpParametersObservabilityTests use. These tests
// mutate JauntyConfig's process-wide interceptor registry, so they must not run beside anything else
// that does - a separate collection name would have run them in parallel with exactly those classes.
[Collection("Jaunty Config State")]
public class ScalarMapperTests : IDisposable
{
    public void Dispose() => JauntyConfig.ClearInterceptors();

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = """
            CREATE TABLE products (id INTEGER, name TEXT, price REAL);
            INSERT INTO products VALUES (1, 'widget', 150.0);
            """;
        seed.ExecuteNonQuery();
        return connection;
    }

    private static void RegisterAnInterceptor()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(new NoOpInterceptor());
    }

    // ------------------------------------------------------------------
    // The mapper is called - fast path
    // ------------------------------------------------------------------

    [Fact]
    public void QueryScalar_CallsTheMapper()
    {
        using SqliteConnection connection = Seed();

        var options = CommandOptions<string>.WithMapper(r => $"{r.GetString(1)}@{r.GetDouble(2)}");

        Assert.Equal("widget@150", connection.QueryScalar("SELECT id, name, price FROM products", options));
    }

    [Fact]
    public void ExecuteScalar_CallsTheMapper()
    {
        using SqliteConnection connection = Seed();

        var options = CommandOptions<string>.WithMapper(r => r.GetString(1));

        Assert.Equal("widget", connection.ExecuteScalar("SELECT id, name, price FROM products", options));
    }

    [Fact]
    public async Task QueryScalarAsync_CallsTheMapper()
    {
        using SqliteConnection connection = Seed();

        var options = CommandOptions<string>.WithMapper(r => r.GetString(1));

        Assert.Equal("widget", await connection.QueryScalarAsync(
            "SELECT id, name, price FROM products", options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteScalarAsync_CallsTheMapper()
    {
        using SqliteConnection connection = Seed();

        var options = CommandOptions<string>.WithMapper(r => r.GetString(1));

        Assert.Equal("widget", await connection.ExecuteScalarAsync(
            "SELECT id, name, price FROM products", options, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A mapper's whole point here is reaching columns <c>ExecuteScalar</c> cannot see - it returns
    /// the first column of the first row and nothing else. Reading column 2 is what proves the
    /// command ran as a reader.
    /// </summary>
    [Fact]
    public void TheMapper_SeesColumnsBeyondTheFirst()
    {
        using SqliteConnection connection = Seed();

        var options = CommandOptions<double>.WithMapper(r => r.GetDouble(2));

        Assert.Equal(150.0, connection.QueryScalar("SELECT id, name, price FROM products", options));
    }

    // ------------------------------------------------------------------
    // The mapper is called - interceptor-pipeline path
    // ------------------------------------------------------------------

    [Fact]
    public void QueryScalar_CallsTheMapper_WithInterceptorsRegistered()
    {
        RegisterAnInterceptor();

        using SqliteConnection connection = Seed();

        var options = CommandOptions<string>.WithMapper(r => r.GetString(1));

        Assert.Equal("widget", connection.QueryScalar("SELECT id, name, price FROM products", options));
    }

    [Fact]
    public async Task QueryScalarAsync_CallsTheMapper_WithInterceptorsRegistered()
    {
        RegisterAnInterceptor();

        using SqliteConnection connection = Seed();

        var options = CommandOptions<string>.WithMapper(r => r.GetString(1));

        Assert.Equal("widget", await connection.QueryScalarAsync(
            "SELECT id, name, price FROM products", options, TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------
    // Empty results, and everything that must not change
    // ------------------------------------------------------------------

    /// <summary>
    /// No rows yields default, matching what <c>ExecuteScalar</c> returns for an empty result set.
    /// The mapper is not called with a reader positioned before any row.
    /// </summary>
    [Fact]
    public void AnEmptyResultSet_ReturnsDefault_WithoutCallingTheMapper()
    {
        using SqliteConnection connection = Seed();

        bool called = false;
        var options = CommandOptions<string>.WithMapper(r => { called = true; return r.GetString(1); });

        Assert.Null(connection.QueryScalar("SELECT id, name FROM products WHERE id = 999", options));
        Assert.False(called);
    }

    [Fact]
    public async Task AnEmptyResultSetAsync_ReturnsDefault_WithoutCallingTheMapper()
    {
        using SqliteConnection connection = Seed();

        bool called = false;
        var options = CommandOptions<string>.WithMapper(r => { called = true; return r.GetString(1); });

        Assert.Null(await connection.QueryScalarAsync(
            "SELECT id, name FROM products WHERE id = 999", options, TestContext.Current.CancellationToken));
        Assert.False(called);
    }

    /// <summary>
    /// Without a mapper the path is unchanged: same <c>ExecuteScalar</c>, same conversion. These are
    /// the cases that would break if the reader path had been made unconditional.
    /// </summary>
    [Fact]
    public void WithoutAMapper_TheScalarPathIsUnchanged()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(1, connection.QueryScalar<int>("SELECT id FROM products"));
        Assert.Equal(1L, connection.QueryScalar<long>("SELECT id FROM products"));
        Assert.Equal("widget", connection.QueryScalar<string>("SELECT name FROM products"));
        Assert.Equal(150.0m, connection.QueryScalar<decimal>("SELECT price FROM products"));
    }

    [Fact]
    public void WithoutAMapper_AnEmptyResultSetStillReturnsDefault()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(0, connection.QueryScalar<int>("SELECT id FROM products WHERE id = 999"));
        Assert.Null(connection.QueryScalar<string>("SELECT name FROM products WHERE id = 999"));
    }

    [Fact]
    public void WithoutAMapper_ANullValueStillReturnsDefault()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(0, connection.QueryScalar<int>("SELECT NULL"));
    }

    private sealed class NoOpInterceptor : ICommandInterceptor
    {
        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken = default)
            => default;

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken = default)
            => default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken = default)
            => default;
    }
}
