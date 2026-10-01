namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Core;

public class DuckDbDatabasePathTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_dbpath_{Guid.NewGuid():N}");

    public DuckDbDatabasePathTests() => Directory.CreateDirectory(_dataDir);

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    [Fact]
    public void DatabasePath_WithASemicolonInADirectory_OpensThatFile()
    {
        string directory = Path.Combine(_dataDir, "a;b");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "x.duckdb");

        using (var db = new DuckDb(new FlatFileOptions { DatabasePath = path }))
            Assert.Equal(1, db.Connection.ExecuteScalar<int>("SELECT 1"));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DatabasePath_WithATrailingSettingLikeSuffix_IsTakenAsTheFileName()
    {
        string plain = Path.Combine(_dataDir, "x.duckdb");
        string path = plain + ";threads=1";

        using (var db = new DuckDb(new FlatFileOptions { DatabasePath = path }))
            Assert.Equal(1, db.Connection.ExecuteScalar<int>("SELECT 1"));

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(plain));
    }

    [Fact]
    public void DatabasePath_Memory_StaysInMemory()
    {
        using var db = new DuckDb(new FlatFileOptions { DatabasePath = ":memory:" });

        Assert.Equal(1, db.Connection.ExecuteScalar<int>("SELECT 1"));
        Assert.Empty(Directory.GetFiles(_dataDir));
    }
}
