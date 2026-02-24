using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Read;
using Jaunty.StoredProcedure;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a stored procedure with output parameters and returns the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// After execution, output parameter values can be retrieved using <c>parameters.Get&lt;T&gt;("parameterName")</c>.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>A list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a stored procedure using strict mapping mode. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// <strong>Output Parameters:</strong> After execution, retrieve output parameter values using:
    /// <code>
    /// var outputValue = parameters.Get&lt;int&gt;("TotalCount");
    /// </code>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Execute stored procedure with output parameters
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("TotalCount", DbType.Int32);
    /// 
    /// var products = connection.ExecuteStoredProcedure&lt;Product&gt;("GetProductsByCategoryWithTotal", parameters);
    /// 
    /// // Get output parameter value
    /// int totalCount = parameters.Get&lt;int&gt;("TotalCount");
    /// Console.WriteLine($"Total products: {totalCount}");
    /// 
    /// foreach (var product in products)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    /// <seealso cref="ExecuteStoredProcedureAsync{T}(IDbConnection, string, SpParameters, CommandOptions{T}, CancellationToken)"/>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParameters(connection, procedureName, parameters, spOptions, (reader, _) =>
        {
            var results = new List<T>();
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            while (reader.Read())
            {
                results.Add(map(reader));
            }
            return results;
        });
    }

    /// <summary>
    /// Executes a stored procedure with output parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// <para>
    /// Output parameter values can be retrieved after execution using <c>parameters.Get&lt;T&gt;("parameterName")</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure with output parameters and get first result
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("HasMore", DbType.Boolean);
    /// 
    /// var product = connection.ExecuteStoredProcedureFirst&lt;Product&gt;("GetFirstProductByCategory", parameters);
    /// 
    /// // Get output parameter value
    /// bool hasMore = parameters.Get&lt;bool&gt;("HasMore");
    /// Console.WriteLine($"First product: {product.Name}, Has more: {hasMore}");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParameters(connection, procedureName, parameters, spOptions, (reader, _) =>
        {
            if (!reader.Read())
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        });
    }

    /// <summary>
    /// Executes a stored procedure with output parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>
    /// The first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// <para>
    /// Output parameter values can be retrieved after execution using <c>parameters.Get&lt;T&gt;("parameterName")</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure with output parameters and get first or null
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 999)
    ///     .AddOutput("TotalCount", DbType.Int32);
    /// 
    /// var product = connection.ExecuteStoredProcedureFirstOrDefault&lt;Product&gt;("GetFirstProductByCategory", parameters);
    /// 
    /// if (product != null)
    /// {
    ///     int totalCount = parameters.Get&lt;int&gt;("TotalCount");
    ///     Console.WriteLine($"Found: {product.Name}, Total: {totalCount}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParameters<T?>(connection, procedureName, parameters, spOptions, (reader, _) =>
        {
            if (!reader.Read())
                return default;
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        });
    }

    /// <summary>
    /// Executes a stored procedure with output parameters and returns a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that return a single value along with output parameters.
    /// </para>
    /// <para>
    /// Output parameter values can be retrieved after execution using <c>parameters.Get&lt;T&gt;("parameterName")</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure that returns scalar with output parameters
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("AveragePrice", DbType.Decimal);
    /// 
    /// var count = connection.ExecuteStoredProcedureScalar&lt;long&gt;("GetProductCountWithAverage", parameters);
    /// 
    /// // Get output parameter value
    /// decimal avgPrice = parameters.Get&lt;decimal&gt;("AveragePrice");
    /// Console.WriteLine($"Count: {count}, Average Price: ${avgPrice}");
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string, object?, CommandOptions)"/>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteScalarWithOutputParameters<T>(connection, procedureName, parameters, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure with output parameters that does not return results (INSERT, UPDATE, DELETE) 
    /// and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that perform data modification operations with output parameters.
    /// </para>
    /// <para>
    /// Output parameter values can be retrieved after execution using <c>parameters.Get&lt;T&gt;("parameterName")</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure that updates prices with output parameter
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddInput("PriceIncreasePercent", 10)
    ///     .AddOutput("RowsUpdated", DbType.Int32);
    /// 
    /// int affected = connection.ExecuteStoredProcedureNonQuery("UpdatePricesByCategory", parameters);
    /// 
    /// // Get output parameter value (may differ from return value for complex procedures)
    /// int rowsUpdated = parameters.Get&lt;int&gt;("RowsUpdated");
    /// Console.WriteLine($"Rows updated: {rowsUpdated}");
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string, object?, CommandOptions)"/>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteNonQueryWithOutputParameters(connection, procedureName, parameters, spOptions);
    }

    #region Core execution with output parameters

    private static TResult ExecuteWithOutputParameters<TResult>(IDbConnection connection, string procedureName, SpParameters parameters,
        CommandOptions options, Func<IDataReader, SpParameters, TResult> handler)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException(nameof(procedureName));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindSpParameters(command, parameters);

            using IDataReader reader = command.ExecuteReader();
            TResult result = handler(reader, parameters);

            // Close reader before reading output parameters
            reader.Close();

            // Read output parameter values back into SpParameters
            ReadOutputParameters(parameters);

            return result;
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static T ExecuteScalarWithOutputParameters<T>(IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException(nameof(procedureName));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindSpParameters(command, parameters);

            object? result = command.ExecuteScalar();

            // Read output parameter values
            ReadOutputParameters(parameters);

            if (result is null || result == DBNull.Value)
            {
                if (default(T) is null)
                    return default!;
                throw new InvalidOperationException("Scalar result is null but expected a non-nullable value.");
            }

            return (T)Convert.ChangeType(result, typeof(T));
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static int ExecuteNonQueryWithOutputParameters(IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException(nameof(procedureName));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindSpParameters(command, parameters);

            int rowsAffected = command.ExecuteNonQuery();

            // Read output parameter values
            ReadOutputParameters(parameters);

            return rowsAffected;
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static void BindSpParameters(IDbCommand command, SpParameters parameters)
    {
        IReadOnlyList<SpParameter> paramList = parameters.Parameters;

        for (int i = 0; i < paramList.Count; i++)
        {
            SpParameter sp = paramList[i];
            IDbDataParameter dbParam = command.CreateParameter();

            // Ensure parameter name starts with @
            dbParam.ParameterName = sp.Name.StartsWith("@") ? sp.Name : "@" + sp.Name;
            dbParam.Direction = sp.Direction;

            if (sp.DbType.HasValue)
                dbParam.DbType = sp.DbType.Value;

            if (sp.Size.HasValue)
                dbParam.Size = sp.Size.Value;

            // Set value for Input and InputOutput parameters
            if (sp.Direction == ParameterDirection.Input || sp.Direction == ParameterDirection.InputOutput)
                dbParam.Value = sp.Value ?? DBNull.Value;

            command.Parameters.Add(dbParam);

            // Store reference to the db parameter so we can read output values later
            sp.DbParameter = dbParam;
        }
    }

    private static void ReadOutputParameters(SpParameters parameters)
    {
        IReadOnlyList<SpParameter> paramList = parameters.Parameters;

        for (int i = 0; i < paramList.Count; i++)
        {
            SpParameter sp = paramList[i];

            if (sp.Direction == ParameterDirection.Output ||
                sp.Direction == ParameterDirection.InputOutput ||
                sp.Direction == ParameterDirection.ReturnValue)
            {
                // The DbParameter already has the value set by the database
                // SpParameters.Get<T>() will read from sp.DbParameter.Value
                if (sp.DbParameter is not null)
                {
                    sp.Value = sp.DbParameter.Value;
                }
            }
        }
    }

    #endregion
}
