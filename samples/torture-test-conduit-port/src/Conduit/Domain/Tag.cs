using System.Collections.Generic;

using Extrode.Jaunty.Attributes;

namespace Conduit.Domain;

[Table("Tags")]
public class Tag
{
    [Key]
    public string? TagId { get; set; }

    [Ignore]
    public List<ArticleTag> ArticleTags { get; set; } = new();
}
