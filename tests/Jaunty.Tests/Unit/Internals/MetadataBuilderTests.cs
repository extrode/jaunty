using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Internals;

public class MetadataBuilderTests
{
    #region Test Entities

    public class SimpleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    [Table("custom_table")]
    public class TableAttributeEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Table("schema_table", "myschema")]
    public class SchemaEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ColumnAttributeEntity
    {
        [Column("entity_id")]
        public int Id { get; set; }

        [Column("full_name")]
        public string Name { get; set; } = string.Empty;
    }

    public class KeyAttributeEntity
    {
        [Key]
        public int MyKey { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class CompositeKeyEntity
    {
        [Key]
        public int Key1 { get; set; }

        [Key]
        public int Key2 { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class IgnoredPropertyEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [Ignore]
        public string Ignored { get; set; } = string.Empty;
    }

    public class DatabaseGeneratedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
    }

    public class ConventionIdEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TypeNameIdEntity
    {
        public int TypeNameIdEntityId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ReadOnlyPropertyEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ReadOnly => $"Computed: {Name}";
    }

    public class IndexerEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // AUD-R22: a *writable* indexer passes the pre-existing CanWrite check, unlike a get-only
        // one, so it actually exercises MetadataBuilder's own indexer guard rather than being
        // filtered out incidentally by CanWrite.
        public string this[int index]
        {
            get => Name;
            set => Name = value;
        }
    }

    public abstract class AbstractEntity
    {
        public int Id { get; set; }
    }

    public class DuplicateColumnNameEntity
    {
        [Column("Foo")]
        public int A { get; set; }

        [Column("foo")]
        public string B { get; set; } = string.Empty;
    }

    [System.ComponentModel.DataAnnotations.Schema.Table("da_table")]
    public class DataAnnotationsTableWithoutSchemaEntity
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column]
        public string Name { get; set; } = string.Empty;
    }

    #endregion

    [Fact]
    public void Build_SimpleEntity_ResolvesTableNameFromTypeName()
    {
        var metadata = MetadataBuilder.Build<SimpleEntity>();

        Assert.Equal("SimpleEntity", metadata.TableName);
    }

    [Fact]
    public void Build_SimpleEntity_SchemaIsNull()
    {
        var metadata = MetadataBuilder.Build<SimpleEntity>();

        Assert.Null(metadata.SchemaName);
    }

    [Fact]
    public void Build_SimpleEntity_ResolvesAllWritableProperties()
    {
        var metadata = MetadataBuilder.Build<SimpleEntity>();

        Assert.Equal(3, metadata.Columns.Count);
    }

    [Fact]
    public void Build_SimpleEntity_IdConventionDetectsKey()
    {
        var metadata = MetadataBuilder.Build<SimpleEntity>();

        Assert.Single(metadata.PrimaryKeys);
        Assert.Equal("Id", metadata.PrimaryKeys[0].Property.Name);
    }

    [Fact]
    public void Build_TableAttribute_UsesCustomTableName()
    {
        var metadata = MetadataBuilder.Build<TableAttributeEntity>();

        Assert.Equal("custom_table", metadata.TableName);
    }

    [Fact]
    public void Build_SchemaAttribute_UsesSchema()
    {
        var metadata = MetadataBuilder.Build<SchemaEntity>();

        Assert.Equal("myschema", metadata.SchemaName);
        Assert.Equal("schema_table", metadata.TableName);
    }

    [Fact]
    public void Build_ColumnAttribute_UsesCustomColumnNames()
    {
        var metadata = MetadataBuilder.Build<ColumnAttributeEntity>();

        var idCol = metadata.Columns.First(c => c.Property.Name == "Id");
        var nameCol = metadata.Columns.First(c => c.Property.Name == "Name");

        Assert.Equal("entity_id", idCol.ColumnName);
        Assert.Equal("full_name", nameCol.ColumnName);
    }

    [Fact]
    public void Build_KeyAttribute_IdentifiesKey()
    {
        var metadata = MetadataBuilder.Build<KeyAttributeEntity>();

        Assert.Single(metadata.PrimaryKeys);
        Assert.Equal("MyKey", metadata.PrimaryKeys[0].Property.Name);
    }

    [Fact]
    public void Build_CompositeKey_IdentifiesBothKeys()
    {
        var metadata = MetadataBuilder.Build<CompositeKeyEntity>();

        Assert.Equal(2, metadata.PrimaryKeys.Count);
    }

    [Fact]
    public void Build_IgnoredProperty_ExcludesFromColumns()
    {
        var metadata = MetadataBuilder.Build<IgnoredPropertyEntity>();

        Assert.Equal(2, metadata.Columns.Count);
        Assert.DoesNotContain(metadata.Columns, c => c.Property.Name == "Ignored");
    }

    [Fact]
    public void Build_DatabaseGeneratedIdentity_SetsIsIdentity()
    {
        var metadata = MetadataBuilder.Build<DatabaseGeneratedEntity>();

        var idCol = metadata.Columns.First(c => c.Property.Name == "Id");
        Assert.True(idCol.IsIdentity);
        Assert.False(idCol.IsComputed);
    }

    [Fact]
    public void Build_DatabaseGeneratedComputed_SetsIsComputed()
    {
        var metadata = MetadataBuilder.Build<DatabaseGeneratedEntity>();

        var computedCol = metadata.Columns.First(c => c.Property.Name == "CreatedAt");
        Assert.True(computedCol.IsComputed);
        Assert.False(computedCol.IsIdentity);
    }

    [Fact]
    public void Build_IdentityColumn_ExcludedFromNonIdentityColumns()
    {
        var metadata = MetadataBuilder.Build<DatabaseGeneratedEntity>();

        Assert.DoesNotContain(metadata.NonIdentityColumns, c => c.Property.Name == "Id");
        Assert.Contains(metadata.NonIdentityColumns, c => c.Property.Name == "Name");
    }

    [Fact]
    public void Build_TypeNameIdConvention_DetectsKey()
    {
        var metadata = MetadataBuilder.Build<TypeNameIdEntity>();

        Assert.Single(metadata.PrimaryKeys);
        Assert.Equal("TypeNameIdEntityId", metadata.PrimaryKeys[0].Property.Name);
    }

    [Fact]
    public void Build_ReadOnlyProperty_Excluded()
    {
        var metadata = MetadataBuilder.Build<ReadOnlyPropertyEntity>();

        Assert.DoesNotContain(metadata.Columns, c => c.Property.Name == "ReadOnly");
        Assert.Equal(2, metadata.Columns.Count);
    }

    [Fact]
    public void Build_IndexerProperty_Excluded()
    {
        var metadata = MetadataBuilder.Build<IndexerEntity>();

        Assert.Equal(2, metadata.Columns.Count);
    }

    [Fact]
    public void Build_AbstractType_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MetadataBuilder.Build<AbstractEntity>());

        Assert.Contains("cannot be abstract", ex.Message);
    }

    [Fact]
    public void Build_NonPrimaryKeyColumns_ExcludesKeys()
    {
        var metadata = MetadataBuilder.Build<SimpleEntity>();

        Assert.DoesNotContain(metadata.NonPrimaryKeyColumns, c => c.IsPrimaryKey);
        Assert.Equal(2, metadata.NonPrimaryKeyColumns.Count);
    }

    [Fact]
    public void Build_ColumnWithoutAttribute_UsesPropertyName()
    {
        var metadata = MetadataBuilder.Build<SimpleEntity>();

        var nameCol = metadata.Columns.First(c => c.Property.Name == "Name");
        Assert.Equal("Name", nameCol.ColumnName);
    }

    [Fact]
    public void Build_DuplicateColumnNameCaseInsensitive_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            MetadataBuilder.Build<DuplicateColumnNameEntity>());
    }

    [Fact]
    public void Build_DataAnnotationsTableWithoutSchema_DoesNotThrow()
    {
        var metadata = MetadataBuilder.Build<DataAnnotationsTableWithoutSchemaEntity>();

        Assert.Equal("da_table", metadata.TableName);
        Assert.Null(metadata.SchemaName);

        var nameCol = metadata.Columns.First(c => c.Property.Name == "Name");
        Assert.Equal("Name", nameCol.ColumnName);
    }

    // AUD-R32-006: ColumnAttribute's constructor rejects null but not "", and this was the one
    // resolution in Build<T> without an IsNullOrEmpty guard - so [Column("")] mapped the property
    // to an empty column name while the DataAnnotations-compat path on the same property fell
    // back to the default.

    public class EmptyColumnNameEntity
    {
        public int Id { get; set; }

        [Column("")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Build_EmptyColumnAttributeName_FallsBackToThePropertyName()
    {
        var metadata = MetadataBuilder.Build<EmptyColumnNameEntity>();

        var nameCol = metadata.Columns.First(c => c.Property.Name == "Name");
        Assert.Equal("Name", nameCol.ColumnName);
    }

    [Fact]
    public void Build_NonEmptyColumnAttributeName_StillWins()
    {
        var metadata = MetadataBuilder.Build<ColumnAttributeEntity>();

        Assert.All(metadata.Columns, c => Assert.False(string.IsNullOrEmpty(c.ColumnName)));
    }
}