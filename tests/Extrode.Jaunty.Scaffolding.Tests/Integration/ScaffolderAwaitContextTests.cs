using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Configuration;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Schema;
using Extrode.Jaunty.Scaffolding.Tests.Helpers;

namespace Extrode.Jaunty.Scaffolding.Tests.Integration;

public class ScaffolderAwaitContextTests
{
    private sealed class SlowEmptyReader : ISchemaReader
    {
        public async Task<DatabaseSchema> ReadSchemaAsync(
            string connectionString,
            SchemaReaderOptions options,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);
            return new DatabaseSchema { DatabaseName = "slow", Tables = [] };
        }
    }

    private static Scaffolder Slow() =>
        new(_ => (new SlowEmptyReader(), new SQLiteTypeMapper()));

    [Fact]
    public void ScaffoldAsync_AwaitingTheReader_NeverPostsBackToTheCallersContext()
    {
        var context = new CountingContext();
        var options = new ScaffoldOptions
        {
            ConnectionString = "Data Source=x.db",
            Provider = DatabaseProvider.SQLite,
            OutputDirectory = "out",
            Namespace = "Test.Entities"
        };

        ScaffoldResult result = CountingContext.Run(context, () => Slow().ScaffoldAsync(options));

        Assert.False(result.Success);
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public void ListTablesAsync_AwaitingTheReader_NeverPostsBackToTheCallersContext()
    {
        var context = new CountingContext();

        IReadOnlyList<(string Schema, string Table)> tables = CountingContext.Run(
            context, () => Slow().ListTablesAsync("Data Source=x.db", DatabaseProvider.SQLite, null));

        Assert.Empty(tables);
        Assert.Equal(0, context.Posts);
    }
}
