using DuckDB.NET.Data;

using Jaunty.Configuration;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.Interceptors;

namespace Jaunty.FlatFiles.DuckDB.Tests.Unit;

/// <summary>
/// AUD-R35-251. <c>NonQueryExecutor</c> called <c>DuckDbObservation.Describe(parameters)</c> twice
/// per execution on both the sync and async paths - once to build the argument for
/// <c>CommandObservation.Execute</c> and again inside <c>ExecuteDirect</c> for
/// <c>CommandObservation.Log</c>. <c>Describe</c> allocates a dictionary sized to the parameter
/// list, so every non-query built two identical ones and the logger and an interceptor were handed
/// different objects describing the same command.
/// </summary>
[Collection(GlobalInterceptorStateCollection.Name)]
public class NonQueryParameterDescriptionSharingTests : IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly RecordingInterceptor _interceptor = new();
    private readonly List<object?> _logged = [];

    public NonQueryParameterDescriptionSharingTests()
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        using (DuckDBCommand cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE audited (id INTEGER, name TEXT)";
            cmd.ExecuteNonQuery();
        }

        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(_interceptor);
        JauntyConfig.Logger = (_, parameters) => _logged.Add(parameters);
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = null;
        _connection.Dispose();
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

    private static List<DuckDBParameter> Row(int id, string name)
        => [new() { Value = id }, new() { Value = name }];

    private const string Sql = "INSERT INTO audited (id, name) VALUES ($1, $2)";

    [Fact]
    public void Execute_HandsTheInterceptorAndTheLoggerTheSameParameterSet()
    {
        NonQueryExecutor.Execute(_connection, Sql, Row(1, "a"));

        object? intercepted = Assert.Single(_interceptor.Parameters);
        object? logged = Assert.Single(_logged);

        Assert.Same(intercepted, logged);
    }

    [Fact]
    public async Task ExecuteAsync_HandsTheInterceptorAndTheLoggerTheSameParameterSet()
    {
        await NonQueryExecutor.ExecuteAsync(_connection, Sql, Row(2, "b"), default);

        object? intercepted = Assert.Single(_interceptor.Parameters);
        object? logged = Assert.Single(_logged);

        Assert.Same(intercepted, logged);
    }

    [Fact]
    public void TheSharedSet_StillDescribesEveryParameter()
    {
        NonQueryExecutor.Execute(_connection, Sql, Row(3, "c"));

        var described = (IReadOnlyDictionary<string, object?>)Assert.Single(_logged)!;

        Assert.Equal(2, described.Count);
        Assert.Contains(3, described.Values);
        Assert.Contains("c", described.Values);
    }

    [Fact]
    public void TwoExecutions_GetTheirOwnParameterSets()
    {
        NonQueryExecutor.Execute(_connection, Sql, Row(4, "d"));
        NonQueryExecutor.Execute(_connection, Sql, Row(5, "e"));

        Assert.Equal(2, _logged.Count);
        Assert.NotSame(_logged[0], _logged[1]);
    }

    [Fact]
    public void TheRowsStillLand()
    {
        Assert.Equal(1, NonQueryExecutor.Execute(_connection, Sql, Row(6, "f")));

        using DuckDBCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM audited WHERE id = 6";

        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }
}
