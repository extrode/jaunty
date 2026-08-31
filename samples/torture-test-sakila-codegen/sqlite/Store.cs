using Jaunty.Attributes;

namespace Sakila.Entities;

public class Store
{
    [Key]
    [Column("store_id")]
    public long StoreId { get; set; }

    [Column("manager_staff_id")]
    public long ManagerStaffId { get; set; }

    [Column("address_id")]
    public long AddressId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
