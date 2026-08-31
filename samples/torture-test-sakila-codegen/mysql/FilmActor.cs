using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("film_actor")]
public class FilmActor
{
    [Key]
    [Column("actor_id")]
    public int ActorId { get; set; }

    [Key]
    [Column("film_id")]
    public int FilmId { get; set; }

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
