using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("rental", "dbo")]
public class Rental
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
