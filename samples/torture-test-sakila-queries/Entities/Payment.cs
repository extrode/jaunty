using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("payment")]
public partial class Payment
{
    [Key]
    [Column("payment_id")]
    public int PaymentId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("staff_id")]
    public int StaffId { get; set; }

    [Column("rental_id")]
    public int? RentalId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; }
}
