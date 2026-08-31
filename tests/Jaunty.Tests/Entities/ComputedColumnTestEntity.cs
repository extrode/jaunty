using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Source-generated test entity with a computed column, for verifying IsComputed propagates
/// through the source generator (excluding the column from generated INSERT/UPDATE SQL).
/// </summary>
[Table("computed_column_test")]
public partial class ComputedColumnTestEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("computed_value")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public string ComputedValue { get; set; } = string.Empty;
}
