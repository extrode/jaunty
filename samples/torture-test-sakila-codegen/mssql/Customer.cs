using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("customer", "dbo")]
public class Customer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    [Column("address_id")]
    public int AddressId { get; set; }

    public string Active { get; set; } = string.Empty;

    [Column("create_date")]
    public DateTime CreateDate { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
