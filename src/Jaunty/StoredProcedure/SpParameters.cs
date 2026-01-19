using System.Data;

namespace Jaunty;

/// <summary>
/// Represents a stored procedure parameter with direction support.
/// </summary>
public sealed class SpParameter
{
    /// <summary>
    /// The parameter name (without @ prefix).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The parameter value.
    /// </summary>
    public object? Value { get; internal set; }

    /// <summary>
    /// The parameter direction.
    /// </summary>
    public ParameterDirection Direction { get; }

    /// <summary>
    /// The parameter database type (optional).
    /// </summary>
    public DbType? DbType { get; }

    /// <summary>
    /// The parameter size (optional, useful for strings).
    /// </summary>
    public int? Size { get; }

    /// <summary>
    /// The underlying IDbDataParameter after execution.
    /// </summary>
    internal IDbDataParameter? DbParameter { get; set; }

    internal SpParameter(string name, object? value, ParameterDirection direction, DbType? dbType, int? size)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        Direction = direction;
        DbType = dbType;
        Size = size;
    }
}

/// <summary>
/// A collection of stored procedure parameters with support for input, output, and input/output parameters.
/// </summary>
public sealed class SpParameters
{
    private readonly List<SpParameter> _parameters = new();

    /// <summary>
    /// Gets all parameters in this collection.
    /// </summary>
    public IReadOnlyList<SpParameter> Parameters => _parameters;

    /// <summary>
    /// Adds an input parameter.
    /// </summary>
    /// <param name="name">The parameter name (without @ prefix).</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SpParameters AddInput(string name, object? value)
    {
        _parameters.Add(new SpParameter(name, value, ParameterDirection.Input, null, null));
        return this;
    }

    /// <summary>
    /// Adds an input parameter with explicit database type.
    /// </summary>
    /// <param name="name">The parameter name (without @ prefix).</param>
    /// <param name="value">The parameter value.</param>
    /// <param name="dbType">The database type.</param>
    /// <param name="size">Optional size for string parameters.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SpParameters AddInput(string name, object? value, DbType dbType, int? size = null)
    {
        _parameters.Add(new SpParameter(name, value, ParameterDirection.Input, dbType, size));
        return this;
    }

    /// <summary>
    /// Adds an output parameter.
    /// </summary>
    /// <param name="name">The parameter name (without @ prefix).</param>
    /// <param name="dbType">The database type.</param>
    /// <param name="size">Optional size for string parameters.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SpParameters AddOutput(string name, DbType dbType, int? size = null)
    {
        _parameters.Add(new SpParameter(name, null, ParameterDirection.Output, dbType, size));
        return this;
    }

    /// <summary>
    /// Adds an input/output parameter.
    /// </summary>
    /// <param name="name">The parameter name (without @ prefix).</param>
    /// <param name="value">The input value.</param>
    /// <param name="dbType">The database type.</param>
    /// <param name="size">Optional size for string parameters.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SpParameters AddInputOutput(string name, object? value, DbType dbType, int? size = null)
    {
        _parameters.Add(new SpParameter(name, value, ParameterDirection.InputOutput, dbType, size));
        return this;
    }

    /// <summary>
    /// Adds a return value parameter.
    /// </summary>
    /// <param name="name">The parameter name (typically "RETURN_VALUE" or similar).</param>
    /// <param name="dbType">The database type (typically DbType.Int32).</param>
    /// <returns>This instance for fluent chaining.</returns>
    public SpParameters AddReturnValue(string name = "RETURN_VALUE", DbType dbType = DbType.Int32)
    {
        _parameters.Add(new SpParameter(name, null, ParameterDirection.ReturnValue, dbType, null));
        return this;
    }

    /// <summary>
    /// Gets the value of an output or input/output parameter after execution.
    /// </summary>
    /// <typeparam name="T">The expected type of the parameter value.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value converted to the specified type.</returns>
    public T? Get<T>(string name)
    {
        SpParameter? param = null;
        for (int i = 0; i < _parameters.Count; i++)
        {
            if (string.Equals(_parameters[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                param = _parameters[i];
                break;
            }
        }

        if (param is null)
            throw new ArgumentException($"Parameter '{name}' not found.", nameof(name));

        // After execution, the DbParameter will have the output value
        object? value = param.DbParameter?.Value ?? param.Value;

        if (value is null || value == DBNull.Value)
            return default;

        return (T)Convert.ChangeType(value, typeof(T));
    }

    /// <summary>
    /// Gets the return value after execution.
    /// </summary>
    /// <returns>The return value as an integer.</returns>
    public int GetReturnValue()
    {
        SpParameter? param = null;
        for (int i = 0; i < _parameters.Count; i++)
        {
            if (_parameters[i].Direction == ParameterDirection.ReturnValue)
            {
                param = _parameters[i];
                break;
            }
        }

        if (param is null)
            throw new InvalidOperationException("No return value parameter was defined. Use AddReturnValue() to add one.");

        object? value = param.DbParameter?.Value ?? param.Value;

        if (value is null || value == DBNull.Value)
            return 0;

        return Convert.ToInt32(value);
    }

    /// <summary>
    /// Checks if a parameter exists and has a non-null value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>True if the parameter exists and has a non-null, non-DBNull value.</returns>
    public bool HasValue(string name)
    {
        for (int i = 0; i < _parameters.Count; i++)
        {
            if (string.Equals(_parameters[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                var param = _parameters[i];
                object? value = param.DbParameter?.Value ?? param.Value;
                return value is not null && value != DBNull.Value;
            }
        }
        return false;
    }
}
