using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

/// <summary>
/// Written to by one test only, so that test's first write is the first time
/// <c>WriteParameterCache&lt;FirstWriteWidget&gt;</c> is touched in the process.
/// </summary>
[Table("first_write_widgets")]
public partial class FirstWriteWidget : IMapped<FirstWriteWidget>
{
    [Key]
    public int WidgetId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
