using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

using Jaunty.Interfaces;

namespace Jaunty.Benchmarks.Entities;

[Table("benchmark_products")]
public partial class JauntyProduct : IMapped<JauntyProduct>
{
    [Key]
    [Column("product_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("units_in_stock")]
    public int UnitsInStock { get; set; }

    [Column("discontinued")]
    public bool Discontinued { get; set; }

    // Source-generated via JauntyGenerator (ReadEntity, BindInsert, etc.)
    // The IMapped<T> interface signals the generator to emit these methods.
}