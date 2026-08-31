using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("language", "public")]
public class Language
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("language_id")]
    public int LanguageId { get; set; }

    public object Name { get; set; } = null!;

    [Column("last_update")]
    public DateTimeOffset LastUpdate { get; set; }
}
