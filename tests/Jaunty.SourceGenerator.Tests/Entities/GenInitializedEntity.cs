using Jaunty.Attributes;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R33-009: Name carries an initializer and Note does not, so a NULL column in each shows
// separately whether the generated mapper resets the property or leaves it alone - which is what
// the two reader branches used to disagree about.
[Table("gen_initialized")]
public partial class GenInitializedEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = "unset";

    public string? Note { get; set; }
}
