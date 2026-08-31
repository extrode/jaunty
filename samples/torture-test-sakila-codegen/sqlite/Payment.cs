using Jaunty.Attributes;

namespace Sakila.Entities;

public class Payment
{
    [Key]
    [Column("payment_id")]
    public long PaymentId { get; set; }

    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("staff_id")]
    public long StaffId { get; set; }

    [Column("rental_id")]
    public long? RentalId { get; set; }

    public decimal Amount { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
