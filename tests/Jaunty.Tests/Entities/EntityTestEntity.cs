using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Test entity implementing IEntity&lt;long&gt; for Delete&lt;T, TId&gt; tests.
/// </summary>
[Table("bulk_test")]
public partial class EntityTestEntity : IEntity<long>
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("value")]
    public int Value { get; set; }
}