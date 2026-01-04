using System.Collections.Concurrent;
using System.Data;

namespace Jaunty.Internal.Parameters;

internal static class ParameterBinder
{
    public static void Bind(IDbCommand command, object parameters)
    {
        if (IsPositionalParameters(parameters))
            BindPositional(command, parameters);
        else
            BindNamed(command, parameters);
    }

    private static bool IsPositionalParameters(object parameters)
    {
        var type = parameters.GetType();

        if (type.IsArray)
            return true;

        // Primitive types and common value types are positional (single value)
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(Guid) ||
               type == typeof(byte[]);
    }

    private static void BindNamed(IDbCommand command, object parameters)
    {
        var meta = ParameterCache.Get(parameters.GetType());

        for (int i = 0; i < meta.Length; i++)
        {
            ParameterMetadata m = meta[i];
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = m.Name;
            p.Value = m.Getter(parameters) ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    private static void BindPositional(IDbCommand command, object parameters)
    {
        string[] paramNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        object?[] values = GetPositionalValues(parameters);

        int uniqueSqlParams = DeduplicateAndBind(command, paramNames, values);

        if (uniqueSqlParams != values.Length)
        {
            throw new ArgumentException(
                $"Parameter count mismatch: SQL contains {uniqueSqlParams} unique parameter(s), but {values.Length} value(s) provided.");
        }
    }

    private static int DeduplicateAndBind(IDbCommand command, string[] paramNames, object?[] values)
    {
        int uniqueSqlCount = 0;
        int valueIndex = 0;

        for (int i = 0; i < paramNames.Length; i++)
        {
            var name = paramNames[i];

            bool seen = false;
            for (int j = 0; j < i; j++)
            {
                if (string.Equals(paramNames[j], name, StringComparison.OrdinalIgnoreCase))
                {
                    seen = true;
                    break;
                }
            }

            if (seen)
                continue;

            if (valueIndex >= values.Length)
                return uniqueSqlCount + 1;

            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = values[valueIndex++] ?? DBNull.Value;
            command.Parameters.Add(p);

            uniqueSqlCount++;
        }

        return uniqueSqlCount;
    }

    private static object?[] GetPositionalValues(object parameters)
    {
        if (parameters is object?[] objArray)
            return objArray;

        if (parameters is Array array)
        {
            var result = new object?[array.Length];

            for (int i = 0; i < array.Length; i++)
                result[i] = array.GetValue(i);

            return result;
        }

        // Single value - avoid array allocation for common case
        return [parameters];
    }
}

internal static class SqlParameterParserCache
{
    private static readonly ConcurrentDictionary<string, string[]> Cache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql)
    {
        return Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s));
    }
}