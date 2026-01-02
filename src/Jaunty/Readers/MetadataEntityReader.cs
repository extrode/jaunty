using System.Data;

using Jaunty.Internal.Mapping;

namespace Jaunty.Readers;

internal static class MetadataEntityReader
{
    public static IEnumerable<T> ReadEntities<T>(IDataReader reader, MappingMode mode) where T : new()
    {
        var setters = MetadataCache<T>.GetSetters(reader, mode);

        while (reader.Read())
        {
            var entity = new T();
            for (int i = 0; i < setters.Length; i++)
                setters[i].Set(entity, reader);

            yield return entity;
        }
    }
}