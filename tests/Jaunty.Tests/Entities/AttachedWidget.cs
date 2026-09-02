using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Mapped to a table in a SQLite schema that only exists after ATTACH, so every statement Jaunty
/// generates for it has to carry the schema to reach the right database file. Paired with
/// <see cref="MainWidget"/>, which is the same table name in main.
/// </summary>
[Table("widgets", "archive")]
public partial class AttachedWidget
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// The decoy: same table name, no schema, so it resolves to main. A statement that loses the
/// schema hits this table instead of <see cref="AttachedWidget"/>'s, which is the failure the
/// SQLite schema tests exist to catch.
/// </summary>
[Table("widgets")]
public partial class MainWidget
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
