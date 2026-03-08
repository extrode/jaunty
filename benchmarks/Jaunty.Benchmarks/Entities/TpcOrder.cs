using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Jaunty.Interfaces;

namespace Jaunty.Benchmarks.Entities;

[Table("orders")]
public partial class TpcOrder : IMapped<TpcOrder>
{
    [Key]
    [Column("o_orderkey")]
    public int OrderKey { get; set; }

    [Column("o_custkey")]
    public int CustKey { get; set; }

    [Column("o_orderstatus")]
    public string OrderStatus { get; set; } = null!;

    [Column("o_totalprice")]
    public decimal TotalPrice { get; set; }

    [Column("o_orderdate")]
    public string OrderDate { get; set; } = null!;

    [Column("o_orderpriority")]
    public string OrderPriority { get; set; } = null!;

    [Column("o_clerk")]
    public string Clerk { get; set; } = null!;

    [Column("o_shippriority")]
    public int ShipPriority { get; set; }

    [Column("o_comment")]
    public string Comment { get; set; } = null!;
}

// Dapper uses the same columns via snake_case mapping
public class DapperOrder
{
    public int o_orderkey { get; set; }
    public int o_custkey { get; set; }
    public string o_orderstatus { get; set; } = null!;
    public decimal o_totalprice { get; set; }
    public string o_orderdate { get; set; } = null!;
    public string o_orderpriority { get; set; } = null!;
    public string o_clerk { get; set; } = null!;
    public int o_shippriority { get; set; }
    public string o_comment { get; set; } = null!;
}