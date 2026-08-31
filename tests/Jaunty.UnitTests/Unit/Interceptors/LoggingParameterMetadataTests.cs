using System.Data;

using Microsoft.Extensions.Logging;

using Jaunty.Configuration;
using Jaunty.Interceptors;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// LoggingInterceptor used to run its own reflection pass over the parameters type - every public
/// instance property - which was both the extension's last reflection site and a second, divergent
/// definition of "what counts as a parameter". It now reads the metadata core binds from.
///
/// These tests pin the two behaviours that divergence produced.
/// </summary>
public class LoggingParameterMetadataTests
{
    private sealed class SetOnlyParameters
    {
        public int Id { get; set; }

        // No getter. The old scan took this from GetProperties and called GetValue on it, which
        // throws ArgumentException - so logging a parameters object that happened to declare a
        // set-only property brought down the query it was logging.
        public string WriteOnly { set { } }
    }

    private sealed class IndexedParameters
    {
        public int Id { get; set; }

        // Surfaces as a public instance property named "Item". The old scan skipped it explicitly;
        // ParameterCache skips it for the same reason, so the exclusion has to survive the move.
        public object this[int index] => index;
    }

    private sealed class CapturingLogger : ILogger<LoggingInterceptor>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }

    private static async Task<string> LogFor(object parameters)
    {
        var logger = new CapturingLogger();
        var config = new LoggingConfiguration { LogSql = true, LogParameters = true };
        var interceptor = new LoggingInterceptor(logger, config);

        var context = new CommandContext(
            "SELECT 1",
            parameters,
            new StubConnection(),
            CommandType.Text);

        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        return string.Join("\n", logger.Messages);
    }

    [Fact]
    public async Task ASetOnlyPropertyIsSkippedInsteadOfThrowing()
    {
        string log = await LogFor(new SetOnlyParameters { Id = 7 });

        Assert.Contains("Id=7", log, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteOnly", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIndexerIsNotLoggedAsAParameter()
    {
        string log = await LogFor(new IndexedParameters { Id = 3 });

        Assert.Contains("Id=3", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Item=", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrdinaryPropertiesStillLog()
    {
        // Guards against the two tests above passing because nothing is logged at all.
        string log = await LogFor(new { Name = "Chai", Price = 18 });

        // Strings are quoted by FormatParameterValue; numbers are not.
        Assert.Contains("Name=\"Chai\"", log, StringComparison.Ordinal);
        Assert.Contains("Price=18", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADictionaryStillLogsThroughTheDictionaryPath()
    {
        // The dictionary branch never used reflection and must be unaffected by the change.
        string log = await LogFor(new Dictionary<string, object> { ["id"] = 42 });

        Assert.Contains("id=42", log, StringComparison.Ordinal);
    }
}
