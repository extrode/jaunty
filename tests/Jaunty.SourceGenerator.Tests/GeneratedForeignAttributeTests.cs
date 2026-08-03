using Jaunty.Attributes;
using Jaunty.Interfaces;
using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R35-070. <c>HasAttribute</c>/<c>GetAttribute</c> compared <c>AttributeClass?.Name</c> - the
/// simple class name - so <c>[Ignore]</c>, <c>[NotMapped]</c>, <c>[Key]</c>, <c>[Column]</c>,
/// <c>[DatabaseGenerated]</c> and <c>[EnumStorage]</c> were honoured from <em>any</em> namespace.
/// The reflection twin matches by exact type or by <c>AttributeType.FullName</c>
/// (<c>MetadataBuilder</c>), so a consumer type carrying another library's <c>ColumnAttribute</c> or
/// <c>NotMappedAttribute</c> - both names several libraries define - got a renamed or dropped column
/// on the generated path and the original mapping under reflection. AUD-R34-031 fixed exactly this
/// hazard but scoped the fix to <c>[Table]</c>; its own reason for doing so applies unchanged to the
/// other six.
/// </summary>
public sealed class GeneratedForeignAttributeTests
{
    private static EntityColumnInfo Column(IEntityMetadataSource source, string propertyName)
        => Assert.Single(source.Columns, c => c.PropertyName == propertyName);

    // ------------------------------------------------------------------
    // A foreign attribute changes nothing.
    // ------------------------------------------------------------------

    [Fact]
    public void AForeignColumnAttribute_DoesNotRenameTheColumn()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.Equal(nameof(GenForeignAttributeEntity.Kept), Column(source, "Kept").ColumnName);
        Assert.DoesNotContain(source.Columns, c => c.ColumnName == "renamed_by_a_stranger");
    }

    [Fact]
    public void AForeignNotMappedAttribute_DoesNotDropTheColumn()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.Equal(
            nameof(GenForeignAttributeEntity.StillMappedDespiteNotMapped),
            Column(source, "StillMappedDespiteNotMapped").ColumnName);
    }

    [Fact]
    public void AForeignIgnoreAttribute_DoesNotDropTheColumn()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.Equal(
            nameof(GenForeignAttributeEntity.StillMappedDespiteIgnore),
            Column(source, "StillMappedDespiteIgnore").ColumnName);
    }

    [Fact]
    public void AForeignKeyAttribute_DoesNotMakeASecondPrimaryKey()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.False(Column(source, "NotAKey").IsPrimaryKey);
        Assert.Equal("EntityId", Assert.Single(source.Columns, c => c.IsPrimaryKey).PropertyName);
    }

    [Fact]
    public void AForeignDatabaseGeneratedAttribute_DoesNotMarkTheColumnComputed()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        EntityColumnInfo column = Column(source, "NotComputed");
        Assert.False(column.IsComputed);
        Assert.False(column.IsIdentity);
    }

    [Fact]
    public void AForeignEnumStorageAttribute_DoesNotOverrideTheStorage()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.Null(Column(source, "Grade").EnumStorageOverride);
    }

    [Fact]
    public void EveryPropertyOfTheForeignlyAttributedEntity_IsStillMapped()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.Equal(8, source.Columns.Count);
    }

    // ------------------------------------------------------------------
    // Recognized attributes - including a subclass of one - still bind.
    // ------------------------------------------------------------------

    [Fact]
    public void AttributeSubclassing_ARecognizedColumnAttribute_StillRenamesTheColumn()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        Assert.Equal("derived_column", Column(source, "Derived").ColumnName);
    }

    [Fact]
    public void JauntysOwnColumnAndKey_StillBind()
    {
        IEntityMetadataSource source = new GenForeignAttributeEntity();

        EntityColumnInfo key = Column(source, "EntityId");
        Assert.Equal("entity_id", key.ColumnName);
        Assert.True(key.IsPrimaryKey);
    }

    [Fact]
    public void DataAnnotationsColumnAndKey_StillBind()
    {
        IEntityMetadataSource source = new GenRecognizedAttributeEntity();

        EntityColumnInfo key = Column(source, "Code");
        Assert.Equal("code", key.ColumnName);
        Assert.True(key.IsPrimaryKey);
    }

    [Fact]
    public void DataAnnotationsNotMapped_StillDropsTheColumn()
    {
        IEntityMetadataSource source = new GenRecognizedAttributeEntity();

        Assert.DoesNotContain(source.Columns, c => c.PropertyName == "Dropped");
    }

    [Fact]
    public void JauntysOwnEnumStorage_StillOverridesTheStorage()
    {
        IEntityMetadataSource source = new GenRecognizedAttributeEntity();

        Assert.Equal(EnumStorage.String, Column(source, "Grade").EnumStorageOverride);
    }
}
