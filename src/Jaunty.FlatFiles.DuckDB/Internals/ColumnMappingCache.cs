#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Concurrent;
using System.Reflection;

using Jaunty.Attributes;
using System.Linq.Expressions;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Caches column mappings per entity type to avoid repeated reflection.
/// Uses FrozenDictionary for optimal read performance on .NET 8+.
/// </summary>
internal static class ColumnMappingCache
{
#if NET8_0_OR_GREATER
    private static readonly ConcurrentDictionary<Type, FrozenDictionary<string, ColumnMapping>> _cache = new();
#else
    private static readonly ConcurrentDictionary<Type, Dictionary<string, ColumnMapping>> _cache = new();
#endif

    /// <summary>
    /// Gets or creates column mappings for the specified entity type.
    /// </summary>
    /// <param name="entityType">The entity type to get mappings for.</param>
    /// <returns>A read-only dictionary of column name to mapping.</returns>
    public static IReadOnlyDictionary<string, ColumnMapping> Get(Type entityType)
    {
        return _cache.GetOrAdd(entityType, type =>
        {
            var dict = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);
            // AUD-R26: assigning with dict[name] = ... silently dropped the earlier property when
            // two mapped onto one column, while TargetDdlGenerator emitted both and produced DDL
            // that SQLite and SQL Server reject. See DuplicateColumnGuard.
            Dictionary<string, string> claimed = DuplicateColumnGuard.NewClaimSet(4);

            foreach (PropertyInfo prop in MappedPropertyFilter.GetMappedProperties(type))
            {
                Type underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var columnName = GetColumnName(prop);
                DuplicateColumnGuard.Claim(type, columnName, prop, claimed);

                dict[columnName] = new ColumnMapping
                {
                    ColumnName = columnName,
                    Property = prop,
                    Getter = CreateGetter(prop),
                    Setter = CreateSetter(prop),
                    PropertyType = prop.PropertyType,
                    // R29: DateTimeOffset needs the same CAST(... AS TIMESTAMP) view treatment as
                    // DateTime, else a text-sniffed column materializes as string with no
                    // string->DateTimeOffset conversion path.
                    IsDateTime = underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset)
                };
            }

            // AUD-R35-031: an entity with no mapped properties at all - every one [Ignore]d,
            // [NotMapped] or get-only - reached every write site and failed there, differently each
            // time and never naming the entity. The batch inserts divided by mappingList.Count and
            // threw DivideByZeroException; the single-entity inserts emitted INSERT INTO "t" ()
            // VALUES () and got a raw DuckDB parser error. Reads and generated DDL fared no better,
            // producing empty objects and CREATE TABLE "t" () respectively. The condition is a
            // property of the type, not of any one call, so it is caught once here where the type is
            // first inspected - and, being inside the GetOrAdd factory, nothing is cached when it
            // throws.
            if (dict.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Entity type '{type.FullName}' has no mapped properties, so it cannot be read " +
                    "from or written to a flat file. A property is mapped when it is public, has " +
                    "both a getter and a setter, and carries neither [Ignore] nor [NotMapped].");
            }

#if NET8_0_OR_GREATER
            return dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
#else
            return dict;
#endif
        });
    }

    // AUD-R26-067: shared with ExpressionTranslator and TargetDdlGenerator - see
    // MappedPropertyFilter.GetColumnName for why the naming half of the rule moved there too.
    private static string GetColumnName(PropertyInfo prop) => MappedPropertyFilter.GetColumnName(prop);

    /// <summary>
    /// Compiles a getter delegate: (object entity) => (object?)entity.Property
    /// </summary>
    private static Func<object, object?> CreateGetter(PropertyInfo prop)
    {
        ParameterExpression param = System.Linq.Expressions.Expression.Parameter(typeof(object), "entity");
        UnaryExpression cast = System.Linq.Expressions.Expression.Convert(param, prop.DeclaringType!);
        MemberExpression access = System.Linq.Expressions.Expression.Property(cast, prop);
        UnaryExpression box = System.Linq.Expressions.Expression.Convert(access, typeof(object));
        return System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(box, param).Compile();
    }

    /// <summary>
    /// Compiles a setter delegate: (object entity, object? value) => entity.Property = value
    /// </summary>
    private static Action<object, object?> CreateSetter(PropertyInfo prop)
    {
        ParameterExpression entityParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "entity");
        ParameterExpression valueParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "value");
        UnaryExpression cast = System.Linq.Expressions.Expression.Convert(entityParam, prop.DeclaringType!);
        UnaryExpression convertedValue = System.Linq.Expressions.Expression.Convert(valueParam, prop.PropertyType);
        BinaryExpression assign = System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Property(cast, prop), convertedValue);
        return System.Linq.Expressions.Expression.Lambda<Action<object, object?>>(assign, entityParam, valueParam).Compile();
    }
}