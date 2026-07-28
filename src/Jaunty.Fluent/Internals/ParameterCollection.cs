using System.Data;
using System.Dynamic;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Efficiently collects parameters for SQL queries without LINQ overhead.
/// </summary>
internal sealed class ParameterCollection
{
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly HashSet<string> _names = new();

    public int Count => _parameters.Count;

    /// <summary>
    /// Adds a parameter. Throws if <paramref name="name"/> was already added, so a bug that
    /// produces a duplicate parameter name fails consistently and immediately - previously
    /// <see cref="BindTo"/> would add both duplicates to the command (the ADO.NET provider then
    /// throws), while <see cref="ToParameterObject"/> silently let the last value win via
    /// dictionary overwrite, so the failure mode depended on which output path the caller used.
    /// </summary>
    public void Add(string name, object? value)
    {
        if (!_names.Add(name))
            throw new ArgumentException($"A parameter named '{name}' has already been added.", nameof(name));

        _parameters.Add((name, value));
    }

    /// <summary>
    /// Whether a parameter with this name has already been added. Lets callers raise a more
    /// specific error before <see cref="Add"/>'s generic duplicate check fires.
    /// </summary>
    public bool Contains(string name) => _names.Contains(name);

    public void AddRange(List<(string Name, object? Value)> parameters)
    {
        foreach ((string name, object? value) in parameters)
            Add(name, value);
    }

    /// <summary>
    /// Binds all parameters directly to the command (bypasses reflection-based ParameterBinder).
    /// </summary>
    public void BindTo(IDbCommand command)
    {
        for (int i = 0; i < _parameters.Count; i++)
        {
            (string? name, object? value) = _parameters[i];
            IDbDataParameter param = command.CreateParameter();
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
            (string? name, object? value) = _parameters[i];
            // Strip parameter prefix (@, $, or :) if present
            var key = name.Length > 0 && name[0] is '@' or '$' or ':' ? name.Substring(1) : name;
            dict[key] = value;
        }

        return expando;
    }

    public void Clear()
    {
        _parameters.Clear();
        _names.Clear();
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
        clone._names.UnionWith(_names);
        return clone;
    }
}