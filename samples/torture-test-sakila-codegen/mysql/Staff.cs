using Jaunty.Attributes;

namespace Sakila.Entities;

public class Staff
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("staff_id")]
    public int StaffId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Column("address_id")]
    public int AddressId { get; set; }

    public byte[]? Picture { get; set; }

    public string? Email { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    public sbyte Active { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
