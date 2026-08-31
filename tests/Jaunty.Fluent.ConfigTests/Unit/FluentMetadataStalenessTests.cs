using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Fluent;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// AUD-R26 (batch 4). <c>FluentMetadataCache</c>'s two caches keyed on the entity type alone - and
/// on the dialect type, for the escaped form - so the first fluent query over a given <c>T</c> fixed
/// its table and column names for the life of the process. Both come from <c>EntityMetadata</c>,
/// which <c>MetadataBuilder</c> builds from <c>JauntyConfig</c>'s schema, table and column name
/// resolvers; all three are public and settable at any time, and none of them was part of the key.
///
/// <para>
/// This is the fluent half of the same defect the core write path had, and it is here rather than
/// in the core suite because <c>FluentMetadataCache</c> is a separate cache in a separate assembly
/// that <c>Jaunty.Tests</c> never touches. Without it the fix to that file would ship unexercised.
/// </para>
/// </summary>
public class FluentMetadataStalenessTests : IDisposable
{
    public FluentMetadataStalenessTests() => JauntyReflectionExtensions.UseReflectionMapping();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Table("fluent_resolver_rename")]
    public class FluentRenameWidget
    {
        [Key]
        public int Id { get; set; }
        public string? WidgetName { get; set; }
    }

    /// <summary>
    /// A column-name resolver registered after the type has been queried once must change the SQL
    /// the next query generates. Asserting on the statement rather than on a round trip keeps the
    /// observation on the cache under test: it is the escaped column name in the generated SELECT
    /// that <c>FluentMetadataCache</c> holds.
    /// </summary>
    [Fact]
    public void AColumnNameResolverRegisteredAfterTheFirstQueryChangesTheGeneratedSql()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        string before = connection.From<FluentRenameWidget>()
            .Where(w => w.WidgetName == "x")
            .ToSql();

        Assert.Contains("WidgetName", before, StringComparison.Ordinal);
        Assert.DoesNotContain("widget_name", before, StringComparison.Ordinal);

        JauntyConfig.ColumnNameResolver = static name =>
            name == nameof(FluentRenameWidget.WidgetName) ? "widget_name" : name;

        string after = connection.From<FluentRenameWidget>()
            .Where(w => w.WidgetName == "x")
            .ToSql();

        Assert.Contains("widget_name", after, StringComparison.Ordinal);
    }
}
