using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Dialects;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Extrode.Jaunty.Tests.Integration.Write;

[Collection("Dialect Factory State")]
public class BulkInsertMultiRowPlaceholderTests : IDisposable
{
    private readonly PlaceholderSqliteConnection _connection;

    public BulkInsertMultiRowPlaceholderTests()
    {
        SqlDialectFactory.RegisterDialect<PlaceholderSqliteConnection>(new PostgreSqlDialect());
        _connection = new PlaceholderSqliteConnection();
        _connection.Open();
        using SqliteCommand create = _connection.CreateCommand();
        create.CommandText = "CREATE TABLE placeholder_rows (id INTEGER, \"price$usd\" INTEGER)";
        create.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqlDialectFactory.ResetRegistrations();
        GC.SuppressFinalize(this);
    }

    private sealed class PlaceholderSqliteConnection() : SqliteConnection("Data Source=:memory:");

    [Table("placeholder_rows")]
    public class PlaceholderRow
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("price$usd")]
        public long PriceUsd { get; set; }
    }

    private static readonly List<PlaceholderRow> TwoRows =
    [
        new() { Id = 1, PriceUsd = 10 },
        new() { Id = 2, PriceUsd = 20 },
    ];

    private long SumOfPrices()
    {
        using SqliteCommand sum = _connection.CreateCommand();
        sum.CommandText = "SELECT SUM(\"price$usd\") FROM placeholder_rows";
        return Convert.ToInt64(sum.ExecuteScalar());
    }

    [Fact]
    public void BulkInsert_MultiRow_ColumnNameThatIsNotAParameterName_BindsEveryPlaceholder()
    {
        int inserted = _connection.BulkInsert(TwoRows);

        Assert.Equal(2, inserted);
        Assert.Equal(30, SumOfPrices());
    }

    [Fact]
    public async Task BulkInsertAsync_MultiRow_ColumnNameThatIsNotAParameterName_BindsEveryPlaceholder()
    {
        int inserted = await _connection.BulkInsertAsync(TwoRows, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, inserted);
        Assert.Equal(30, SumOfPrices());
    }
}
