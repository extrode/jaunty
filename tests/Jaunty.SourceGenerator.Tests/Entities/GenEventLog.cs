using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

public enum GenEventSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// Exercises the GetValue-fallback reader types (enum, TimeSpan, DateTimeOffset) whose
/// generated DbDataReader.GetFieldValue&lt;T&gt; call previously used "object" as the type
/// argument, producing an invalid implicit cast to the property type (CS0266) - see
/// AUD-R9-006.
/// </summary>
[Table("gen_event_logs")]
public partial class GenEventLog : IMapped<GenEventLog>
{
    [Key]
    [Column("event_id")]
    public int EventId { get; set; }

    [Column("severity")]
    public GenEventSeverity Severity { get; set; }

    [Column("duration")]
    public TimeSpan Duration { get; set; }

    [Column("occurred_at")]
    public DateTimeOffset OccurredAt { get; set; }
}
