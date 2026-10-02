using System.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Import;
using Extrode.Jaunty.TypeHandlers;

namespace Extrode.Jaunty.Configuration;

/// <summary>
/// One immutable set of the settings <see cref="JauntyConfigBuilder"/> exposes. <see cref="JauntyConfig"/>
/// holds exactly one in a volatile field and replaces it whole, so a reader never sees half of one
/// configuration and half of another within a single field read.
/// </summary>
internal sealed class JauntySettings
{
    internal static readonly JauntySettings Defaults = new JauntySettings(new JauntyConfigBuilder());

    internal JauntySettings(JauntyConfigBuilder from)
    {
        SchemaNameResolver = from.SchemaNameResolver;
        TableNameResolver = from.TableNameResolver;
        ColumnNameResolver = from.ColumnNameResolver;
        ReflectionMapperResolver = from.ReflectionMapperResolver;
        ReflectionInsertBinderResolver = from.ReflectionInsertBinderResolver;
        ReflectionUpdateBinderResolver = from.ReflectionUpdateBinderResolver;
        ReflectionDeleteBinderResolver = from.ReflectionDeleteBinderResolver;
        ReflectionTableMetadataResolver = from.ReflectionTableMetadataResolver;
        ReflectionMultiMapperResolver = from.ReflectionMultiMapperResolver;
        ReflectionMultiMapperResolverN = from.ReflectionMultiMapperResolverN;
        SpecialTypeMapperResolver = from.SpecialTypeMapperResolver;
        CopyImportFactory = from.CopyImportFactory;
        DefaultEnumStorage = from.DefaultEnumStorage;
        TypeHandlers = from.TypeHandlers.ToArray();
    }

    internal Func<Type, string>? SchemaNameResolver { get; }
    internal Func<Type, string>? TableNameResolver { get; }
    internal Func<string, string>? ColumnNameResolver { get; }
    internal Func<Type, MappingMode, object>? ReflectionMapperResolver { get; }
    internal Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver { get; }
    internal Func<Type, Action<IDbCommand, object>>? ReflectionUpdateBinderResolver { get; }
    internal Func<Type, Action<IDbCommand, object>>? ReflectionDeleteBinderResolver { get; }
    internal Func<Type, object>? ReflectionTableMetadataResolver { get; }
    internal Func<Type, Type, object>? ReflectionMultiMapperResolver { get; }
    internal Func<Type[], IDataReader, Action<object, IDataRecord>[]>? ReflectionMultiMapperResolverN { get; }
    internal Func<Type, IDataReader, object>? SpecialTypeMapperResolver { get; }
    internal CopyImportFactory? CopyImportFactory { get; }
    internal EnumStorage DefaultEnumStorage { get; }
    internal KeyValuePair<Type, ITypeHandler>[] TypeHandlers { get; }

    /// <summary>
    /// Whether <paramref name="other"/> holds the same settings: each delegate by
    /// <see cref="Delegate.Equals(object)"/> (same method, same target), and the type handlers in the
    /// same order. Lets a second <see cref="JauntyConfig.Configure"/> with identical settings - a test
    /// host running <c>Program.cs</c> again - be a no-op instead of an error.
    /// </summary>
    internal bool SameAs(JauntySettings other)
    {
        if (!Equals(SchemaNameResolver, other.SchemaNameResolver)
            || !Equals(TableNameResolver, other.TableNameResolver)
            || !Equals(ColumnNameResolver, other.ColumnNameResolver)
            || !Equals(ReflectionMapperResolver, other.ReflectionMapperResolver)
            || !Equals(ReflectionInsertBinderResolver, other.ReflectionInsertBinderResolver)
            || !Equals(ReflectionUpdateBinderResolver, other.ReflectionUpdateBinderResolver)
            || !Equals(ReflectionDeleteBinderResolver, other.ReflectionDeleteBinderResolver)
            || !Equals(ReflectionTableMetadataResolver, other.ReflectionTableMetadataResolver)
            || !Equals(ReflectionMultiMapperResolver, other.ReflectionMultiMapperResolver)
            || !Equals(ReflectionMultiMapperResolverN, other.ReflectionMultiMapperResolverN)
            || !Equals(SpecialTypeMapperResolver, other.SpecialTypeMapperResolver)
            || !Equals(CopyImportFactory, other.CopyImportFactory)
            || DefaultEnumStorage != other.DefaultEnumStorage
            || TypeHandlers.Length != other.TypeHandlers.Length)
            return false;

        for (int i = 0; i < TypeHandlers.Length; i++)
        {
            if (TypeHandlers[i].Key != other.TypeHandlers[i].Key || !Equals(TypeHandlers[i].Value, other.TypeHandlers[i].Value))
                return false;
        }

        return true;
    }
}
