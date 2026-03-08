using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Test entity for bulk operation tests.
/// Uses a dedicated test table that can be created and dropped.
/// </summary>
[Table("bulk_test")]
public partial class BulkTestEntity
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