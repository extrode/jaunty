using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

[Table("offset_records")]
public class OffsetRecord
{
    [Key]
    public int Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
