using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.FlatFiles.Import;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// The import binds a source value for an enum property as it is, not as a boxed enum: SQLite would
/// store a boxed enum as its underlying integer, so a TEXT column holding "Closed" would receive 1.
/// </summary>
public class ImportEnumValueTests : IDisposable
{
    public enum RowStatus
    {
        Open,
        Closed,
    }

    [Table("enum_rows")]
    public class EnumRow
    {
        public int Id { get; set; }
        public RowStatus Status { get; set; }
    }

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_enum_{Guid.NewGuid():N}");

    public ImportEnumValueTests() => Directory.CreateDirectory(_dataDir);

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AnEnumNameInTheSource_ReachesATextColumnAsTheName()
    {
        string csv = Path.Combine(_dataDir, "rows.csv");
        File.WriteAllText(csv, "Id,Status\n1,Closed\n");
        using var target = new SqliteConnection("Data Source=:memory:");
        target.Open();
        using (SqliteCommand create = target.CreateCommand())
        {
            create.CommandText = "CREATE TABLE enum_rows (Id INTEGER, Status TEXT)";
            create.ExecuteNonQuery();
        }

        long imported = await FlatFileImporter.ImportAsync<EnumRow>(csv, target, new ImportOptions());

        using SqliteCommand read = target.CreateCommand();
        read.CommandText = "SELECT Status FROM enum_rows";
        Assert.Equal(1, imported);
        Assert.Equal("Closed", read.ExecuteScalar());
    }
}
