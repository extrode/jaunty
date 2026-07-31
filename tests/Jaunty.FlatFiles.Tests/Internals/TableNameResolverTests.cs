using Jaunty.Attributes;
using Jaunty.FlatFiles.Internals;

namespace Jaunty.FlatFiles.Tests.Internals;

public class TableNameResolverTests
{
    [Table("custom_name")]
    public class EntityWithTableAttribute
    {
        public int Id { get; set; }
    }

    public class EntityDerivedFromDataAnnotationsTable : EntityWithDataAnnotationsTableAttribute
    {
    }

    public class EntityWithoutAttribute
    {
        public int Id { get; set; }
    }

    [Table("products", "dbo")]
    public class EntityWithSchema
    {
        public int Id { get; set; }
    }

    [System.ComponentModel.DataAnnotations.Schema.Table("da_products")]
    public class EntityWithDataAnnotationsTableAttribute
    {
        public int Id { get; set; }
    }

    [Fact]
    public void Resolve_WithTableAttribute_ReturnsAttributeName()
    {
        var name = TableNameResolver.Resolve<EntityWithTableAttribute>();
        Assert.Equal("custom_name", name);
    }

    [Fact]
    public void Resolve_WithoutAttribute_ReturnsClassNameLowercased()
    {
        var name = TableNameResolver.Resolve<EntityWithoutAttribute>();
        Assert.Equal("entitywithoutattribute", name);
    }

    [Fact]
    public void Resolve_WithSchema_ReturnsNameOnly()
    {
        // TableNameResolver returns only the table name, not the schema
        var name = TableNameResolver.Resolve<EntityWithSchema>();
        Assert.Equal("products", name);
    }

    [Fact]
    public void Resolve_ByType_WorksCorrectly()
    {
        var name = TableNameResolver.Resolve(typeof(EntityWithTableAttribute));
        Assert.Equal("custom_name", name);
    }

    [Fact]
    public void Resolve_WithDataAnnotationsTableAttribute_ReturnsAttributeName()
    {
        var name = TableNameResolver.Resolve<EntityWithDataAnnotationsTableAttribute>();
        Assert.Equal("da_products", name);
    }

    [Fact]
    public void Resolve_WithInheritedDataAnnotationsTableAttribute_ReturnsAttributeName()
    {
        var name = TableNameResolver.Resolve<EntityDerivedFromDataAnnotationsTable>();
        Assert.Equal("da_products", name);
    }
}
