using Jaunty.Attributes;
using Jaunty.Interfaces;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Write;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests WriteParameterCache&lt;T&gt;.IdSetter resolution, in particular the fallback branch for
/// IEntity&lt;TId&gt; primary keys that cannot receive a database-generated long identity value.
/// </summary>
public class WriteParameterCacheTests
{
    [Table("guid_key_items")]
    public class GuidKeyEntity : IEntity<Guid>
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    // R23 batch-3: CreateIdSetter()'s IsConvertibleFromInt64 false branch (Guid/string TId) was
    // never exercised by any test - every IEntity<TId> test entity in the repo used int/long.
    [Fact]
    public void IdSetter_ForGuidPrimaryKey_IsNull()
    {
        Assert.Null(WriteParameterCache<GuidKeyEntity>.IdSetter);
    }
}
