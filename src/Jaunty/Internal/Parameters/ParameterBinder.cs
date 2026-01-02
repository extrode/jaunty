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

        // Arrays and collections of primitives are positional
        if (type.IsArray)
            return true;

        // Primitive types are positional (single value)
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(Guid) || type == typeof(byte[]);
    }

    private static void BindNamed(IDbCommand command, object parameters)
    {
        var meta = ParameterCache.Get(parameters.GetType());

        for (int i = 0; i < meta.Length; i++)
        {
            var p = command.CreateParameter();
            p.ParameterName = meta[i].Name;
            p.Value = meta[i].Getter(parameters) ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    private static void BindPositional(IDbCommand command, object parameters)
    {
        var sql = command.CommandText;
        var paramNames = SqlParameterParser.ExtractParameterNames(sql);
        var values = GetPositionalValues(parameters);

        // Deduplicate parameter names while preserving order
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueNames = new List<string>(paramNames.Length);

        for (int i = 0; i < paramNames.Length; i++)
        {
            if (seen.Add(paramNames[i]))
                uniqueNames.Add(paramNames[i]);
        }

        if (uniqueNames.Count != values.Length)
        {
            throw new ArgumentException(
                $"Parameter count mismatch: SQL contains {uniqueNames.Count} unique parameter(s) [{string.Join(", ", uniqueNames)}], but {values.Length} value(s) provided.");
        }

        for (int i = 0; i < uniqueNames.Count; i++)
        {
            var p = command.CreateParameter();
            p.ParameterName = uniqueNames[i];
            p.Value = values[i] ?? DBNull.Value;
            command.Parameters.Add(p);
        }
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

        // Single value
        return [parameters];
    }
}
