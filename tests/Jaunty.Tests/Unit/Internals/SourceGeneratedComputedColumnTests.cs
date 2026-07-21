using System.Data.SQLite;

using Jaunty.Internals.Write;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Unit.Internals;

// AUD-R14: the source generator hardcoded IsComputed = false for every source-generated column,
// so [DatabaseGenerated(Computed)] properties on partial (source-generated) entities were
// included in generated INSERT/UPDATE SQL - unlike the reflection path (CrudSqlCacheTests'
// ComputedItem), which already derived IsComputed correctly from DatabaseGeneratedOption.
public class SourceGeneratedComputedColumnTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public SourceGeneratedComputedColumnTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetSql_ComputedColumnTestEntity_ExcludesComputedFromInsert()
    {
        var sql = CrudSqlCache.GetSql<ComputedColumnTestEntity>(_connection);

        Assert.DoesNotContain("computed_value", sql.InsertSql);
        Assert.Contains("name", sql.InsertSql);
    }

    [Fact]
    public void GetSql_ComputedColumnTestEntity_ExcludesComputedFromUpdate()
    {
        var sql = CrudSqlCache.GetSql<ComputedColumnTestEntity>(_connection);

        Assert.DoesNotContain("computed_value", sql.UpdateSql);
        Assert.Contains("name", sql.UpdateSql);
    }
}
