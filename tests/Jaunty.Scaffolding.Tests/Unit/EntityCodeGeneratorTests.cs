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
        Assert.Contains("public class Product", code);
        Assert.Contains("[Table(\"Products\", \"dbo\")]", code);
    }

    [Fact]
    public void GenerateEntity_WithPrimaryKey_GeneratesKeyAttribute()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[Key]", code);
    }

    [Fact]
    public void GenerateEntity_WithIdentity_GeneratesDatabaseGeneratedAttribute()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]", code);
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

        Assert.Contains("[Column(\"order_detail_id\")]", code);
        Assert.Contains("public int OrderDetailId { get; set; }", code);

        Assert.Contains("[Column(\"order_id\")]", code);
        Assert.Contains("public int OrderId { get; set; }", code);

        Assert.Contains("[Column(\"unit_price\")]", code);
        Assert.Contains("public decimal? UnitPrice { get; set; }", code);
    }

    [Fact]
    public void GenerateEntity_Singularize_SingularizesClassName()
    {
        var table = CreateSimpleTable(); // TableName = "Products"
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("public class Product", code);
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

        Assert.Contains("public class Products", code);
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

        Assert.Contains("public class ProductEntity", code);
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

        Assert.Contains("public class DbProduct", code);
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

        Assert.DoesNotContain("[Table(", code);
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

        Assert.DoesNotContain("[Column(", code);
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

        Assert.DoesNotContain("[Key]", code);
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

        Assert.DoesNotContain("[DatabaseGenerated(", code);
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
        Assert.Contains("[Table(\"simple_table\")]", code);
        Assert.DoesNotContain("[Table(\"simple_table\", \"\")]", code);
    }

    [Fact]
    public void GenerateEntity_IncludesJauntyAttributesUsing()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        Assert.Contains("using Jaunty.Attributes;", code);
    }
}
