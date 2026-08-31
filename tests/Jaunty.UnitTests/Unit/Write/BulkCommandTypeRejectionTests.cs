using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-101 (round-35 batch 03b). The six Bulk* cores reported <c>options.CommandType</c> to
/// <c>CommandObservation</c> and then never applied it, so an interceptor or DiagnosticListener
/// could be told a bulk delete ran as <see cref="CommandType.StoredProcedure"/> while the command
/// that executed was <see cref="CommandType.Text"/>. Bulk operations run SQL Jaunty generated from
/// entity metadata, so there is no procedure for a provider to resolve; the option is rejected
/// rather than propagated.
/// </summary>
public class BulkCommandTypeRejectionTests
{
    [Table("bulk_cmdtype_test")]
    private class Entity
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private static SQLiteConnection OpenConnection()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE bulk_cmdtype_test (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL)";
        create.ExecuteNonQuery();

        return connection;
    }

    private static List<Entity> TwoRows =>
    [
        new() { Name = "one" },
        new() { Name = "two" },
    ];

    [Fact]
    public void BulkInsert_WithStoredProcedure_Throws()
    {
        using SQLiteConnection connection = OpenConnection();

        var ex = Assert.Throws<ArgumentException>(() => connection.BulkInsert(TwoRows, CommandOptions.AsStoredProcedure()));

        Assert.Contains("BulkInsert", ex.Message);
        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public async Task BulkInsertAsync_WithStoredProcedure_Throws()
    {
        using SQLiteConnection connection = OpenConnection();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await connection.BulkInsertAsync(TwoRows, CommandOptions.AsStoredProcedure()));

        Assert.Contains("BulkInsertAsync", ex.Message);
    }

    [Fact]
    public void BulkUpdate_WithStoredProcedure_Throws()
    {
        using SQLiteConnection connection = OpenConnection();
        connection.BulkInsert(TwoRows);

        var ex = Assert.Throws<ArgumentException>(
            () => connection.BulkUpdate(connection.GetAll<Entity>(), CommandOptions.AsStoredProcedure()));

        Assert.Contains("BulkUpdate", ex.Message);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithStoredProcedure_Throws()
    {
        using SQLiteConnection connection = OpenConnection();
        connection.BulkInsert(TwoRows);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await connection.BulkUpdateAsync(connection.GetAll<Entity>(), CommandOptions.AsStoredProcedure()));

        Assert.Contains("BulkUpdateAsync", ex.Message);
    }

    [Fact]
    public void BulkDelete_WithStoredProcedure_Throws()
    {
        using SQLiteConnection connection = OpenConnection();
        connection.BulkInsert(TwoRows);

        var ex = Assert.Throws<ArgumentException>(
            () => connection.BulkDelete(connection.GetAll<Entity>(), CommandOptions.AsStoredProcedure()));

        Assert.Contains("BulkDelete", ex.Message);
    }

    [Fact]
    public async Task BulkDeleteAsync_WithStoredProcedure_Throws()
    {
        using SQLiteConnection connection = OpenConnection();
        connection.BulkInsert(TwoRows);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await connection.BulkDeleteAsync(connection.GetAll<Entity>(), CommandOptions.AsStoredProcedure()));

        Assert.Contains("BulkDeleteAsync", ex.Message);
    }

    // TableDirect is the other value the allow-list rejects, and it reaches the same validator by a
    // different constructor, so one case is enough to say the check is not StoredProcedure-specific.
    [Fact]
    public void BulkInsert_WithTableDirect_Throws()
    {
        using SQLiteConnection connection = OpenConnection();

        Assert.Throws<ArgumentException>(
            () => connection.BulkInsert(TwoRows, new CommandOptions(commandType: CommandType.TableDirect)));
    }

    // The controls. default(CommandOptions).CommandType is 0, not CommandType.Text (1), and every
    // caller that omits options passes default - so if the validator treated only Text as valid it
    // would reject every ordinary bulk call, and these would fail rather than the tests above.
    [Fact]
    public void BulkInsert_WithDefaultOptions_Inserts()
    {
        using SQLiteConnection connection = OpenConnection();

        Assert.Equal(2, connection.BulkInsert(TwoRows));
        Assert.Equal(2, connection.GetAll<Entity>().Count());
    }

    [Fact]
    public void BulkInsert_WithExplicitTextCommandType_Inserts()
    {
        using SQLiteConnection connection = OpenConnection();

        Assert.Equal(2, connection.BulkInsert(TwoRows, new CommandOptions(commandType: CommandType.Text)));
    }
}
