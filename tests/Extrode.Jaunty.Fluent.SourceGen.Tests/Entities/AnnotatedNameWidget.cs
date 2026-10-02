using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interfaces;

using DataAnnotations = System.ComponentModel.DataAnnotations.Schema;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

/// <summary>
/// A source-generated entity named only by the DataAnnotations <c>[Table]</c> and <c>[Column]</c>
/// attributes, which win over the <c>JauntyConfig</c> naming resolvers as Jaunty's own do.
/// </summary>
[DataAnnotations.Table("annotated_name_widgets")]
public partial class AnnotatedNameWidget : IMapped<AnnotatedNameWidget>
{
    [Key]
    public int WidgetId { get; set; }

    [DataAnnotations.Column("label")]
    public string DisplayName { get; set; } = string.Empty;

    public int UnitCount { get; set; }
}
