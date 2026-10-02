using System.Reflection;
using System.Text;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Unit;
using Extrode.Jaunty.FlatFiles.Import;
using Extrode.Jaunty.Fluent;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// AUD-R35-243 and AUD-R35-246: FlatFiles named tables and columns without the
/// <c>JauntyConfig</c> resolvers, so an import created a table core could not find.
/// </summary>
[Collection(GlobalInterceptorStateCollection.Name)]
public sealed class ImportNamingResolverTests : IDisposable
{
    private readonly Func<Type, string?>? _originalTableResolver = JauntyConfig.TableNameResolver;
    private readonly Func<string, string?>? _originalColumnResolver = JauntyConfig.ColumnNameResolver;
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_naming_{Guid.NewGuid():N}");

    public ImportNamingResolverTests() => Directory.CreateDirectory(_dataDir);

    public void Dispose()
    {
        JauntyConfig.TableNameResolver = _originalTableResolver;
        JauntyConfig.ColumnNameResolver = _originalColumnResolver;
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    public class StockItem
    {
        public int Id { get; set; }

        public string? DisplayName { get; set; }

        [Column("legacy_code")]
        public string? Code { get; set; }

        [Column("")]
        public int UnitCount { get; set; }
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    private static void UseSnakeCase()
    {
        JauntyConfig.TableNameResolver = t => ToSnakeCase(t.Name);
        JauntyConfig.ColumnNameResolver = ToSnakeCase;
    }

    private static PropertyInfo Property(string name) => typeof(StockItem).GetProperty(name)!;

    [Theory]
    [InlineData(nameof(StockItem.DisplayName), "display_name")]
    [InlineData(nameof(StockItem.Code), "legacy_code")]
    [InlineData(nameof(StockItem.UnitCount), "unit_count")]
    public void TheColumnName_FollowsCoreOrder(string property, string expected)
    {
        UseSnakeCase();

        Assert.Equal(expected, MappedPropertyFilter.GetColumnName(Property(property)));
    }

    [Fact]
    public void AnAttributeColumnName_DoesNotCallTheResolver()
    {
        int calls = 0;
        JauntyConfig.ColumnNameResolver = n => { calls++; return n; };

        MappedPropertyFilter.GetColumnName(Property(nameof(StockItem.Code)));

        Assert.Equal(0, calls);
    }

    [Fact]
    public void TheColumnMappings_AreRebuiltAfterTheResolverChanges()
    {
        JauntyConfig.ColumnNameResolver = null;
        Assert.Contains("DisplayName", ColumnMappingCache.Get(typeof(StockItem)).Keys);

        JauntyConfig.ColumnNameResolver = ToSnakeCase;

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(StockItem));
        Assert.Contains("display_name", mappings.Keys);
        Assert.DoesNotContain("DisplayName", mappings.Keys);
    }

    [Fact]
    public async Task AnImportCreatesTheTableCoreReads()
    {
        UseSnakeCase();
        string csv = Path.Combine(_dataDir, "stock.csv");
        File.WriteAllText(csv, "id,display_name,legacy_code,unit_count\n1,Bolt,B-1,40\n2,Nut,N-2,75\n");
        using var target = new SqliteConnection("Data Source=:memory:");
        target.Open();

        long imported = await FlatFileImporter.ImportAsync<StockItem>(csv, target, new ImportOptions(createTableIfMissing: true));

        List<StockItem> rows = target.From<StockItem>().OrderBy(s => s.Id).Select();
        Assert.Equal(2, imported);
        Assert.Collection(rows,
            r => { Assert.Equal("Bolt", r.DisplayName); Assert.Equal("B-1", r.Code); Assert.Equal(40, r.UnitCount); },
            r => { Assert.Equal("Nut", r.DisplayName); Assert.Equal("N-2", r.Code); Assert.Equal(75, r.UnitCount); });

        using SqliteCommand tables = target.CreateCommand();
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        Assert.Equal("stock_item", tables.ExecuteScalar());
    }
}
