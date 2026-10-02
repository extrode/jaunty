using Extrode.Jaunty.Attributes;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

[Table("temporal_records")]
public class TemporalRecord
{
    [Key]
    public int Id { get; set; }
    public DateTime EventDate { get; set; }
    public TimeSpan EventTime { get; set; }
}
