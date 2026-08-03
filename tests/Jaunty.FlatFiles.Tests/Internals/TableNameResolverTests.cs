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

    public class EntityDerivedFromJauntyTable : EntityWithTableAttribute
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

    // AUD-R35-242: the one inheritance shape left unpinned, and the one whose result surprises.
    // Jaunty's TableAttribute is [AttributeUsage(..., Inherited = false)], so a derived entity does
    // not inherit it and falls through to the class-name default - the mirror image of the
    // DataAnnotations case directly above, which does inherit. Core's MetadataBuilder reads the
    // attribute the same way, so this is the attribute's declaration, not a FlatFiles divergence.
    [Fact]
    public void Resolve_WithInheritedJauntyTableAttribute_FallsBackToTheClassName()
    {
        var name = TableNameResolver.Resolve<EntityDerivedFromJauntyTable>();
        Assert.Equal("entityderivedfromjauntytable", name);
    }
}
