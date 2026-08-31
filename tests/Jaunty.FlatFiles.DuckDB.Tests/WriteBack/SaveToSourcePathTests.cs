using Jaunty.Attributes;
using Jaunty.FlatFiles.WriteBack;

namespace Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// AUD-R35-034. <c>Save&lt;T&gt;(string)</c> and <c>Export&lt;T&gt;(string)</c> are documented as
/// non-destructive - <c>IFlatFile.Save&lt;T&gt;(string)</c>: "The original file is not modified" -
/// but neither checked the path it was handed against the source's own, so passing it ran
/// <c>COPY ... TO</c> straight at the file the source is still reading through a VIEW. The failure
/// was silent: no exception, the loss visible only in the file afterwards.
/// </summary>
public class SaveToSourcePathTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_savepath_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    [Table("saved")]
    private sealed class SavedRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
    }

    public SaveToSourcePathTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csvPath = Path.Combine(_dataDir, "saved.csv");
        File.WriteAllText(_csvPath, "Id,Name\n1,alpha\n2,beta\n3,gamma\n");

        var options = new FlatFileOptions();
        options.AddCsv<SavedRow>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SaveToTheSourcePath_ThrowsAndLeavesTheFileIntact()
    {
        string before = File.ReadAllText(_csvPath);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => _db.Save<SavedRow>(_csvPath));

        Assert.Contains("non-destructive", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WriteBackMode.Overwrite", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(_csvPath));
    }

    [Fact]
    public void ExportToTheSourcePath_ThrowsAndLeavesTheFileIntact()
    {
        string before = File.ReadAllText(_csvPath);

        Assert.Throws<ArgumentException>(() => _db.Export<SavedRow>(_csvPath));

        Assert.Equal(before, File.ReadAllText(_csvPath));
    }

    [Fact]
    public async Task SaveAsyncToTheSourcePath_Throws()
    {
        string before = File.ReadAllText(_csvPath);

        await Assert.ThrowsAsync<ArgumentException>(async () => await _db.SaveAsync<SavedRow>(_csvPath));

        Assert.Equal(before, File.ReadAllText(_csvPath));
    }

    [Fact]
    public async Task ExportAsyncToTheSourcePath_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await _db.ExportAsync<SavedRow>(_csvPath));
    }

    /// <summary>
    /// The path is compared fully resolved, so the same file reached by a relative or
    /// dot-segmented path is caught too - which is how it would arrive in practice.
    /// </summary>
    [Fact]
    public void ADottedPathToTheSameFileIsAlsoRejected()
    {
        string dotted = Path.Combine(_dataDir, ".", "sub", "..", "saved.csv");
        Directory.CreateDirectory(Path.Combine(_dataDir, "sub"));

        Assert.Throws<ArgumentException>(() => _db.Save<SavedRow>(dotted));
    }

    /// <summary>
    /// The control, and the case that must keep working: a different path still writes, and still
    /// leaves the original alone.
    /// </summary>
    [Fact]
    public void SaveToADifferentPathStillWorks()
    {
        string outputPath = Path.Combine(_dataDir, "copy.csv");
        string before = File.ReadAllText(_csvPath);

        _db.Save<SavedRow>(outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.Contains("gamma", File.ReadAllText(outputPath), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(_csvPath));
    }

    /// <summary>
    /// A different file in the same directory, differing only in name, must not be caught by the
    /// comparison.
    /// </summary>
    [Fact]
    public void ANeighbouringFileIsNotMistakenForTheSource()
    {
        string outputPath = Path.Combine(_dataDir, "saved2.csv");

        _db.Save<SavedRow>(outputPath);

        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// The in-place route is unaffected: it is the overload that asks for destruction by name, and
    /// it replaces the file atomically through a temporary one.
    /// </summary>
    [Fact]
    public void WriteBackModeOverwriteStillOverwritesTheSource()
    {
        _db.Insert(new SavedRow { Id = 4, Name = "delta" });

        _db.Save<SavedRow>(WriteBackMode.Overwrite);

        Assert.Contains("delta", File.ReadAllText(_csvPath), StringComparison.Ordinal);
    }
}
