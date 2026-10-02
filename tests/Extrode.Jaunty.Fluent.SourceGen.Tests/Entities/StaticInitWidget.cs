using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

/// <summary>
/// A source-generated entity whose own static initializer reads the generated
/// <see cref="TableName"/>, which runs before the generated file's initializers.
/// </summary>
[Table("static_init_widgets")]
public partial class StaticInitWidget : IMapped<StaticInitWidget>
{
    public static readonly string SelectAll = "SELECT * FROM " + TableName;

    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
