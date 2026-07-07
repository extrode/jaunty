using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

// DataAnnotations' attributes throughout (not Jaunty's) so the emission tests exercise
// the named-argument Schema resolution path (Jaunty's own TableAttribute.Schema is a
// get-only property and can only be set positionally).
[Table("gen_order_lines", Schema = "sales")]
public partial class GenOrderLine : IMapped<GenOrderLine>
{
    [Key]
    [Column("order_id")]
    public int OrderId { get; set; }

    [Key]
    [Column("line_number")]
    public int LineNumber { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }
}
