using Jaunty.Attributes;

namespace Sakila.Entities;

public class Payment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("payment_id")]
    public int PaymentId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("staff_id")]
    public int StaffId { get; set; }

    [Column("rental_id")]
    public int? RentalId { get; set; }

    public decimal Amount { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; }

    [Column("last_update")]
    public DateTime? LastUpdate { get; set; }
}
