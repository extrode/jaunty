using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// AUD-R35-073 (Excel) and AUD-R35-074 (JSON). Both sources honoured their write-relevant read
/// options - <c>SheetName</c>/<c>HasHeader</c>, <c>JsonFormat</c> - on the read side and returned
/// <c>null</c> from <c>GenerateCopyToOptions()</c>, so an export did not round-trip: the JSON file
/// came back newline-delimited when the source was configured for an array, and the Excel file
/// landed on the default sheet with a header row the source had been told not to expect. Same
/// round-trip-loss class as the already-fixed <c>CsvFileSource</c> (AUD-R21-003) and
/// <c>TsvFileSource</c> items, leaving these two the unfixed members of the set.
/// </summary>
public class JsonAndExcelRoundTripTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r35_roundtrip_{Guid.NewGuid():N}");

    public JsonAndExcelRoundTripTests() => Directory.CreateDirectory(_dataDir);

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------

    [Fact]
    public void Save_AnArrayConfiguredJsonSource_WritesAnArray()
    {
        string source = WriteJson("in_array.json", array: true);
        string outputPath = Path.Combine(_dataDir, "out_array.json");

        var options = new FlatFileOptions();
        options.AddJson<InventoryItem>(source, json => json.JsonFormat = JsonFileFormat.Array);

        using (var db = new DuckDb(options))
            db.Save<InventoryItem>(outputPath);

        Assert.StartsWith("[", File.ReadAllText(outputPath).TrimStart());
    }

    [Fact]
    public void Save_AnArrayConfiguredJsonSource_ProducesAFileTheSameSourceCanReadBack()
    {
        string source = WriteJson("in_roundtrip.json", array: true);
        string outputPath = Path.Combine(_dataDir, "out_roundtrip.json");

        var options = new FlatFileOptions();
        options.AddJson<InventoryItem>(source, json => json.JsonFormat = JsonFileFormat.Array);

        using (var db = new DuckDb(options))
            db.Save<InventoryItem>(outputPath);

        var reopened = new FlatFileOptions();
        reopened.AddJson<InventoryItem>(outputPath, json => json.JsonFormat = JsonFileFormat.Array);

        using var db2 = new DuckDb(reopened);
        Assert.Equal(2, db2.Connection.From<InventoryItem>().Count());
    }

    [Fact]
    public void Save_ANewlineDelimitedJsonSource_WritesOneObjectPerLine()
    {
        string source = WriteJson("in_ndjson.json", array: false);
        string outputPath = Path.Combine(_dataDir, "out_ndjson.json");

        var options = new FlatFileOptions();
        options.AddJson<InventoryItem>(source, json => json.JsonFormat = JsonFileFormat.NewlineDelimited);

        using (var db = new DuckDb(options))
            db.Save<InventoryItem>(outputPath);

        string text = File.ReadAllText(outputPath);
        Assert.StartsWith("{", text.TrimStart());
        Assert.Equal(2, File.ReadAllLines(outputPath).Count(l => l.Trim().Length != 0));
    }

    // ------------------------------------------------------------------
    // Excel
    // ------------------------------------------------------------------

    [Fact]
    public void Save_ASheetNamedExcelSource_WritesToThatSheet()
    {
        string source = WriteXlsx("in_sheet.xlsx", "Data", header: true);
        string outputPath = Path.Combine(_dataDir, "out_sheet.xlsx");

        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(source, xl => xl.SheetName = "Data");

        using (var db = new DuckDb(options))
            db.Save<InventoryItem>(outputPath);

        // Before the fix the export landed on DuckDB's default sheet, so this read threw
        // "Sheet 'Data' not found".
        Assert.Equal(2, CountXlsxRows(outputPath, ", sheet = 'Data'"));
    }

    [Fact]
    public void Save_ASheetNamedExcelSource_ProducesAFileTheSameSourceCanReadBack()
    {
        string source = WriteXlsx("in_sheet_rt.xlsx", "Data", header: true);
        string outputPath = Path.Combine(_dataDir, "out_sheet_rt.xlsx");

        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(source, xl => xl.SheetName = "Data");

        using (var db = new DuckDb(options))
            db.Save<InventoryItem>(outputPath);

        var reopened = new FlatFileOptions();
        reopened.AddExcel<InventoryItem>(outputPath, xl => xl.SheetName = "Data");

        using var db2 = new DuckDb(reopened);
        Assert.Equal(2, db2.Connection.From<InventoryItem>().Count());
    }

    [Fact]
    public void Save_AHeaderlessExcelSource_DoesNotInjectAHeaderRow()
    {
        string source = WriteXlsx("in_headerless.xlsx", sheetName: null, header: false);
        string outputPath = Path.Combine(_dataDir, "out_headerless.xlsx");

        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(source, xl => xl.HasHeader = false);

        using (var db = new DuckDb(options))
            db.Save<InventoryItem>(outputPath);

        // Read with header = false: two data rows, not three. Before the fix a header row was
        // written into a file declared to have none, and the next read ate it as data.
        Assert.Equal(2, CountXlsxRows(outputPath, ", header = false"));
    }

    // ------------------------------------------------------------------
    // Fixtures, built with DuckDB itself so nothing here depends on a checked-in binary.
    // ------------------------------------------------------------------

    private string WriteJson(string fileName, bool array)
    {
        string path = Path.Combine(_dataDir, fileName);
        Copy(path, "FORMAT JSON, ARRAY " + (array ? "true" : "false"), loadExcel: false);
        return path;
    }

    private string WriteXlsx(string fileName, string? sheetName, bool header)
    {
        string path = Path.Combine(_dataDir, fileName);
        string opts = "FORMAT XLSX";
        if (sheetName is not null) opts += $", SHEET '{sheetName}'";
        if (!header) opts += ", HEADER false";
        Copy(path, opts, loadExcel: true);
        return path;
    }

    private static void Copy(string path, string copyOptions, bool loadExcel)
    {
        using var conn = new DuckDBConnection("DataSource=:memory:");
        conn.Open();

        if (loadExcel)
        {
            Exec(conn, "INSTALL excel");
            Exec(conn, "LOAD excel");
        }

        Exec(conn,
            "CREATE TABLE inventory AS SELECT * FROM (VALUES " +
            "(1, 'Widget A', 'Electronics', 150, 29.99, true), " +
            "(2, 'Widget B', 'Electronics', 0, 49.99, false)) " +
            "AS t(ItemId, ItemName, Category, StockQuantity, UnitPrice, InStock)");

        Exec(conn, $"COPY inventory TO '{path.Replace("\\", "/").Replace("'", "''")}' ({copyOptions})");
    }

    private static int CountXlsxRows(string path, string readArgs)
    {
        using var conn = new DuckDBConnection("DataSource=:memory:");
        conn.Open();
        Exec(conn, "INSTALL excel");
        Exec(conn, "LOAD excel");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT count(*) FROM read_xlsx('{path.Replace("\\", "/").Replace("'", "''")}'{readArgs})";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void Exec(DuckDBConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
