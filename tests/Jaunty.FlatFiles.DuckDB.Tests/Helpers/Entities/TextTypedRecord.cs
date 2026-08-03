using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

[Table("text_typed_records")]
public class TextTypedRecord
{
    [Key]
    public int Id { get; set; }
    public Guid Reference { get; set; }
    public TimeSpan Elapsed { get; set; }
    public DateOnly StartedOn { get; set; }
    public TimeOnly StartedAt { get; set; }
    public char Grade { get; set; }
}
