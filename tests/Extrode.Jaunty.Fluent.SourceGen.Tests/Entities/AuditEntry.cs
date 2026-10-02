using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

/// <summary>
/// A source-generated entity whose own columns carry names the generator used to declare on the
/// entity, which made it fall back to reflection with JAUNTYGEN004.
/// </summary>
[Table("audit_entries")]
public partial class AuditEntry : IMapped<AuditEntry>
{
    [Key]
    public int Id { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string? SchemaName { get; set; }

    public string ParameterMap { get; set; } = string.Empty;
}
