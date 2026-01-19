using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a stored procedure with output parameters and returns the results as a list.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <returns>A list of mapped entities.</returns>
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
    /// Executes a stored procedure with output parameters and returns the first result.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <returns>The first mapped entity.</returns>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParameters(connection, procedureName, parameters, spOptions, (reader, _) =>
        {
            if (!reader.Read())
                throw new InvalidOperationException("Sequence contains no elements.");
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        });
    }

    /// <summary>
    /// Executes a stored procedure with output parameters and returns the first result or default.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <returns>The first mapped entity or default.</returns>
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
    /// Executes a stored procedure with output parameters and returns a scalar value.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The scalar value.</returns>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteScalarWithOutputParameters<T>(connection, procedureName, parameters, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure with output parameters that does not return results (INSERT, UPDATE, DELETE).
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows affected.</returns>
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
