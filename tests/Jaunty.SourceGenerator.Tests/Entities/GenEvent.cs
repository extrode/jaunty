using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

[Table("gen_events")]
public partial class GenEvent : IMapped<GenEvent>
{
    [Key]
    [Column("event_id")]
    public int EventId { get; set; }

    [Column("occurred_at")]
    public DateTime? OccurredAt { get; set; }

    [Column("trace_id")]
    public Guid? TraceId { get; set; }
}
