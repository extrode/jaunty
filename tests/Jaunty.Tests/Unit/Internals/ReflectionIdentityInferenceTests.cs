using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R25 (B8-5): the reflection side of the identity-inference divergence.
///
/// <para>
/// <c>MetadataBuilder</c> and <c>JauntyGenerator</c> derive <c>isKey</c> identically - <c>[Key]</c>,
/// or a property named <c>Id</c> or <c>{ClassName}Id</c>, case-insensitively - so both agree on
/// which column is the key. They disagree on whether the database generates it. The generator treats
/// a single int/long key with no <c>[DatabaseGenerated]</c> as an identity column and omits it from
/// the INSERT; <c>MetadataBuilder</c> leaves <c>genOption</c> null, <c>ColumnMetadata</c> sets
/// <c>IsIdentity = false</c>, and the column stays in. <c>DrDispatcher</c> prefers the generated
/// mapper when one exists, so merely adding or removing the <c>Jaunty.SourceGenerator</c> package
/// reference silently changes the SQL for such an entity.
/// </para>
///
/// <para>
/// Neither behaviour is being changed here - converging them is a product decision, and existing
/// tests depend on both. What was missing is that neither side was pinned on this exact point:
/// <c>CrudSqlCacheTests.GetSql_SimpleItem_GeneratesInsertSql</c> asserts <c>@name</c> and
/// <c>@value</c> but never says whether <c>@id</c> is there, and <c>MetadataBuilderTests</c>'
/// convention-key tests assert <c>PrimaryKeys</c> and never <c>IsIdentity</c>. So the divergence was
/// true but untested on both sides, and either path could have drifted without a failure. The
/// generator side is pinned by
/// <c>Jaunty.SourceGenerator.Tests.GeneratedIdentityInferenceTests</c>.
/// </para>
///
/// <para>
/// Asserted against <c>EntityMetadata.InsertColumns</c> rather than against generated SQL text, so
/// these run everywhere: <c>CrudSqlCache.GetSql</c> needs a live connection to pick a dialect, and
/// the SQLite-backed tests cannot run on macOS arm64 (no <c>osx-arm64</c> SQLite.Interop.dll).
/// <c>InsertColumns</c> is the decision point anyway - <c>CrudSqlCache.BuildInsertSql</c> reads it
/// and never inspects <c>IsIdentity</c> itself, and the column-list-to-SQL step is already covered
/// by <c>CrudSqlCacheTests</c>.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class ReflectionIdentityInferenceTests
{
    public ReflectionIdentityInferenceTests() => JauntyReflectionExtensions.UseReflectionMapping();

    [Table("reflection_attributed_key_items")]
    public class AttributedKeyItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("reflection_convention_key_items")]
    public class ConventionKeyItem
    {
        // No [Key] at all - the key is found by the shared "named Id" convention.
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("reflection_composite_key_items")]
    public class CompositeKeyItem
    {
        [Key]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Key]
        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("quantity")]
        public short Quantity { get; set; }
    }

    [Table("reflection_explicit_identity_items")]
    public class ExplicitIdentityItem
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void AnIntKeyWithNoDatabaseGenerated_IsNotIdentity()
    {
        EntityMetadata metadata = Metadata<AttributedKeyItem>();

        ColumnMetadata id = Assert.Single(metadata.Columns, c => c.ColumnName == "id");

        Assert.True(id.IsPrimaryKey);
        Assert.False(id.IsIdentity);
    }

    [Fact]
    public void AnIntKeyWithNoDatabaseGenerated_StaysInTheInsertColumns()
    {
        // The generator would omit this column. This path sends it.
        Assert.Contains("id", InsertColumnNames<AttributedKeyItem>());
        Assert.Contains("name", InsertColumnNames<AttributedKeyItem>());
    }

    [Fact]
    public void AConventionNamedKeyWithNoDatabaseGenerated_AlsoStaysInTheInsertColumns()
    {
        // Same answer whether the key came from [Key] or from the name convention - the divergence
        // is about [DatabaseGenerated], not about how the key was found.
        EntityMetadata metadata = Metadata<ConventionKeyItem>();
        ColumnMetadata id = Assert.Single(metadata.Columns, c => c.ColumnName == "id");

        Assert.True(id.IsPrimaryKey);
        Assert.False(id.IsIdentity);
        Assert.Contains("id", InsertColumnNames<ConventionKeyItem>());
    }

    [Fact]
    public void ACompositeKey_KeepsEveryKeyColumnInTheInsertColumns()
    {
        // The one place the two paths now agree for an unattributed int key. The generator used to
        // mark every int key identity and drop all of them, writing a row with no key values; it
        // now withdraws the inference for composite keys, matching this.
        IReadOnlyList<string> columns = InsertColumnNames<CompositeKeyItem>();

        Assert.Contains("order_id", columns);
        Assert.Contains("product_id", columns);
        Assert.Contains("quantity", columns);
    }

    [Fact]
    public void AnExplicitDatabaseGenerated_IsHonouredHereJustAsInTheGenerator()
    {
        // Writing the attribute is what makes the two paths agree, which is the advice
        // docs/01-api-reference/attributes.md now gives.
        EntityMetadata metadata = Metadata<ExplicitIdentityItem>();
        ColumnMetadata id = Assert.Single(metadata.Columns, c => c.ColumnName == "id");

        Assert.True(id.IsIdentity);
        Assert.DoesNotContain("id", InsertColumnNames<ExplicitIdentityItem>());
        Assert.Contains("name", InsertColumnNames<ExplicitIdentityItem>());
    }

    private static EntityMetadata Metadata<T>() =>
        (EntityMetadata)JauntyConfig.ReflectionTableMetadataResolver!(typeof(T));

    private static IReadOnlyList<string> InsertColumnNames<T>() =>
        [.. Metadata<T>().InsertColumns.Select(c => c.ColumnName)];
}
