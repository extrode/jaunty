using Jaunty.Attributes;

namespace Sakila.Entities;

public class Actor
{
    [Key]
    [Column("actor_id")]
    public long ActorId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
