using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R18 batch-8: two properties mapped to the same effective column name (a literal duplicate
// here, case-variant is the other flavor) used to make the generator emit a ParameterMap
// collection initializer with a duplicate dictionary key, which compiles but throws
// ArgumentException at this type's static-constructor time. The generator now reports a
// JAUNTYGEN001 warning and keeps only the first property ("Code") in ParameterMap instead.
[Table("gen_duplicate_column_entities")]
public partial class GenDuplicateColumnEntity : IMapped<GenDuplicateColumnEntity>
{
    [Key]
    [Column("entity_id")]
    public int EntityId { get; set; }

    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("code")]
    public string LegacyCode { get; set; } = string.Empty;
}
