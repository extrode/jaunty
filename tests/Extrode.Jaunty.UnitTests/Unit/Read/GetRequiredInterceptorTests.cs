using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Read;

[Collection(ConfigurationGenerationCollection.Name)]
public class GetRequiredInterceptorTests
{
    [Table("class_row")]
    public sealed class ClassRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    private sealed class OutcomeInterceptor : ICommandInterceptor
    {
        public int Executed { get; private set; }

        public List<Exception> Failed { get; } = [];

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed++;
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            Failed.Add(exception);
            return default;
        }
    }

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE class_row (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO class_row VALUES (1, 'one');";
        seed.ExecuteNonQuery();

        return connection;
    }

    [Fact]
    public void GetRequired_Miss_IsACompletedCommandNotAFailedOne()
    {
        using SqliteConnection connection = Seed();
        var interceptor = new OutcomeInterceptor();
        JauntyConfig.AddInterceptor(interceptor);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => connection.GetRequired<ClassRow>(99));
            Assert.Equal("Entity of type 'ClassRow' with ID '99' not found.", ex.Message);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }

        Assert.Empty(interceptor.Failed);
        Assert.Equal(1, interceptor.Executed);
    }

    [Fact]
    public async Task GetRequiredAsync_Miss_IsACompletedCommandNotAFailedOne()
    {
        using SqliteConnection connection = Seed();
        var interceptor = new OutcomeInterceptor();
        JauntyConfig.AddInterceptor(interceptor);
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await connection.GetRequiredAsync<ClassRow>(99, TestContext.Current.CancellationToken));
            Assert.Equal("Entity of type 'ClassRow' with ID '99' not found.", ex.Message);
        }
        finally
        {
            JauntyConfig.ClearInterceptors();
        }

        Assert.Empty(interceptor.Failed);
        Assert.Equal(1, interceptor.Executed);
    }

    [Fact]
    public void GetRequired_Hit_ReturnsTheRow()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal("one", connection.GetRequired<ClassRow>(1).Name);
    }
}
