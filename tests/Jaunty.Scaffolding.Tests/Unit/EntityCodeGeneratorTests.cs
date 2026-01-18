using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Schema;

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

        code.Should().Contain("namespace Test.Entities;");
        code.Should().Contain("public class Product");
        code.Should().Contain("[Table(\"Products\", \"dbo\")]");
    }

    [Fact]
    public void GenerateEntity_WithPrimaryKey_GeneratesKeyAttribute()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("[Key]");
    }

    [Fact]
    public void GenerateEntity_WithIdentity_GeneratesDatabaseGeneratedAttribute()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
    }

    [Fact]
    public void GenerateEntity_NullableColumn_GeneratesNullableType()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("public decimal? UnitPrice { get; set; }");
    }

    [Fact]
    public void GenerateEntity_NonNullableString_GeneratesDefaultValue()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("public string ProductName { get; set; } = string.Empty;");
    }

    [Fact]
    public void GenerateEntity_SnakeCaseColumns_GeneratesColumnAttributes()
    {
        var table = CreateSnakeCaseTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("[Column(\"order_detail_id\")]");
        code.Should().Contain("public int OrderDetailId { get; set; }");

        code.Should().Contain("[Column(\"order_id\")]");
        code.Should().Contain("public int OrderId { get; set; }");

        code.Should().Contain("[Column(\"unit_price\")]");
        code.Should().Contain("public decimal? UnitPrice { get; set; }");
    }

    [Fact]
    public void GenerateEntity_Singularize_SingularizesClassName()
    {
        var table = CreateSimpleTable(); // TableName = "Products"
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("public class Product");
        code.Should().NotContain("public class Products");
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

        code.Should().Contain("public class Products");
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

        code.Should().Contain("public class ProductEntity");
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

        code.Should().Contain("public class DbProduct");
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

        code.Should().Contain("public partial class");
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

        code.Should().Contain("namespace Test.Entities");
        code.Should().Contain("{");
        code.Should().NotContain("namespace Test.Entities;");
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

        code.Should().NotContain("[Table(");
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

        code.Should().NotContain("[Column(");
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

        code.Should().NotContain("[Key]");
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

        code.Should().NotContain("[DatabaseGenerated(");
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
        code.Should().Contain("[Table(\"simple_table\")]");
        code.Should().NotContain("[Table(\"simple_table\", \"\")]");
    }

    [Fact]
    public void GenerateEntity_IncludesJauntyAttributesUsing()
    {
        var table = CreateSimpleTable();
        var code = _generator.GenerateEntity(table, _defaultOptions);

        code.Should().Contain("using Jaunty.Attributes;");
    }
}
