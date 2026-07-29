using System.Data;

using Jaunty.Configuration;
using Jaunty.Interceptors;

using Xunit;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R26 (batch 4, medium/security). Sensitive-parameter masking matched parameter names by
/// <em>exact</em> (case-insensitive) equality, and <c>LogParameters</c> defaults to
/// <see langword="true"/>. So the seven seeded names masked a parameter spelled exactly that way
/// and nothing else - and the names real code actually uses do not match.
///
/// <para>
/// <c>connection.Execute(sql, new { UserId = 1, NewPassword = "hunter2" })</c> logged
/// <c>NewPassword="hunter2"</c> in plaintext at Information level, out of the box, with nothing in
/// the documentation to suggest matching was exact. That the interceptor carried a
/// <c>StripProviderPrefix</c> helper - added so <c>@Password</c> also matches - shows the intent was
/// to match what callers mean, which makes the absence of any prefix/suffix handling an oversight
/// rather than a decision.
/// </para>
///
/// <para>
/// The theory below is the finding's own list of names. Every one of them is a real spelling from
/// real schemas, and every one of them logged in clear.
/// </para>
/// </summary>
public class SensitiveParameterMaskingTests
{
    private const string Masked = "***MASKED***";

    private static async Task<string> LogLineFor(object parameters, LoggingConfiguration? config = null)
    {
        var logger = new CapturingLogger();
        var interceptor = new LoggingInterceptor(logger, config ?? new LoggingConfiguration());

        var context = new CommandContext(
            "INSERT INTO probe VALUES (...)", parameters, new StubConnection(), CommandType.Text);

        await interceptor.OnCommandExecutingAsync(context, CancellationToken.None);

        Assert.Single(logger.Messages);
        return logger.Messages[0];
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<LoggingInterceptor>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    /// <summary>Nothing here executes; the interceptor only reads the connection for context.</summary>
    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "stub";
        public int ConnectionTimeout => 0;
        public string Database => "stub";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }

    // ------------------------------------------------------------------
    // The defect: real-world spellings of the seeded names
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("NewPassword")]
    [InlineData("PasswordHash")]
    [InlineData("UserPassword")]
    [InlineData("password_hash")]
    [InlineData("AccessToken")]
    [InlineData("RefreshToken")]
    [InlineData("TokenValue")]
    [InlineData("ClientSecret")]
    [InlineData("Api_Key")]
    [InlineData("apiKeyValue")]
    [InlineData("PrivateKeyPem")]
    [InlineData("user_credentials")]
    public async Task ARealWorldSpellingOfASeededNameIsMasked(string parameterName)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["UserId"] = 1,
            [parameterName] = "hunter2",
        };

        string line = await LogLineFor(parameters);

        Assert.False(line.IndexOf("hunter2", StringComparison.Ordinal) >= 0,
            $"Parameter '{parameterName}' logged its value in clear. Matching was exact, so only a " +
            $"parameter spelled exactly like a seeded name was masked. Logged: {line}");

        Assert.Contains(Masked, line, StringComparison.Ordinal);
    }

    /// <summary>The provider prefix was already handled; it must keep working alongside the rest.</summary>
    [Theory]
    [InlineData("@NewPassword")]
    [InlineData(":AccessToken")]
    [InlineData("$ClientSecret")]
    public async Task APrefixedRealWorldSpellingIsMasked(string parameterName)
    {
        var parameters = new Dictionary<string, object?> { [parameterName] = "hunter2" };

        string line = await LogLineFor(parameters);

        Assert.DoesNotContain("hunter2", line, StringComparison.Ordinal);
        Assert.Contains(Masked, line, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Controls - masking must stay useful, not become total
    // ------------------------------------------------------------------

    /// <summary>
    /// Widening the match is the safe direction, but it must not swallow ordinary columns. A log
    /// that masks everything is as useless as one that masks nothing.
    /// </summary>
    [Theory]
    [InlineData("UserId")]
    [InlineData("OrderDate")]
    [InlineData("ProductName")]
    [InlineData("Quantity")]
    public async Task AnOrdinaryParameterIsStillLogged(string parameterName)
    {
        var parameters = new Dictionary<string, object?> { [parameterName] = "visible-value" };

        string line = await LogLineFor(parameters);

        Assert.Contains("visible-value", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reason matching is by <em>word</em> and not by substring. Each of these contains a seeded
    /// name as a run of characters but not as a word, and each is an ordinary column whose value is
    /// worth reading. A plain <c>IndexOf</c> masks all of them - which is how masking earns a
    /// reputation for being noise and gets switched off wholesale.
    /// </summary>
    [Theory]
    [InlineData("TokenizerVersion")]   // contains "Token"
    [InlineData("SecretariatId")]      // contains "Secret"
    [InlineData("PasswordlessLogin")]  // contains "Password"
    public async Task ASeededNameEmbeddedInALongerWordIsNotMasked(string parameterName)
    {
        var parameters = new Dictionary<string, object?> { [parameterName] = "visible-value" };

        string line = await LogLineFor(parameters);

        Assert.Contains("visible-value", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Masked, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A multi-word seeded name matches as a consecutive run, so it does not fire just because both
    /// of its words appear somewhere in the parameter name.
    /// </summary>
    [Fact]
    public async Task AMultiWordSeededNameOnlyMatchesAsAConsecutiveRun()
    {
        // "ApiKey" is seeded. This has "Api" and "Key" as words, but not adjacent.
        string line = await LogLineFor(
            new Dictionary<string, object?> { ["ApiRequestKey"] = "visible-value" });

        Assert.Contains("visible-value", line, StringComparison.Ordinal);
    }

    /// <summary>An all-caps acronym still splits, so <c>APIKey</c> is <c>API|Key</c>.</summary>
    [Theory]
    [InlineData("APIKey")]
    [InlineData("USER_PASSWORD")]
    [InlineData("ACCESS_TOKEN")]
    public async Task AnAcronymOrScreamingSnakeSpellingIsStillMasked(string parameterName)
    {
        string line = await LogLineFor(
            new Dictionary<string, object?> { [parameterName] = "hunter2" });

        Assert.DoesNotContain("hunter2", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// The known limitation, pinned deliberately rather than left to be discovered. Word matching
    /// needs a boundary to find, and an unbroken single-case run has none: <c>USERPASSWORD</c> is
    /// indistinguishable from one long word. Every boundary-bearing spelling of the same column -
    /// <c>USER_PASSWORD</c>, <c>UserPassword</c>, <c>user_password</c> - does match. A schema that
    /// runs words together needs the name added explicitly.
    ///
    /// <para>
    /// Substring matching would cover this case, at the cost of masking <c>TOKENIZERVERSION</c> and
    /// every other embedded run. This is the side of that trade that keeps the log readable. Note
    /// the escape hatch has to be spelled the same unbroken way - adding "UserPassword" does not
    /// help, because that splits into two words and the candidate is one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AnUnbrokenSingleCaseRunIsNotMatchedUnlessAddedVerbatim()
    {
        var parameters = new Dictionary<string, object?> { ["USERPASSWORD"] = "hunter2" };

        Assert.Contains("hunter2", await LogLineFor(parameters), StringComparison.Ordinal);

        var splitName = new LoggingConfiguration();
        splitName.SensitiveParameterNames.Add("UserPassword");
        Assert.Contains("hunter2", await LogLineFor(parameters, splitName), StringComparison.Ordinal);

        var verbatim = new LoggingConfiguration();
        verbatim.SensitiveParameterNames.Add("USERPASSWORD");
        Assert.DoesNotContain("hunter2", await LogLineFor(parameters, verbatim), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExactlySpelledSeededNamesStillMask()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Password"] = "a",
            ["Secret"] = "b",
            ["Token"] = "c",
            ["ApiKey"] = "d",
            ["ApiSecret"] = "e",
            ["Credentials"] = "f",
            ["PrivateKey"] = "g",
        };

        string line = await LogLineFor(parameters);

        foreach (string value in new[] { "\"a\"", "\"b\"", "\"c\"", "\"d\"", "\"e\"", "\"f\"", "\"g\"" })
            Assert.DoesNotContain(value, line, StringComparison.Ordinal);
    }

    /// <summary>A name the caller adds must get the same treatment as a seeded one.</summary>
    [Fact]
    public async Task ACallerSuppliedNameAlsoMatchesByWord()
    {
        var config = new LoggingConfiguration();
        config.SensitiveParameterNames.Add("Ssn");

        string line = await LogLineFor(
            new Dictionary<string, object?> { ["EmployeeSsnValue"] = "123-45-6789" }, config);

        Assert.DoesNotContain("123-45-6789", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Widening the match is a behaviour change, so it has an off switch for anyone who was
    /// relying on exact matching deliberately.
    /// </summary>
    [Fact]
    public async Task ExactMatchingCanBeRestored()
    {
        var config = new LoggingConfiguration
        {
            SensitiveParameterMatching = SensitiveParameterMatching.Exact,
        };

        string line = await LogLineFor(
            new Dictionary<string, object?> { ["NewPassword"] = "hunter2" }, config);

        Assert.Contains("hunter2", line, StringComparison.Ordinal);
    }
}
