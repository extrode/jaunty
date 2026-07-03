using Jaunty.Attributes;

namespace Jaunty.Tests.Entities;

[Table("typehandler_test")]
public partial class TypeHandlerTestEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("custom_value")]
    public object? CustomValue { get; set; }

    [EnumStorage(EnumStorage.String)]
    [Column("enum_string_override")]
    public TestEnumForHandlers EnumStringOverride { get; set; }

    [Column("enum_global_string")]
    public TestEnumForHandlers EnumGlobalString { get; set; }
}

public enum TestEnumForHandlers
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2
}
