using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

/// <summary>
/// Test entity carrying a schema, used only to verify that IBulkCopyProvider.CopyToServer(Async)
/// receives EntityMetadata.SchemaName - never touches a real database (see
/// BulkInsertNativeGuardOrderTests.RecordingBulkCopyProvider).
/// </summary>
[Table("bulk_test", "custom")]
public partial class SchemaQualifiedBulkTestEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("value")]
    public int Value { get; set; }
}
