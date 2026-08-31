using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

[Table("unsigned_counters")]
public class UnsignedCounterRecord
{
    [Key]
    public int Id { get; set; }
    public ulong Counter { get; set; }
}
