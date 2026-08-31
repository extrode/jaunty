using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("payment_p2022_05", "public")]
public class PaymentP202205
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
    public int RentalId { get; set; }

    public decimal Amount { get; set; }

    [Key]
    [Column("payment_date")]
    public DateTimeOffset PaymentDate { get; set; }
}
