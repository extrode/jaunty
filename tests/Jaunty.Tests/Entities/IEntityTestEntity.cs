using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Test entity implementing non-generic IEntity for WriteParameterCache tests.
/// </summary>
[Table("bulk_test")]
public partial class IEntityTestEntity : IEntity
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