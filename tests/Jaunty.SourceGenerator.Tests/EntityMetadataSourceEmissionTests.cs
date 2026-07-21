using Jaunty.Interfaces;
using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Round 3 AOT-metadata redesign: the source generator emits an explicit
/// <see cref="IEntityMetadataSource"/> implementation (TableName/SchemaName/Columns) plus a new
/// <see cref="EntityColumnInfo"/> DTO on every generated entity, so
/// <c>SourceGeneratedMetadataResolver</c> can resolve entity metadata via a plain
/// <c>new T() is IEntityMetadataSource</c> check with zero reflection. This file is the first
/// test coverage for that emission - the round 3 tests/ audit found it had none.
/// </summary>
public sealed class EntityMetadataSourceEmissionTests
{
    [Fact]
    public void TableName_SingleKeyNoSchema_MatchesStaticTableName()
    {
        IEntityMetadataSource source = new GenProduct();

        Assert.Equal(GenProduct.TableName, source.TableName);
        Assert.Null(source.SchemaName);
    }

    [Fact]
    public void SchemaName_NamedArgument_MatchesStaticSchemaName()
    {
        IEntityMetadataSource source = new GenOrderLine();

        Assert.Equal(GenOrderLine.TableName, source.TableName);
        Assert.Equal(GenOrderLine.SchemaName, source.SchemaName);
        Assert.Equal("sales", source.SchemaName);
    }

    [Fact]
    public void SchemaName_PositionalConstructorArgument_MatchesStaticSchemaName()
    {
        IEntityMetadataSource source = new GenWidget();

        Assert.Equal(GenWidget.TableName, source.TableName);
        Assert.Equal("dbo", source.SchemaName);
    }

    [Fact]
    public void Columns_SingleKeyEntity_ReportsAllPropertiesWithCorrectPrimaryKeyFlag()
    {
        IEntityMetadataSource source = new GenProduct();

        Assert.Equal(4, source.Columns.Count);

        EntityColumnInfo productId = Assert.Single(source.Columns, c => c.ColumnName == "product_id");
        Assert.Equal(nameof(GenProduct.ProductId), productId.PropertyName);
        Assert.True(productId.IsPrimaryKey);
        // int/long [Key] properties with no explicit [DatabaseGenerated] default to identity=true
        // per the generator's convention (JauntyGenerator.cs PropertyMetadata resolution).
        Assert.True(productId.IsIdentity);
        Assert.Equal(typeof(int), productId.PropertyType);

        foreach (EntityColumnInfo column in source.Columns)
        {
            if (column.ColumnName != "product_id")
            {
                Assert.False(column.IsPrimaryKey);
                Assert.False(column.IsIdentity);
            }
        }
    }

    [Fact]
    public void Columns_CompositeKeyEntity_MarksBothKeyColumnsAsPrimaryKey()
    {
        IEntityMetadataSource source = new GenOrderLine();

        EntityColumnInfo orderId = Assert.Single(source.Columns, c => c.ColumnName == "order_id");
        EntityColumnInfo lineNumber = Assert.Single(source.Columns, c => c.ColumnName == "line_number");
        EntityColumnInfo productId = Assert.Single(source.Columns, c => c.ColumnName == "product_id");

        Assert.True(orderId.IsPrimaryKey);
        Assert.True(lineNumber.IsPrimaryKey);
        Assert.False(productId.IsPrimaryKey);
    }

    [Fact]
    public void Columns_ExplicitDatabaseGeneratedAttribute_OverridesIntKeyIdentityConvention()
    {
        IEntityMetadataSource source = new GenIdentityEntity();

        EntityColumnInfo entityId = Assert.Single(source.Columns, c => c.ColumnName == "entity_id");
        EntityColumnInfo secondaryKey = Assert.Single(source.Columns, c => c.ColumnName == "secondary_key");
        EntityColumnInfo label = Assert.Single(source.Columns, c => c.ColumnName == "label");

        Assert.True(entityId.IsPrimaryKey);
        Assert.True(entityId.IsIdentity);

        // Also an int [Key] (which defaults to identity=true by convention), but explicitly
        // marked [DatabaseGenerated(None)] - the explicit attribute must win over the convention.
        Assert.True(secondaryKey.IsPrimaryKey);
        Assert.False(secondaryKey.IsIdentity);

        Assert.False(label.IsPrimaryKey);
        Assert.False(label.IsIdentity);
    }

    [Fact]
    public void Columns_ComputedAttribute_SetsIsComputedTrue()
    {
        IEntityMetadataSource source = new GenComputedEntity();

        EntityColumnInfo entityId = Assert.Single(source.Columns, c => c.ColumnName == "entity_id");
        EntityColumnInfo label = Assert.Single(source.Columns, c => c.ColumnName == "label");
        EntityColumnInfo computedValue = Assert.Single(source.Columns, c => c.ColumnName == "computed_value");

        Assert.False(entityId.IsComputed);
        Assert.False(label.IsComputed);
        Assert.True(computedValue.IsComputed);
    }

    [Fact]
    public void Columns_GetOnlyAndInitOnlyProperties_AreExcluded()
    {
        // If the generator still emitted `entity.Code = ...` / `entity.Display = ...`
        // assignments for these, the whole test assembly would fail to compile
        // (CS0200/CS8852) - this test existing and compiling is itself part of the coverage.
        IEntityMetadataSource source = new GenGetOnlyEntity();

        Assert.Equal(2, source.Columns.Count);
        Assert.Contains(source.Columns, c => c.ColumnName == "entity_id");
        Assert.Contains(source.Columns, c => c.ColumnName == "label");
        Assert.DoesNotContain(source.Columns, c => c.ColumnName == "code");
        Assert.DoesNotContain(source.Columns, c => c.PropertyName == nameof(GenGetOnlyEntity.Display));
    }

    [Fact]
    public void Columns_Getter_ReturnsCurrentPropertyValue()
    {
        var product = new GenProduct { ProductId = 7, ProductName = "Widget" };
        IEntityMetadataSource source = product;

        EntityColumnInfo nameColumn = Assert.Single(source.Columns, c => c.ColumnName == "product_name");

        Assert.Equal("Widget", nameColumn.Getter(product));
    }

    [Fact]
    public void Columns_Setter_MutatesUnderlyingProperty()
    {
        var product = new GenProduct { ProductId = 1, ProductName = "Original" };
        IEntityMetadataSource source = product;

        EntityColumnInfo nameColumn = Assert.Single(source.Columns, c => c.ColumnName == "product_name");
        nameColumn.Setter(product, "Updated");

        Assert.Equal("Updated", product.ProductName);
    }

    [Fact]
    public void EntityColumns_StaticProperty_MatchesInstanceColumnsExactly()
    {
        IEntityMetadataSource source = new GenProduct();

        Assert.Equal(GenProduct.EntityColumns.Count, source.Columns.Count);
        Assert.Equal(
            GenProduct.EntityColumns.Select(c => c.ColumnName).OrderBy(x => x),
            source.Columns.Select(c => c.ColumnName).OrderBy(x => x));
    }
}
