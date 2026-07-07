using Jaunty.Fluent;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.SourceGen.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.Fluent.SourceGen.Tests.Integration;

/// <summary>
/// Spec 003 (fluent NativeAOT-safe metadata) tasks T005/T006: proves fluent queries against
/// source-generated entities work with zero calls to UseReflectionMapping() anywhere in the
/// process. This project deliberately has no reference to Jaunty.Extensions.Reflection at
/// all (see the .csproj) - JauntyConfig.ReflectionTableMetadataResolver is guaranteed null
/// for the lifetime of this process, so any successful query here is resolved entirely by
/// FluentMetadataCache's source-gen-first tier.
/// </summary>
public sealed class FluentSourceGenMetadataTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public FluentSourceGenMetadataTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE sourcegen_widgets (
                widget_id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                price NUMERIC NOT NULL
            );
            CREATE TABLE sourcegen_widget_tags (
                tag_id INTEGER PRIMARY KEY,
                widget_id INTEGER NOT NULL,
                tag TEXT NOT NULL
            );
            INSERT INTO sourcegen_widgets VALUES (1, 'Gadget', 9.99);
            INSERT INTO sourcegen_widgets VALUES (2, 'Gizmo', 19.99);
            INSERT INTO sourcegen_widget_tags VALUES (1, 1, 'new');
            INSERT INTO sourcegen_widget_tags VALUES (2, 1, 'sale');
            INSERT INTO sourcegen_widget_tags VALUES (3, 2, 'new');
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void GetMetadata_SourceGeneratedEntity_ResolvesWithoutReflectionResolver()
    {
        var metadata = FluentMetadataCache.GetMetadata<SourceGenWidget>();

        Assert.Equal("sourcegen_widgets", metadata.TableName);
        Assert.Null(metadata.SchemaName);
        Assert.Equal(3, metadata.Columns.Count);
        Assert.All(metadata.Columns, c => Assert.NotNull(c.Getter));
        Assert.All(metadata.Columns, c => Assert.NotNull(c.Setter));

        var widgetIdColumn = Assert.Single(metadata.PrimaryKeys);
        Assert.Equal("widget_id", widgetIdColumn.ColumnName);
        Assert.Equal("WidgetId", widgetIdColumn.PropertyName);
    }

    [Fact]
    public void Query_WhereAndSelect_ReturnsResults()
    {
        var widgets = _connection.From<SourceGenWidget>()
            .Where(w => w.Price > 10m)
            .Select();

        var widget = Assert.Single(widgets);
        Assert.Equal("Gizmo", widget.Name);
    }

    [Fact]
    public void Query_InnerJoin_ReturnsResults()
    {
        var rows = _connection.From<SourceGenWidget>()
            .InnerJoin<SourceGenWidgetTag>().On((w, t) => w.WidgetId == t.WidgetId)
            .Where((w, t) => w.WidgetId == 1)
            .SelectBoth();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Query_GroupBy_ReturnsResults()
    {
        var results = _connection.From<SourceGenWidgetTag>()
            .GroupBy(t => t.WidgetId)
            .Select(g => new { WidgetId = g.Key, TagCount = g.Count() });

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.WidgetId == 1 && r.TagCount == 2);
        Assert.Contains(results, r => r.WidgetId == 2 && r.TagCount == 1);
    }

    [Fact]
    public void GetMetadata_UnrelatedType_WithNoMetadataSource_ThrowsActionableError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => FluentMetadataCache.GetMetadata<NotMappedEntity>());

        Assert.Contains("source generator", ex.Message);
        Assert.Contains("UseReflectionMapping", ex.Message);
    }

    private class NotMappedEntity
    {
        public int Id { get; set; }
    }
}
