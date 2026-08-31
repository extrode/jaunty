using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Spec 003 (fluent NativeAOT-safe metadata) task T001: the source generator must emit
/// TableName, SchemaName, and PrimaryKeyColumnNames as static, reflection-free members
/// on generated entities, so a future metadata-resolution tier can consume them without
/// falling back to Jaunty.Extensions.Reflection.
/// </summary>
public sealed class TableMetadataEmissionTests
{
    [Fact]
    public void TableName_NoSchemaSpecified_ResolvesFromTableAttribute()
    {
        Assert.Equal("gen_products", GenProduct.TableName);
        Assert.Null(GenProduct.SchemaName);
    }

    [Fact]
    public void SchemaName_NamedArgument_Resolves()
    {
        Assert.Equal("gen_order_lines", GenOrderLine.TableName);
        Assert.Equal("sales", GenOrderLine.SchemaName);
    }

    [Fact]
    public void SchemaName_PositionalConstructorArgument_Resolves()
    {
        Assert.Equal("gen_widgets", GenWidget.TableName);
        Assert.Equal("dbo", GenWidget.SchemaName);
    }

    [Fact]
    public void PrimaryKeyColumnNames_SingleKey_ContainsColumnName()
    {
        Assert.Equal(["product_id"], GenProduct.PrimaryKeyColumnNames);
    }

    [Fact]
    public void PrimaryKeyColumnNames_CompositeKey_ContainsAllColumnNamesInDeclaredOrder()
    {
        Assert.Equal(["order_id", "line_number"], GenOrderLine.PrimaryKeyColumnNames);
    }
}
