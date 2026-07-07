using Jaunty.Attributes;

namespace SakilaQueries.Entities;

[Table("film_actor")]
public partial class FilmActor
{
    [Key]
    [Column("actor_id")]
    public int ActorId { get; set; }

    [Key]
    [Column("film_id")]
    public int FilmId { get; set; }
}
