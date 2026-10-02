using Microsoft.Data.Sqlite;

using Extrode.Jaunty.Scaffolding.Configuration;

namespace Extrode.Jaunty.Scaffolding.Tests.Integration;

public class ScaffolderSynchronizationContextTests : IDisposable
{
    private const int TableCount = 40;

    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly string _outputDir;

    public ScaffolderSynchronizationContextTests()
    {
        _connectionString = $"Data Source=SyncContext_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        _outputDir = Path.Combine(Path.GetTempPath(), $"JauntySyncContext_{Guid.NewGuid():N}");

        for (int i = 0; i < TableCount; i++)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = $"CREATE TABLE widget_{i} (id INTEGER PRIMARY KEY, name TEXT)";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class CountingContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }

    private static ScaffoldResult RunUnder(SynchronizationContext context, ScaffoldOptions options)
        => Task.Run(() =>
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                return new Scaffolder().ScaffoldAsync(options).GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }).GetAwaiter().GetResult();

    [Fact]
    public void ScaffoldAsync_WritingFiles_NeverPostsBackToTheCallersContext()
    {
        var context = new CountingContext();
        var options = new ScaffoldOptions
        {
            ConnectionString = _connectionString,
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = _outputDir,
            Namespace = "Test.Entities"
        };

        ScaffoldResult result = RunUnder(context, options);

        Assert.True(result.Success, result.Error);
        Assert.Equal(TableCount, result.GeneratedFiles.Count);
        Assert.Equal(0, context.Posts);
    }
}
