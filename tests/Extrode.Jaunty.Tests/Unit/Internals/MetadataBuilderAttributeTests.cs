using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Extensions.Reflection;

using DA = System.ComponentModel.DataAnnotations.Schema;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// The string-matched <c>System.ComponentModel.DataAnnotations</c> fallbacks in
/// <c>MetadataBuilder.Build</c>, and the property filters ahead of them. Every one of these was
/// NoCoverage or Survived under Stryker.
/// </summary>
public class MetadataBuilderAttributeTests
{
    public abstract class AbstractEntity
    {
        public int Id { get; set; }
    }

    [DA.Table("da_table", Schema = "da_schema")]
    public class DataAnnotationsTable
    {
        public int Id { get; set; }
    }

    [DA.Table("da_table_only")]
    public class DataAnnotationsTableWithoutSchema
    {
        public int Id { get; set; }
    }

    [DA.Table("", Schema = "")]
    public class DataAnnotationsEmptyNames
    {
        public int Id { get; set; }
    }

    public class FilteredProperties
    {
        public int Id { get; set; }
        [Ignore] public string? Ignored { get; set; }
        [DA.NotMapped] public string? NotMapped { get; set; }
        public string ReadOnly => "x";
        public string? Kept { get; set; }
    }

    public class DataAnnotationsColumns
    {
        public int Id { get; set; }
        [DA.Column("da_name")] public string? Named { get; set; }
        [DA.Column(TypeName = "text")] public string? TypedOnly { get; set; }
        [DA.Column("")] public string? EmptyName { get; set; }
    }

    public class DataAnnotationsGenerated
    {
        [DA.DatabaseGenerated(DA.DatabaseGeneratedOption.Identity)] public int Id { get; set; }
        [DA.DatabaseGenerated(DA.DatabaseGeneratedOption.Computed)] public int Total { get; set; }
        [DA.DatabaseGenerated(DA.DatabaseGeneratedOption.None)] public int Code { get; set; }
        public int Plain { get; set; }
    }

    public class Gadget
    {
        public int GadgetId { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void Build_AnAbstractType_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MetadataBuilder.Build<AbstractEntity>());

        Assert.Equal("Type 'AbstractEntity' cannot be abstract. Only concrete types can be mapped.", ex.Message);
    }

    [Fact]
    public void Build_DataAnnotationsTable_SetsTableAndSchema()
    {
        var metadata = MetadataBuilder.Build<DataAnnotationsTable>();

        Assert.Equal("da_table", metadata.TableName);
        Assert.Equal("da_schema", metadata.SchemaName);
    }

    [Fact]
    public void Build_DataAnnotationsTableWithoutSchema_LeavesTheSchemaUnset()
    {
        var metadata = MetadataBuilder.Build<DataAnnotationsTableWithoutSchema>();

        Assert.Equal("da_table_only", metadata.TableName);
        Assert.Null(metadata.SchemaName);
    }

    [Fact]
    public void Build_DataAnnotationsTableWithEmptyNames_FallsBackToTheDefaults()
    {
        var metadata = MetadataBuilder.Build<DataAnnotationsEmptyNames>();

        Assert.Equal(nameof(DataAnnotationsEmptyNames), metadata.TableName);
        Assert.Null(metadata.SchemaName);
    }

    [Fact]
    public void Build_SkipsIgnoredNotMappedAndReadOnlyProperties()
    {
        var metadata = MetadataBuilder.Build<FilteredProperties>();

        Assert.Equal(new[] { "Id", "Kept" }, metadata.Columns.Select(c => c.ColumnName));
    }

    [Fact]
    public void Build_DataAnnotationsColumn_RenamesOnlyWhenItCarriesAName()
    {
        var metadata = MetadataBuilder.Build<DataAnnotationsColumns>();

        Assert.Equal(new[] { "Id", "da_name", "TypedOnly", "EmptyName" }, metadata.Columns.Select(c => c.ColumnName));
    }

    [Fact]
    public void Build_DataAnnotationsDatabaseGenerated_MapsEachOption()
    {
        var columns = MetadataBuilder.Build<DataAnnotationsGenerated>().Columns.ToDictionary(c => c.PropertyName);

        Assert.True(columns["Id"].IsIdentity);
        Assert.True(columns["Total"].IsComputed);
        Assert.False(columns["Code"].IsIdentity);
        Assert.False(columns["Code"].IsComputed);
        Assert.False(columns["Plain"].IsIdentity);
    }

    [Fact]
    public void Build_TypeNameIdConvention_MarksTheKey()
    {
        var columns = MetadataBuilder.Build<Gadget>().Columns.ToDictionary(c => c.PropertyName);

        Assert.True(columns["GadgetId"].IsPrimaryKey);
        Assert.False(columns["Name"].IsPrimaryKey);
    }
}
