using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("rental")]
public partial class Rental
{
    [Key]
    [Column("rental_id")]
    public int RentalId { get; set; }

    [Column("rental_date")]
    public DateTime RentalDate { get; set; }

    [Column("inventory_id")]
    public int InventoryId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("return_date")]
    public DateTime? ReturnDate { get; set; }

    [Column("staff_id")]
    public int StaffId { get; set; }
}
