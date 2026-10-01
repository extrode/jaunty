using System.Linq.Expressions;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R38-024: the <c>Update</c> column selector took the leaf member of any member chain, so a
/// navigation or a captured value named a root column and a lifted <c>.Value</c> named "Value".
/// </summary>
public class ResolveColumnNameChainTests : IDisposable
{
    public class Address
    {
        public string City { get; set; } = string.Empty;
    }

    public class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int? Score { get; set; }
        public Address Child { get; set; } = new();
    }

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r38_selector_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public ResolveColumnNameChainTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "rows.csv");
        File.WriteAllText(csvPath, "Id,Name,City,Score\n1,a,north,3\n2,b,south,\n");

        var options = new FlatFileOptions();
        options.AddCsv<Row>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static string Resolve(Expression<Func<Row, object>> selector) => ExpressionTranslator.ResolveColumnName(selector);

    [Fact]
    public void LiftedValue_NamesTheNullableProperty()
    {
        Assert.Equal("Score", Resolve(r => r.Score!.Value));
    }

    [Fact]
    public void Navigation_IsRefused()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Resolve(r => r.Child.City));

        Assert.Contains("rather than directly off the entity", ex.Message);
    }

    [Fact]
    public void CapturedValue_IsRefused()
    {
        var other = new Row();

        var ex = Assert.Throws<NotSupportedException>(() => Resolve(r => other.Name));

        Assert.Contains("does not read a property of the entity", ex.Message);
    }

    [Fact]
    public void DirectProperty_StillResolves()
    {
        Assert.Equal("City", Resolve(r => r.City));
    }

    [Fact]
    public void NonMemberSelector_StillThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Resolve(r => r.Name + "x"));
    }

    [Fact]
    public void Update_ThroughALiftedValue_SetsTheNullableColumn()
    {
        Assert.Equal(1, _db.Update<Row>(r => r.Id == 2, r => r.Score!.Value, 9));

        Assert.Equal(9, _db.Query<Row>("SELECT * FROM \"row\" WHERE Id = 2").Single().Score);
    }

    [Fact]
    public void Update_ThroughANavigation_LeavesTheRootColumnAlone()
    {
        Assert.Throws<NotSupportedException>(() => _db.Update<Row>(r => r.Id == 1, r => r.Child.City, "x"));

        Assert.Equal("north", _db.Query<Row>("SELECT * FROM \"row\" WHERE Id = 1").Single().City);
    }

    [Fact]
    public async Task UpdateAsync_ThroughACapturedValue_IsRefused()
    {
        var other = new Row();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await _db.UpdateAsync<Row>(r => r.Id == 1, r => other.Name, "x"));
    }
}
