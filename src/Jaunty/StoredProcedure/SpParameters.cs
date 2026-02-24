using System.Data;

namespace Jaunty.StoredProcedure;

/// <summary>
/// Represents a stored procedure parameter with direction support.
/// </summary>
/// <remarks>
/// <para>
/// This class represents a single parameter for a stored procedure call. It supports input, output, 
/// input/output, and return value parameter directions.
/// </para>
/// <para>
/// Use <see cref="SpParameters"/> to create and manage collections of stored procedure parameters.
/// </para>
/// </remarks>
/// <seealso cref="SpParameters"/>
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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(name);
#else
        if (name is null) throw new ArgumentNullException(nameof(name));
#endif
        Name = name;
        Value = value;
        Direction = direction;
        DbType = dbType;
        Size = size;
    }
}

/// <summary>
/// A collection of stored procedure parameters with support for input, output, and input/output parameters.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a fluent API for building stored procedure parameters. It supports:
/// </para>
/// <list type="bullet">
/// <item><description><strong>Input parameters:</strong> Values passed to the stored procedure</description></item>
/// <item><description><strong>Output parameters:</strong> Values returned from the stored procedure</description></item>
/// <item><description><strong>Input/Output parameters:</strong> Values passed in and potentially modified by the stored procedure</description></item>
/// <item><description><strong>Return value parameters:</strong> The return value of the stored procedure</description></item>
/// </list>
/// <para>
/// After executing a stored procedure with output parameters, use <see cref="Get{T}(string)"/> 
/// to retrieve the output values.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create parameters for a stored procedure
/// var parameters = new SpParameters()
///     .AddInput("CategoryId", 5)
///     .AddInput("MinPrice", 10.00m)
///     .AddOutput("TotalCount", DbType.Int32)
///     .AddOutput("AveragePrice", DbType.Decimal)
///     .AddReturnValue();
/// 
/// // Execute stored procedure
/// var products = connection.ExecuteStoredProcedure&lt;Product&gt;("GetProducts", parameters);
/// 
/// // Get output parameter values
/// int totalCount = parameters.Get&lt;int&gt;("TotalCount");
/// decimal avgPrice = parameters.Get&lt;decimal&gt;("AveragePrice");
/// int returnValue = parameters.GetReturnValue();
/// </code>
/// </example>
/// <seealso cref="SpParameter"/>
/// <seealso cref="Jaunty.ExecuteStoredProcedure{T}(IDbConnection, string, SpParameters, CommandOptions{T})"/>
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
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddInput("MinPrice", 10.00m);
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInput("ProductName", "Widget", DbType.String, size: 100)
    ///     .AddInput("CreatedDate", DateTime.Now, DbType.DateTime);
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("TotalCount", DbType.Int32)
    ///     .AddOutput("StatusMessage", DbType.String, size: 256);
    /// 
    /// connection.ExecuteStoredProcedure&lt;Product&gt;("GetProducts", parameters);
    /// 
    /// int totalCount = parameters.Get&lt;int&gt;("TotalCount");
    /// string status = parameters.Get&lt;string&gt;("StatusMessage");
    /// </code>
    /// </example>
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
    /// <remarks>
    /// Input/output parameters are passed to the stored procedure and may be modified by it.
    /// After execution, use <see cref="Get{T}(string)"/> to retrieve the modified value.
    /// </remarks>
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInputOutput("Counter", 0, DbType.Int32);
    /// 
    /// connection.ExecuteStoredProcedureNonQuery("IncrementCounter", parameters);
    /// 
    /// int newCounter = parameters.Get&lt;int&gt;("Counter");
    /// </code>
    /// </example>
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
    /// <remarks>
    /// The return value parameter captures the integer return value of the stored procedure.
    /// After execution, use <see cref="GetReturnValue()"/> to retrieve the return value.
    /// </remarks>
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddReturnValue();
    /// 
    /// connection.ExecuteStoredProcedureNonQuery("DeleteCategory", parameters);
    /// 
    /// int returnValue = parameters.GetReturnValue();
    /// // Typically: 0 = success, non-zero = error or rows affected
    /// </code>
    /// </example>
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
    /// <returns>The parameter value converted to the specified type, or default if null.</returns>
    /// <remarks>
    /// <para>
    /// This method retrieves the value of an output parameter after the stored procedure has executed.
    /// The value is automatically converted to the specified type <typeparamref name="T"/>.
    /// </para>
    /// <para>
    /// If the parameter value is NULL or DBNull, this method returns <see langword="default"/> for the type.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("TotalCount", DbType.Int32)
    ///     .AddOutput("AveragePrice", DbType.Decimal);
    /// 
    /// connection.ExecuteStoredProcedure&lt;Product&gt;("GetProductStats", parameters);
    /// 
    /// int totalCount = parameters.Get&lt;int&gt;("TotalCount");
    /// decimal avgPrice = parameters.Get&lt;decimal&gt;("AveragePrice");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when a parameter with the specified name is not found.
    /// </exception>
    public T? Get<T>(string name)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(name);
#else
        if (name is null) throw new ArgumentNullException(nameof(name));
#endif
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
    /// <returns>The return value as an integer, or 0 if no return value was defined.</returns>
    /// <remarks>
    /// <para>
    /// This method retrieves the integer return value of a stored procedure.
    /// You must call <see cref="AddReturnValue"/> before executing the stored procedure 
    /// to capture the return value.
    /// </para>
    /// <para>
    /// If no return value parameter was defined or the value is null, this method returns 0.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddReturnValue();
    /// 
    /// connection.ExecuteStoredProcedureNonQuery("DeleteCategory", parameters);
    /// 
    /// int returnValue = parameters.GetReturnValue();
    /// if (returnValue == 0)
    /// {
    ///     Console.WriteLine("Category deleted successfully");
    /// }
    /// else
    /// {
    ///     Console.WriteLine($"Error or rows affected: {returnValue}");
    /// }
    /// </code>
    /// </example>
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
    /// <remarks>
    /// This method is useful for checking if an output parameter has a value before retrieving it.
    /// </remarks>
    /// <example>
    /// <code>
    /// var parameters = new SpParameters()
    ///     .AddOutput("OptionalResult", DbType.String);
    /// 
    /// connection.ExecuteStoredProcedureNonQuery("GetOptionalData", parameters);
    /// 
    /// if (parameters.HasValue("OptionalResult"))
    /// {
    ///     string result = parameters.Get&lt;string&gt;("OptionalResult");
    ///     Console.WriteLine($"Result: {result}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No result returned");
    /// }
    /// </code>
    /// </example>
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
