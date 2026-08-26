using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

public enum GenFallbackGrade
{
    Low = 0,
    High = 1
}

/// <summary>
/// AUD-R35-118. A ulong-backed enum with a member above long.MaxValue: the core converter widens
/// through the enum's own underlying type, the generated helper used to widen through long.
/// </summary>
public enum GenFallbackHuge : ulong
{
    None = 0,
    Max = ulong.MaxValue
}

/// <summary>
/// AUD-R34-033. One property per type that lands in <c>GetReaderTypeInfo</c>'s catch-all arm, so the
/// emitted <c>ReadFallback&lt;T&gt;</c> helper's conversion branches are all reachable from one
/// entity and one reader. All four scaffolder type mappers emit <c>DateOnly</c> for a <c>date</c>
/// column and <c>TimeOnly</c> for a <c>time</c> one, so default scaffolder output routinely produces
/// properties that read through this helper.
/// </summary>
[Table("gen_fallbacks")]
public partial class GenFallbackEntity : IMapped<GenFallbackEntity>
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("day")]
    public DateOnly Day { get; set; }

    [Column("moment")]
    public TimeOnly Moment { get; set; }

    [Column("duration")]
    public TimeSpan Duration { get; set; }

    [Column("occurred_at")]
    public DateTimeOffset OccurredAt { get; set; }

    [Column("ref_id")]
    public Guid RefId { get; set; }

    [Column("grade")]
    public GenFallbackGrade Grade { get; set; }

    [Column("counter")]
    public uint Counter { get; set; }

    [Column("initial")]
    public char Initial { get; set; }

    [Column("huge")]
    public GenFallbackHuge Huge { get; set; }
}
