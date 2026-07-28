using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-5): the generator infers identity for an int/long key carrying no
// [DatabaseGenerated]. Applied per property, as it was, that marked *every* int key column
// identity - so this entity had both OrderId and ProductId dropped from InsertColumns and from
// BindInsert, and the generated INSERT wrote a row with no key values at all.
//
// No database has two identity columns, so there is no schema for which that emission is correct.
// Modelled on Jaunty.Tests.Entities.OrderDetail, which is the real composite-key entity in the
// suite and is `partial` - it would have hit exactly this the moment Jaunty.Tests gained the
// generator.
[Table("gen_composite_key_entities")]
public partial class GenCompositeKeyEntity : IMapped<GenCompositeKeyEntity>
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

// The single-key counterpart, so the tests can show the convention is withdrawn only where it is
// indefensible and still applies where the generator has always applied it.
[Table("gen_single_key_entities")]
public partial class GenSingleKeyEntity : IMapped<GenSingleKeyEntity>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

// A composite key whose parts are explicitly attributed. An explicit [DatabaseGenerated] is never
// withdrawn - only the inference is - so this one keeps IsIdentity on the attributed column even
// though the entity has two keys.
[Table("gen_explicit_composite_entities")]
public partial class GenExplicitCompositeEntity : IMapped<GenExplicitCompositeEntity>
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
