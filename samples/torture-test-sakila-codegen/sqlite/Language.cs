using Jaunty.Attributes;

namespace Sakila.Entities;

public class Language
{
    [Key]
    [Column("language_id")]
    public long LanguageId { get; set; }

    public string Name { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
