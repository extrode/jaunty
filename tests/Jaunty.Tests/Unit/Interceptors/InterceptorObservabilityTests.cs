using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Diagnostics;
using Jaunty.Extensions.Reflection;
using Jaunty.Interceptors;
using Jaunty.Interfaces;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R26 (batch 4 / 5 / 7, filed four times, fixed as one cluster). Whether a Jaunty command is
/// observable was decided by thirty independently-written call-site guards, and they all tested the
/// wrong thing.
///
/// <para>
/// AUD-R25-013 added <see cref="InterceptorPipeline.IsObserved"/> so that subscribing to the
/// "Jaunty" diagnostic source without registering an <see cref="ICommandInterceptor"/> would emit
/// events. It never took effect for a single ordinary Jaunty API, because every call site returned
/// early on <c>HasInterceptors</c> before the pipeline was consulted. The ten tests written for it
/// all constructed an <see cref="InterceptorPipeline"/> directly, so none of them crossed a guard.
/// </para>
///
/// <para>
/// <b>And swapping the predicate was not enough.</b> <c>JauntyConfig.InterceptorPipeline</c> is
/// <see langword="null"/> until <c>AddInterceptor</c> is called - it is never constructed otherwise
/// - so in the exact scenario the finding describes, "subscriber but no interceptor", there is no
/// pipeline object to ask. <c>pipeline?.IsObserved == true</c> is false because of the null, not
/// because of the predicate. The fix the finding implies would have left the defect in place, which
/// is why these tests go through the public API rather than through the pipeline.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class InterceptorObservabilityTests : IDisposable
{
    private readonly RecordingObserver _observer = new();
    private readonly IDisposable _subscription;

    public InterceptorObservabilityTests()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
        JauntyConfig.ClearInterceptors();
        _subscription = JauntyDiagnosticListener.Instance.Subscribe(_observer);
    }

    public void Dispose()
    {
        _subscription.Dispose();
        JauntyConfig.ClearInterceptors();
        GC.SuppressFinalize(this);
    }

    private sealed class RecordingObserver : IObserver<KeyValuePair<string, object?>>
    {
        public List<string> Events { get; } = [];

        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(KeyValuePair<string, object?> value) => Events.Add(value.Key);
    }

    private sealed class CountingInterceptor : ICommandInterceptor
    {
        public int Executing { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executing++;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    [Table("widgets")]
    public class Widget : IEntity<int>
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }
    }

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE widgets (Id INTEGER PRIMARY KEY, Name TEXT);" +
            "INSERT INTO widgets VALUES (1, 'sprocket');";
        create.ExecuteNonQuery();

        return connection;
    }

    private void AssertSawACommand(string what)
    {
        Assert.True(
            _observer.Events.Contains(JauntyDiagnosticListener.CommandExecutingEventName),
            $"A subscriber to the 'Jaunty' diagnostic source with no ICommandInterceptor registered " +
            $"received no Executing event for {what}. Events seen: [{string.Join(", ", _observer.Events)}].");

        Assert.Contains(JauntyDiagnosticListener.CommandExecutedEventName, _observer.Events);
    }

    // ------------------------------------------------------------------
    // The defect, measured through the public API - a diagnostics subscriber
    // with no interceptor registered
    // ------------------------------------------------------------------

    [Fact]
    public void AQueryIsVisibleToADiagnosticsSubscriberWithNoInterceptor()
    {
        using SqliteConnection connection = Seed();

        List<Widget> widgets = connection.Query<Widget>("SELECT Id, Name FROM widgets");

        Assert.Single(widgets);
        AssertSawACommand("Query<T>");
    }

    [Fact]
    public void GetAllIsVisibleToADiagnosticsSubscriberWithNoInterceptor()
    {
        using SqliteConnection connection = Seed();

        List<Widget> widgets = connection.GetAll<Widget>();

        Assert.Single(widgets);
        AssertSawACommand("GetAll<T>");
    }

    [Fact]
    public void GetByIdIsVisibleToADiagnosticsSubscriberWithNoInterceptor()
    {
        using SqliteConnection connection = Seed();

        Widget? widget = connection.Get<Widget>(1);

        Assert.Equal("sprocket", widget?.Name);
        AssertSawACommand("Get<T>(id)");
    }

    [Fact]
    public void AWriteIsVisibleToADiagnosticsSubscriberWithNoInterceptor()
    {
        using SqliteConnection connection = Seed();

        connection.Insert(new Widget { Id = 2, Name = "flange" });

        AssertSawACommand("Insert<T>");
    }

    [Fact]
    public async Task TheAsyncPathIsVisibleToADiagnosticsSubscriberWithNoInterceptor()
    {
        using SqliteConnection connection = Seed();

        List<Widget> widgets = await connection.QueryAsync<Widget>(
            "SELECT Id, Name FROM widgets", TestContext.Current.CancellationToken);

        Assert.Single(widgets);
        AssertSawACommand("QueryAsync<T>");
    }

    // ------------------------------------------------------------------
    // Controls - what already worked must keep working
    // ------------------------------------------------------------------

    [Fact]
    public void ARegisteredInterceptorStillSeesTheCommand()
    {
        var interceptor = new CountingInterceptor();
        JauntyConfig.AddInterceptor(interceptor);

        using SqliteConnection connection = Seed();
        _ = connection.Query<Widget>("SELECT Id, Name FROM widgets");

        Assert.Equal(1, interceptor.Executing);
        AssertSawACommand("Query<T> with an interceptor registered");
    }

    // ------------------------------------------------------------------
    // Anti-drift: the shape that caused this must not come back
    // ------------------------------------------------------------------

    /// <summary>
    /// The reason this defect existed for two audit rounds is that the observation test was
    /// copyable, and it got copied thirty times. It was still spreading during round 26 itself -
    /// the fix for the batch-2 write-path finding added two fresh copies, taking the count from 29
    /// to 30 while the finding against it was open.
    ///
    /// <para>
    /// So the fix is not only "correct the thirty guards", it is "make the thirty-first
    /// impossible". <c>HasInterceptors</c> answers a narrower question than any call site wants -
    /// it cannot see a diagnostics subscriber - and outside
    /// <see cref="InterceptorPipeline"/> there is no legitimate reason to ask it. Callers ask
    /// <c>CommandObservation.Observer</c> instead, which is the only thing that knows the whole
    /// answer.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingOutsideThePipelineTestsHasInterceptors()
    {
        DirectoryInfo source = LocateSourceRoot();

        List<string> offenders = [];

        foreach (string file in Directory.EnumerateFiles(source.FullName, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            if (Path.GetFileName(file) is "InterceptorPipeline.cs" or "CommandObservation.cs")
                continue;

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();

                // Prose may discuss it; code may not.
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
                    continue;

                if (lines[i].IndexOf("HasInterceptors", StringComparison.Ordinal) >= 0)
                    offenders.Add($"{file.Substring(source.FullName.Length).TrimStart(Path.DirectorySeparatorChar)}:{i + 1}");
            }
        }

        Assert.True(offenders.Count == 0,
            "HasInterceptors cannot see a diagnostics subscriber, so a call site that tests it is the " +
            "exact defect AUD-R26 closed - ask CommandObservation.Observer instead. Found at: " +
            string.Join(", ", offenders));
    }

    /// <summary>Walks up from the test binary to the repository's <c>src</c> directory.</summary>
    private static DirectoryInfo LocateSourceRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "Jaunty", "Interceptors", "InterceptorPipeline.cs");
            if (File.Exists(candidate))
                return new DirectoryInfo(Path.Combine(dir.FullName, "src"));

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository's src directory walking up from '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// The fast path has to stay a fast path: with nothing subscribed and nothing registered, no
    /// pipeline is entered and no closure is allocated for it. Asserted indirectly - the query
    /// still returns the right answer and the observer, which is not subscribed here, sees nothing.
    /// </summary>
    [Fact]
    public void WithNothingObservingTheQueryStillRuns()
    {
        _subscription.Dispose();

        var unsubscribed = new RecordingObserver();
        using SqliteConnection connection = Seed();

        List<Widget> widgets = connection.Query<Widget>("SELECT Id, Name FROM widgets");

        Assert.Single(widgets);
        Assert.Empty(unsubscribed.Events);
    }
}
