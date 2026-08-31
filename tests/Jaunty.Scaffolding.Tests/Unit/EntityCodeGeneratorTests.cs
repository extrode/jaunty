using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class EntityCodeGeneratorTests
{
    private readonly EntityCodeGenerator _generator;
    private readonly CodeGeneratorOptions _defaultOptions;

    public EntityCodeGeneratorTests()
    {
        _generator = new EntityCodeGenerator(new SqlServerTypeMapper());
        _defaultOptions = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            UseFileScopedNamespace = true,
            GenerateTableAttribute = true,
            GenerateColumnAttribute = true,
            GenerateKeyAttribute = true,
            GenerateDatabaseGeneratedAttribute = true,
            UseNullableReferenceTypes = true,
            Singularize = true
        };
    }

    private static TableSchema CreateSimpleTable() =>
        new()
        {
            SchemaName = "dbo",
            TableName = "Products",
            Columns =
            [
                new ColumnSchema
                {
                    ColumnName = "ProductId",
                    DataType = "int",
                    IsNullable = false,
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    OrdinalPosition = 1
                },
                new ColumnSchema
                {
                    ColumnName = "ProductName",
                    DataType = "nvarchar",
                    IsNullable = false,
                    MaxLength = 100,
                    OrdinalPosition = 2
                },
                new ColumnSchema
                {
                    ColumnName = "UnitPrice",
                    DataType = "decimal",
                    IsNullable = true,
                    Precision = 10,
                    Scale = 2,
                    OrdinalPosition = 3
                }
            ]
        };

    private static TableSchema CreateSnakeCaseTable() =>
        new()
        {
            SchemaName = "",
            TableName = "order_details",
            Columns =
            [
                new ColumnSchema
                {
                    ColumnName = "order_detail_id",
                    DataType = "int",
                    IsNullable = false,
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    OrdinalPosition = 1
                },
                new ColumnSchema
                {
                    ColumnName = "order_id",
                    DataType = "int",
                    IsNullable = false,
                    OrdinalPosition = 2
                },
                new ColumnSchema
                {
                    ColumnName = "unit_price",
                    DataType = "decimal",
                    IsNullable = true,
                    OrdinalPosition = 3
                }
            ]
        };

    [Fact]
    public void GenerateEntity_SimpleTable_GeneratesCorrectClass()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("namespace Test.Entities;", code);
        Assert.Contains("public partial class Product", code);
        Assert.Contains("[Jaunty.Attributes.Table(\"Products\", \"dbo\")]", code);
    }

    [Fact]
    public void GenerateEntity_WithPrimaryKey_GeneratesKeyAttribute()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.Key]", code);
    }

    [Fact]
    public void GenerateEntity_WithIdentity_GeneratesDatabaseGeneratedAttribute()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.DatabaseGenerated(Jaunty.Attributes.DatabaseGeneratedOption.Identity)]", code);
    }

    [Fact]
    public void GenerateEntity_NullableColumn_GeneratesNullableType()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("public decimal? UnitPrice { get; set; }", code);
    }

    [Fact]
    public void GenerateEntity_NonNullableString_GeneratesDefaultValue()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("public string ProductName { get; set; } = string.Empty;", code);
    }

    [Fact]
    public void GenerateEntity_SnakeCaseColumns_GeneratesColumnAttributes()
    {
        var table = CreateSnakeCaseTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.Column(\"order_detail_id\")]", code);
        Assert.Contains("public int OrderDetailId { get; set; }", code);

        Assert.Contains("[Jaunty.Attributes.Column(\"order_id\")]", code);
        Assert.Contains("public int OrderId { get; set; }", code);

        Assert.Contains("[Jaunty.Attributes.Column(\"unit_price\")]", code);
        Assert.Contains("public decimal? UnitPrice { get; set; }", code);
    }

    [Fact]
    public void GenerateEntity_Singularize_SingularizesClassName()
    {
        var table = CreateSimpleTable(); // TableName = "Products"
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("public partial class Product", code);
        Assert.DoesNotContain("public class Products", code);
    }

    [Fact]
    public void GenerateEntity_NoSingularize_KeepsPluralName()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            Singularize = false
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("public partial class Products", code);
    }

    [Fact]
    public void GenerateEntity_WithClassSuffix_AddsSuffix()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            Singularize = true,
            ClassSuffix = "Entity"
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("public partial class ProductEntity", code);
    }

    [Fact]
    public void GenerateEntity_WithClassPrefix_AddsPrefix()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            Singularize = true,
            ClassPrefix = "Db"
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("public partial class DbProduct", code);
    }

    [Fact]
    public void GenerateEntity_PartialClass_GeneratesPartialKeyword()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            GeneratePartialClasses = true
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("public partial class", code);
    }

    [Fact]
    public void GenerateEntity_BlockScopedNamespace_GeneratesBlockFormat()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            UseFileScopedNamespace = false
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("namespace Test.Entities", code);
        Assert.Contains("{", code);
        Assert.DoesNotContain("namespace Test.Entities;", code);
    }

    [Fact]
    public void GenerateEntity_NoTableAttribute_OmitsTableAttribute()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            GenerateTableAttribute = false
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.DoesNotContain("Jaunty.Attributes.Table(", code);
    }

    [Fact]
    public void GenerateEntity_NoColumnAttribute_OmitsColumnAttribute()
    {
        var table = CreateSnakeCaseTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            GenerateColumnAttribute = false
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.DoesNotContain("Jaunty.Attributes.Column(", code);
    }

    [Fact]
    public void GenerateEntity_NoKeyAttribute_OmitsKeyAttribute()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            GenerateKeyAttribute = false
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.DoesNotContain("Jaunty.Attributes.Key", code);
    }

    [Fact]
    public void GenerateEntity_NoDatabaseGeneratedAttribute_OmitsDatabaseGeneratedAttribute()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions
        {
            Namespace = "Test.Entities",
            GenerateDatabaseGeneratedAttribute = false
        };

        var code = _generator.GenerateEntity(table, options);

        Assert.DoesNotContain("Jaunty.Attributes.DatabaseGenerated(", code);
    }

    [Fact]
    public void GenerateEntity_TableWithNoSchema_OmitsSchemaInAttribute()
    {
        var table = new TableSchema
        {
            SchemaName = "",
            TableName = "simple_table",
            Columns =
            [
                new ColumnSchema
                {
                    ColumnName = "id",
                    DataType = "int",
                    IsPrimaryKey = true,
                    OrdinalPosition = 1
                }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        // Should have [Table("simple_table")] without schema
        Assert.Contains("[Jaunty.Attributes.Table(\"simple_table\")]", code);
        Assert.DoesNotContain("[Jaunty.Attributes.Table(\"simple_table\", \"\")]", code);
    }

    [Fact]
    public void GenerateEntity_DoesNotEmitJauntyAttributesUsing()
    {
        // Jaunty's [Table]/[Column]/[Key]/[DatabaseGenerated] attributes are emitted fully
        // qualified rather than via "using Jaunty.Attributes;", so the generated file never
        // needs that using and can't collide with it.
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.DoesNotContain("using Jaunty.Attributes;", code);
    }

    [Fact]
    public void GenerateEntity_WithSystemNamespaceColumnType_EmitsSystemUsing()
    {
        // AUD-R18 batch-8: CSharpTypeInfo.RequiredUsing exists precisely so a mapped type like
        // Guid/DateOnly can request "using System;" - no ITypeMapper populated it, so generated
        // entities referencing these types failed to compile under ImplicitUsings=disable.
        var table = new TableSchema
        {
            SchemaName = "dbo",
            TableName = "Widgets",
            Columns =
            [
                new ColumnSchema
                {
                    ColumnName = "WidgetId",
                    DataType = "uniqueidentifier",
                    IsNullable = false,
                    IsPrimaryKey = true,
                    OrdinalPosition = 1
                }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("using System;", code);
    }

    [Fact]
    public void GenerateEntity_WithDataAnnotationsAndDefaultAttributeFlags_DoesNotEmitAmbiguousUsings()
    {
        // Regression test: AddDataAnnotations=true combined with the default-on
        // GenerateTableAttribute/GenerateColumnAttribute/GenerateKeyAttribute/
        // GenerateDatabaseGeneratedAttribute flags used to emit both
        // "using Jaunty.Attributes;" and "using System.ComponentModel.DataAnnotations(.Schema);"
        // while writing [Table]/[Column]/[Key]/[DatabaseGenerated] unqualified, which fails to
        // compile with CS0104 ambiguous-reference errors since both namespaces define
        // identically-named attribute types.
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions { Namespace = "Test.Entities", AddDataAnnotations = true };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("using System.ComponentModel.DataAnnotations;", code);
        Assert.Contains("using System.ComponentModel.DataAnnotations.Schema;", code);
        Assert.DoesNotContain("using Jaunty.Attributes;", code);
        Assert.Contains("[Jaunty.Attributes.Table(\"Products\", \"dbo\")]", code);
        Assert.Contains("[Jaunty.Attributes.Key]", code);
        Assert.Contains("[Jaunty.Attributes.DatabaseGenerated(Jaunty.Attributes.DatabaseGeneratedOption.Identity)]", code);
    }

    [Fact]
    public void GenerateEntity_WithDataAnnotations_EmitsRequiredAndMaxLength()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions { Namespace = "Test.Entities", AddDataAnnotations = true };

        var code = _generator.GenerateEntity(table, options);

        // ProductName is a non-nullable string with MaxLength=100.
        Assert.Contains("[Required]", code);
        Assert.Contains("[MaxLength(100)]", code);
    }

    [Fact]
    public void GenerateEntity_DataAnnotationsNullableColumn_OmitsRequired()
    {
        var table = CreateSimpleTable();
        var options = new CodeGeneratorOptions { Namespace = "Test.Entities", AddDataAnnotations = true };

        var code = _generator.GenerateEntity(table, options);

        // UnitPrice is nullable decimal (a value type), so neither [Required] (nullable) nor
        // [MaxLength] (not a string) should be attached to it.
        Assert.Contains("public decimal? UnitPrice { get; set; }", code);
    }

    [Fact]
    public void GenerateEntity_ComputedColumn_GeneratesDatabaseGeneratedComputedAttribute()
    {
        var table = new TableSchema
        {
            SchemaName = "dbo",
            TableName = "Products",
            Columns =
            [
                new ColumnSchema { ColumnName = "Id", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 },
                new ColumnSchema { ColumnName = "Total", DataType = "decimal", IsComputed = true, OrdinalPosition = 2 }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.DatabaseGenerated(Jaunty.Attributes.DatabaseGeneratedOption.Computed)]", code);
    }

    [Fact]
    public void GenerateEntity_NonNullableByteArrayColumn_GeneratesEmptyArrayDefault()
    {
        var table = new TableSchema
        {
            SchemaName = "dbo",
            TableName = "Products",
            Columns =
            [
                new ColumnSchema { ColumnName = "Id", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 },
                new ColumnSchema { ColumnName = "RowVersion", DataType = "varbinary", IsNullable = false, OrdinalPosition = 2 }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("public byte[] RowVersion { get; set; } = [];", code);
    }

    // ------------------------------------------------------------------
    // String literal escaping
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateEntity_TableNameWithQuote_EscapesTableAttribute()
    {
        var table = new TableSchema
        {
            SchemaName = "dbo",
            TableName = "weird\"table",
            Columns =
            [
                new ColumnSchema { ColumnName = "id", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.Table(\"weird\\\"table\", \"dbo\")]", code);
    }

    [Fact]
    public void GenerateEntity_TableNameWithBackslash_EscapesTableAttribute()
    {
        var table = new TableSchema
        {
            SchemaName = "",
            TableName = @"weird\table",
            Columns =
            [
                new ColumnSchema { ColumnName = "id", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.Table(\"weird\\\\table\")]", code);
    }

    [Fact]
    public void GenerateEntity_ColumnNameWithQuote_EscapesColumnAttribute()
    {
        var table = new TableSchema
        {
            SchemaName = "",
            TableName = "products",
            Columns =
            [
                new ColumnSchema { ColumnName = "id", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 },
                new ColumnSchema { ColumnName = "weird\"column", DataType = "int", OrdinalPosition = 2 }
            ]
        };

        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Jaunty.Attributes.Column(\"weird\\\"column\")]", code);
    }

    // ------------------------------------------------------------------
    // Member name collision handling
    // ------------------------------------------------------------------

    [Fact]
    public void GenerateEntity_ColumnNameEqualsClassName_DisambiguatesProperty()
    {
        // Table "Product" with a column literally named "Product" would otherwise generate
        // "public int Product { get; set; }" inside "class Product" - CS0542.
        var table = new TableSchema
        {
            SchemaName = "",
            TableName = "Product",
            Columns =
            [
                new ColumnSchema { ColumnName = "Product", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 },
                new ColumnSchema { ColumnName = "Name", DataType = "nvarchar", OrdinalPosition = 2 }
            ]
        };
        var options = new CodeGeneratorOptions { Namespace = "Test.Entities", Singularize = false };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("public partial class Product", code);
        Assert.DoesNotContain("public int Product { get; set; }", code);
        Assert.Contains("public int Product1 { get; set; }", code);
        Assert.Contains("[Jaunty.Attributes.Column(\"Product\")]", code);
    }

    [Fact]
    public void GenerateEntity_TwoColumnsNormalizeToSameName_DisambiguatesSecondProperty()
    {
        // "order_id" and "OrderId" both PascalCase to "OrderId".
        var table = new TableSchema
        {
            SchemaName = "",
            TableName = "orders",
            Columns =
            [
                new ColumnSchema { ColumnName = "order_id", DataType = "int", IsPrimaryKey = true, OrdinalPosition = 1 },
                new ColumnSchema { ColumnName = "OrderId", DataType = "int", OrdinalPosition = 2 }
            ]
        };
        var options = new CodeGeneratorOptions { Namespace = "Test.Entities", Singularize = false };

        var code = _generator.GenerateEntity(table, options);

        Assert.Contains("[Jaunty.Attributes.Column(\"order_id\")]", code);
        Assert.Contains("public int OrderId { get; set; }", code);

        Assert.Contains("[Jaunty.Attributes.Column(\"OrderId\")]", code);
        Assert.Contains("public int OrderId1 { get; set; }", code);
    }
}