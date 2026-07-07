using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("actor", "public")]
public class Actor
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("actor_id")]
    public int ActorId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTimeOffset LastUpdate { get; set; }
}
