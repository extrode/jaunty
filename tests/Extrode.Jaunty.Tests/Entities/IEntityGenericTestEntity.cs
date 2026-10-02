using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

namespace Extrode.Jaunty.Tests.Entities;

/// <summary>
/// Test entity implementing IEntity&lt;int&gt; for WriteParameterCache tests.
/// </summary>
[Table("bulk_test")]
public partial class IEntityGenericTestEntity : IEntity<int>
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("value")]
    public int Value { get; set; }
}
