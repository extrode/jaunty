using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// Dedicated to EntityMetadataSourceEmissionTests only - no other test references this entity,
// so it's free to carry DatabaseGenerated overrides without risking a ripple effect on the
// Insert/Update column sets other tests assert against for GenProduct/GenOrderLine/GenWidget.
//
// SecondaryKey exists specifically to prove [DatabaseGenerated(None)] overrides the generator's
// int-Key-defaults-to-identity convention (see JauntyGenerator.cs's PropertyMetadata isIdentity
// resolution) - EntityId's [DatabaseGenerated(Identity)] agrees with that convention, so on its
// own it wouldn't distinguish "explicit attribute" from "int Key default".
[Table("gen_identity_entities")]
public partial class GenIdentityEntity : IMapped<GenIdentityEntity>
{
    [Key]
    [Column("entity_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EntityId { get; set; }

    [Key]
    [Column("secondary_key")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int SecondaryKey { get; set; }

    [Column("label")]
    public string Label { get; set; } = string.Empty;
}
