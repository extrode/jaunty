using System.Data;
using System.Data.Common;

using Jaunty.Interfaces;
using Jaunty.Internal.Execution;
using Jaunty.Internal.Mapping;
using Jaunty.Readers;

namespace Jaunty;

public static partial class Jaunty
{
    #region QueryScalar

    public static T QueryScalar<T>(this IDbConnection connection, string sql)
    {
        return QueryScalarCore<T>(connection, sql, null, default);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters)
    {
        return QueryScalarCore<T>(connection, sql, parameters, default);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, CommandOptions options)
    {
        return QueryScalarCore<T>(connection, sql, null, options);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options)
    {
        return QueryScalarCore<T>(connection, sql, parameters, options);
    }

    public static T QueryScalar<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest)
    {
        return QueryScalarCore<T>(connection, sql, CombineParams(param1, param2, rest), default);
    }

    internal static T QueryScalarCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        return CommandExecutor.ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader =>
        {
            if (reader is DbDataReader dbReader)
                return !dbReader.Read() || dbReader.IsDBNull(0) ? default! : dbReader.GetFieldValue<T>(0);

            if (!reader.Read() || reader.IsDBNull(0)) return default!;

            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        });
    }

    #endregion

    #region Query (Strict Mode)

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql) where T : IMapped<T>, new()
    {
        return QueryMapped<T>(connection, sql, null, default, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object parameters) where T : IMapped<T>, new()
    {
        return QueryMapped<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, CommandOptions options) where T : IMapped<T>, new()
    {
        return QueryMapped<T>(connection, sql, null, options, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options) where T : IMapped<T>, new()
    {
        return QueryMapped<T>(connection, sql, parameters, options, MappingMode.Strict);
    }

    public static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest) where T : IMapped<T>, new()
    {
        return QueryMapped<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Strict);
    }

    #endregion

    #region QueryPartial (Partial/Projection Mode)

    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, CommandOptions options) where T : new()
    {
        return QueryCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }

    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return QueryCore<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Projection);
    }

    #endregion

    #region Core Implementation

    internal static List<T> QueryCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T : new()
    {
        return CommandExecutor.ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader =>
        {
            var results = new List<T>();
            var setters = MetadataCache<T>.GetSetters(reader, mode);

            while (reader.Read())
            {
                var entity = new T();

                for (int i = 0; i < setters.Length; i++)
                    setters[i].Set(entity, reader);

                results.Add(entity);
            }

            return results;
        });
    }

    //internal static IEnumerable<T> Query<T>(this IDbConnection connection, string sql, object? parameters = null) where T : IMapped<T>, new()
    //{
    //    return QueryCoreMapped<T>(connection, sql, parameters);
    //}

    internal static IEnumerable<T> QueryMapped<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T : IMapped<T>, new()
    {
        return CommandExecutor.ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader => EntityReader.ReadEntities<T>(reader));
    }

    internal static object[] CombineParams(object param1, object param2, object[] rest)
    {
        var result = new object[2 + rest.Length];
        result[0] = param1;
        result[1] = param2;

        for (int i = 0; i < rest.Length; i++)
            result[i + 2] = rest[i];

        return result;
    }

    #endregion
}
