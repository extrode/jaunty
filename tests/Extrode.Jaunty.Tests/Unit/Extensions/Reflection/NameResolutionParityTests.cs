using System.Text;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Internals.Entity;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// The reflection half of the naming parity check. <c>GeneratedNamingResolverTests</c> in
/// Extrode.Jaunty.Fluent.SourceGen.Tests asserts the same expected names against the
/// source-generated twin of <see cref="ResolvedNameWidget"/>, so both mapping modes are held to one
/// table (docs/plans/2026-10-02-010).
/// </summary>
[Collection("Configuration Operations")]
public sealed class NameResolutionParityTests : IDisposable
{
    [Table("")]
    public sealed class ResolvedNameWidget
    {
        [Key]
        public int WidgetId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        [Column("fixed_price")]
        public decimal UnitPrice { get; set; }
    }

    public void Dispose()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = null);
        JauntyConfig.Reconfigure(jc => jc.SchemaNameResolver = null);
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = null);
    }

    private static string Snake(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    private static string[] ColumnNames(EntityMetadata metadata) => metadata.Columns.Select(c => c.ColumnName).ToArray();

    [Fact]
    public void WithoutResolvers_TheNamesAreTheAttributeOrCSharpNames()
    {
        EntityMetadata metadata = MetadataBuilder.Build<ResolvedNameWidget>();

        Assert.Equal("ResolvedNameWidget", metadata.TableName);
        Assert.Null(metadata.SchemaName);
        Assert.Equal(["WidgetId", "DisplayName", "fixed_price"], ColumnNames(metadata));
    }

    [Fact]
    public void Resolvers_RenameTheDefaultedNames_AndTheAttributeNameWins()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = type => Snake(type.Name) + "s");
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = Snake);

        EntityMetadata metadata = MetadataBuilder.Build<ResolvedNameWidget>();

        Assert.Equal("resolved_name_widgets", metadata.TableName);
        Assert.Equal(["widget_id", "display_name", "fixed_price"], ColumnNames(metadata));
    }

    [Fact]
    public void AResolverReturningNull_FallsBackToTheCSharpName()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = _ => null!);
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => name == "DisplayName" ? null! : Snake(name));

        EntityMetadata metadata = MetadataBuilder.Build<ResolvedNameWidget>();

        Assert.Equal("ResolvedNameWidget", metadata.TableName);
        Assert.Equal(["widget_id", "DisplayName", "fixed_price"], ColumnNames(metadata));
    }

    [Fact]
    public void TheSchemaResolver_SuppliesTheSchema()
    {
        JauntyConfig.Reconfigure(jc => jc.SchemaNameResolver = _ => "main");

        Assert.Equal("main", MetadataBuilder.Build<ResolvedNameWidget>().SchemaName);
    }
}
