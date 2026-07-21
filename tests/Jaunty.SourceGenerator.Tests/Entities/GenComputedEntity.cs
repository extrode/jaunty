using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// Dedicated to EntityMetadataSourceEmissionTests only, mirroring GenIdentityEntity - proves the
// generator's IsComputed detection (DatabaseGeneratedOption.Computed, value 2) reaches the
// emitted EntityColumnInfo.IsComputed, alongside the existing Identity (value 1) coverage.
[Table("gen_computed_entities")]
public partial class GenComputedEntity : IMapped<GenComputedEntity>
{
    [Key]
    [Column("entity_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EntityId { get; set; }

    [Column("label")]
    public string Label { get; set; } = string.Empty;

    [Column("computed_value")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public string ComputedValue { get; set; } = string.Empty;
}
