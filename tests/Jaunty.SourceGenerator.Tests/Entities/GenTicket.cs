using Jaunty.Attributes;
using Jaunty.Interfaces;

namespace Jaunty.SourceGenerator.Tests.Entities;

public enum GenTicketState
{
    Open = 0,
    Closed = 1
}

/// <summary>
/// Exercises the generated write path's enum conversion (AUD-R30): one property with an explicit
/// <c>[EnumStorage]</c> override in each direction, one deferring to
/// <c>JauntyConfig.DefaultEnumStorage</c>, and one nullable.
/// </summary>
[Table("gen_tickets")]
public partial class GenTicket : IMapped<GenTicket>
{
    [Key]
    [Column("ticket_id")]
    public int TicketId { get; set; }

    [Column("state_string")]
    [EnumStorage(EnumStorage.String)]
    public GenTicketState StateString { get; set; }

    [Column("state_numeric")]
    [EnumStorage(EnumStorage.Numeric)]
    public GenTicketState StateNumeric { get; set; }

    [Column("state_default")]
    public GenTicketState StateDefault { get; set; }

    [Column("state_nullable")]
    [EnumStorage(EnumStorage.String)]
    public GenTicketState? StateNullable { get; set; }
}
