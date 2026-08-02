using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

/// <summary>
/// AUD-R34-032. Its own entity type, because the thing under test is a <c>[ThreadStatic]</c> slot
/// on the generated per-entity <c>OrdinalMap</c>: sharing a type with another test would let that
/// test's last resolve decide this one's answer.
/// </summary>
[Table("gen_lifetime")]
public partial class GenLifetimeEntity : IMapped<GenLifetimeEntity>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
