using Jaunty.Attributes;
using Jaunty.Core;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-136. <c>UpdateCore</c> and <c>UpdateCoreAsync</c> both open with the "no primary key
/// found or no columns to update" guard, and neither throw site had a test.
/// <c>CrudSqlCacheTests.GetSql_NoKeyEntity_UpdateSqlIsEmpty</c> asserts the empty statement that
/// triggers it and <c>GetCoreTests.Get_NoPrimaryKey_ThrowsWithInterpolatedTypeName</c> covers the
/// read-side analogue, but nothing called <c>Update</c> with a keyless entity, so neither the
/// message nor the exception type was pinned.
/// </summary>
public class UpdateCoreGuardTests
{
    [Table("keyless_rows")]
    public class KeylessRow
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE keyless_rows (name TEXT);";
        seed.ExecuteNonQuery();

        return connection;
    }

    private const string Expected =
        "Cannot update entity of type 'KeylessRow': No primary key found or no columns to update.";

    [Fact]
    public void AKeylessEntity_CannotBeUpdated_AndSaysWhy()
    {
        using SqliteConnection connection = Open();

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.Update(new KeylessRow { Name = "a" }));

        Assert.Equal(Expected, ex.Message);
    }

    [Fact]
    public async Task AKeylessEntityAsync_CannotBeUpdated_AndSaysWhy()
    {
        using SqliteConnection connection = Open();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await connection.UpdateAsync(new KeylessRow { Name = "a" }, default, TestContext.Current.CancellationToken));

        Assert.Equal(Expected, ex.Message);
    }
}
