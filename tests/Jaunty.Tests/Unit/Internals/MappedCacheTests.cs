using System.Data;

using Jaunty.Interfaces;
using Jaunty.Internals.Read;

namespace Jaunty.Tests.Unit.Internals;

public class MappedCacheTests
{
    #region Test Entities

    public class PlainEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class MappedEntity : IMapped<MappedEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

#if NET8_0_OR_GREATER
        public static MappedEntity ReadEntity(IDataReader reader)
#else
        public MappedEntity ReadEntity(IDataReader reader)
#endif
        {
            return new MappedEntity
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            };
        }
    }

    #endregion

    [Fact]
    public void MappedCache_PlainEntity_MapperIsNull()
    {
        var mapper = MappedCache<PlainEntity>.Mapper;

        Assert.Null(mapper);
    }

    [Fact]
    public void MappedCache_MappedEntity_MapperIsNotNull()
    {
        var mapper = MappedCache<MappedEntity>.Mapper;

        Assert.NotNull(mapper);
    }

    [Fact]
    public void MappedCache_MappedEntity_MapperIsCachedSameReference()
    {
        var mapper1 = MappedCache<MappedEntity>.Mapper;
        var mapper2 = MappedCache<MappedEntity>.Mapper;

        Assert.Same(mapper1, mapper2);
    }
}