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
            var m = meta[i];
            var p = command.CreateParameter();
            p.ParameterName = m.Name;
            p.Value = m.Getter(parameters) ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    private static void BindPositional(IDbCommand command, object parameters)
    {
        var paramNames = SqlParameterParser.ExtractParameterNames(command.CommandText);
        var values = GetPositionalValues(parameters);

        // Deduplicate parameter names while preserving order
        var uniqueCount = DeduplicateAndBind(command, paramNames, values);

        if (uniqueCount != values.Length)
        {
            throw new ArgumentException(
                $"Parameter count mismatch: SQL contains {uniqueCount} unique parameter(s), but {values.Length} value(s) provided.");
        }
    }

    private static int DeduplicateAndBind(IDbCommand command, string[] paramNames, object?[] values)
    {
        // For small parameter counts, linear search is faster than HashSet
        var uniqueCount = 0;

        for (int i = 0; i < paramNames.Length; i++)
        {
            var name = paramNames[i];

            // Check if we've seen this name before (case-insensitive)
            var isDuplicate = false;
            for (int j = 0; j < i; j++)
            {
                if (string.Equals(paramNames[j], name, StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                if (uniqueCount < values.Length)
                {
                    var p = command.CreateParameter();
                    p.ParameterName = name;
                    p.Value = values[uniqueCount] ?? DBNull.Value;
                    command.Parameters.Add(p);
                }
                uniqueCount++;
            }
        }

        return uniqueCount;
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
        return new object?[] { parameters };
    }
}
