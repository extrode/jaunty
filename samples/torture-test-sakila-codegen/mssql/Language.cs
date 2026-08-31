using Jaunty.Attributes;

namespace Sakila.Entities;

[Table("language", "dbo")]
public class Language
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("language_id")]
    public int LanguageId { get; set; }

    public string Name { get; set; } = string.Empty;

    [Column("last_update")]
    public DateTime LastUpdate { get; set; }
}
