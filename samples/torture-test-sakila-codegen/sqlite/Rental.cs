using Jaunty.Attributes;

namespace Sakila.Entities;

public class Rental
{
    [Key]
    [Column("rental_id")]
    public long RentalId { get; set; }

    [Column("rental_date")]
    public DateTime RentalDate { get; set; }

    [Column("inventory_id")]
    public long InventoryId { get; set; }

    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("return_date")]
    public DateTime? ReturnDate { get; set; }

    [Column("staff_id")]
    public long StaffId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
