using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.Fluent.SourceGen.Tests.Entities;

// Natural (non-identity) key, matching Jaunty.Tests' UpsertTests.cs convention of testing
// Upsert only against a non-identity table - MERGE/ON CONFLICT SQL generation for an
// identity primary key is a separate, pre-existing gap unrelated to metadata resolution
// (SQL Server's MERGE "source" derived table excludes identity columns, but CrudSqlCache's
// generated ON clause still references them - out of scope for spec 003).
[Table("crud_sourcegen_settings")]
public partial class CrudSourceGenSetting : IMapped<CrudSourceGenSetting>
{
    [Key]
    [Column("setting_key")]
    public string SettingKey { get; set; } = string.Empty;

    [Column("setting_value")]
    public string SettingValue { get; set; } = string.Empty;
}
