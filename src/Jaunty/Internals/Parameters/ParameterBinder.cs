using System.Data;

namespace Jaunty.Internals.Parameters;

internal static class ParameterBinder
{
    internal static void Bind(IDbCommand command, object parameters)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var meta = ParameterCache.Get(parameters.GetType());

        // Build lookup from property names
        var propertyLookup = new Dictionary<string, ParameterMetadata>(meta.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < meta.Length; i++)
        {
            propertyLookup[meta[i].Name] = meta[i];
        }

        // Dedupe SQL params and validate all exist
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            if (!propertyLookup.TryGetValue(sqlName, out var m))
                throw new ArgumentException(
                    $"No property found matching SQL parameter '@{sqlName}'.");

            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = sqlName;
            p.Value = m.Getter(parameters) ?? DBNull.Value;
            command.Parameters.Add(p);
        }

        // Check for unused properties
        if (bound.Count != meta.Length)
        {
            var unused = new List<string>();
            for (int i = 0; i < meta.Length; i++)
            {
                if (!bound.Contains(meta[i].Name))
                    unused.Add(meta[i].Name);
            }
            throw new ArgumentException(
                $"Unused parameter properties: {string.Join(", ", unused)}. SQL contains no matching parameters.");
        }
    }
}
