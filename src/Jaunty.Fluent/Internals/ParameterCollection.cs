using System.Data;
using System.Dynamic;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Efficiently collects parameters for SQL queries without LINQ overhead.
/// </summary>
internal sealed class ParameterCollection
{
    private readonly List<(string Name, object? Value)> _parameters = new();

    public int Count => _parameters.Count;

    public void Add(string name, object? value)
    {
        _parameters.Add((name, value));
    }

    public void AddRange(List<(string Name, object? Value)> parameters)
    {
        _parameters.AddRange(parameters);
    }

    /// <summary>
    /// Binds all parameters directly to the command (bypasses reflection-based ParameterBinder).
    /// </summary>
    public void BindTo(IDbCommand command)
    {
        for (int i = 0; i < _parameters.Count; i++)
        {
            var (name, value) = _parameters[i];
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }

    /// <summary>
    /// Creates a parameter object suitable for Jaunty's core Query methods.
    /// Uses ExpandoObject which ParameterBinder can read via reflection.
    /// </summary>
    public object? ToParameterObject()
    {
        if (_parameters.Count == 0)
            return null;

        var expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;

        for (int i = 0; i < _parameters.Count; i++)
        {
            var (name, value) = _parameters[i];
            // Strip parameter prefix (@ or $) if present
            var key = name.Length > 0 && name[0] is '@' or '$' ? name.Substring(1) : name;
            dict[key] = value;
        }

        return expando;
    }

    public void Clear()
    {
        _parameters.Clear();
    }

    /// <summary>
    /// Gets all parameters as a list of tuples.
    /// </summary>
    public IReadOnlyList<(string Name, object? Value)> GetAll() => _parameters;

    /// <summary>
    /// Creates a copy of this parameter collection.
    /// </summary>
    public ParameterCollection Clone()
    {
        var clone = new ParameterCollection();
        clone._parameters.AddRange(_parameters);
        return clone;
    }
}
