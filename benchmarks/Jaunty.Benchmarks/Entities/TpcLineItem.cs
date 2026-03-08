using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Jaunty.Interfaces;

namespace Jaunty.Benchmarks.Entities;

[Table("lineitem")]
public partial class TpcLineItem : IMapped<TpcLineItem>
{
    [Key]
    [Column("l_orderkey")]
    public int OrderKey { get; set; }

    [Column("l_partkey")]
    public int PartKey { get; set; }

    [Column("l_suppkey")]
    public int SuppKey { get; set; }

    [Key]
    [Column("l_linenumber")]
    public int LineNumber { get; set; }

    [Column("l_quantity")]
    public decimal Quantity { get; set; }

    [Column("l_extendedprice")]
    public decimal ExtendedPrice { get; set; }

    [Column("l_discount")]
    public decimal Discount { get; set; }

    [Column("l_tax")]
    public decimal Tax { get; set; }

    [Column("l_returnflag")]
    public string ReturnFlag { get; set; } = null!;

    [Column("l_linestatus")]
    public string LineStatus { get; set; } = null!;

    [Column("l_shipdate")]
    public string ShipDate { get; set; } = null!;

    [Column("l_commitdate")]
    public string CommitDate { get; set; } = null!;

    [Column("l_receiptdate")]
    public string ReceiptDate { get; set; } = null!;

    [Column("l_shipinstruct")]
    public string ShipInstruct { get; set; } = null!;

    [Column("l_shipmode")]
    public string ShipMode { get; set; } = null!;

    [Column("l_comment")]
    public string Comment { get; set; } = null!;
}

// Dapper uses the same columns via snake_case mapping
public class DapperLineItem
{
    public int l_orderkey { get; set; }
    public int l_partkey { get; set; }
    public int l_suppkey { get; set; }
    public int l_linenumber { get; set; }
    public decimal l_quantity { get; set; }
    public decimal l_extendedprice { get; set; }
    public decimal l_discount { get; set; }
    public decimal l_tax { get; set; }
    public string l_returnflag { get; set; } = null!;
    public string l_linestatus { get; set; } = null!;
    public string l_shipdate { get; set; } = null!;
    public string l_commitdate { get; set; } = null!;
    public string l_receiptdate { get; set; } = null!;
    public string l_shipinstruct { get; set; } = null!;
    public string l_shipmode { get; set; } = null!;
    public string l_comment { get; set; } = null!;
}