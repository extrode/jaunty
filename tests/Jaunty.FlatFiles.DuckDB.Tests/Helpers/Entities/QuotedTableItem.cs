using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

/// <summary>
/// Table name deliberately contains an embedded double-quote to exercise identifier
/// escaping (round 10 audit: DuckDbWrite/Update/Delete used to interpolate
/// source.TableName directly into SQL instead of going through
/// DuckDbDialect.EscapeTableName, which doubles embedded quotes).
/// </summary>
[Table("weird\"table")]
public class QuotedTableItem
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}
