using System.Data;

namespace Jaunty.Core;

/// <summary>
/// Options for command execution with custom mapper support.
/// </summary>
/// <typeparam name="T">The entity type for the command result.</typeparam>
/// <remarks>
/// <para>
/// This struct provides configuration options for database command execution in Jaunty.
/// It supports custom mapping, transactions, command timeouts, and command type specification.
/// </para>
/// <para>
/// Use the static factory methods for fluent configuration:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="WithMapper(Func{IDataReader, T})"/> - Specify a custom mapper function</description></item>
/// <item><description><see cref="WithTransaction(IDbTransaction)"/> - Execute within a transaction</description></item>
/// <item><description><see cref="WithTimeout(int)"/> - Set a command timeout in seconds</description></item>
/// <item><description><see cref="AsStoredProcedure()"/> - Execute as a stored procedure</description></item>
/// </list>
/// <para>
/// For commands that don't require a custom mapper, use the non-generic <see cref="CommandOptions"/> struct.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Use with transaction
/// using var tx = connection.BeginTransaction();
/// var options = CommandOptions&lt;Product&gt;.WithTransaction(tx);
/// var products = connection.Query("SELECT * FROM Products", options: options);
/// 
/// // Use with timeout
/// var options = CommandOptions&lt;Product&gt;.WithTimeout(60);
/// var products = connection.Query("SELECT * FROM Products", options: options);
/// 
/// // Use as stored procedure
/// var options = CommandOptions&lt;Product&gt;.AsStoredProcedure();
/// var products = connection.Query("GetAllProducts", options: options);
/// 
/// // Combine multiple options
/// var options = CommandOptions&lt;Product&gt;.WithTransaction(tx).WithTimeout(60);
/// </code>
/// </example>
/// <seealso cref="CommandOptions"/>
public readonly struct CommandOptions<T>(Func<IDataReader, T>? mapper = null, IDbTransaction? transaction = null,
    int? commandTimeout = null, CommandType commandType = CommandType.Text)
{
    /// <summary>
    /// Gets the custom mapper function, if specified.
    /// </summary>
    /// <remarks>
    /// The mapper function receives an <see cref="IDataReader"/> positioned on the current row
    /// and returns a mapped entity of type <typeparamref name="T"/>.
    /// </remarks>
    public readonly Func<IDataReader, T>? Mapper = mapper;

    /// <summary>
    /// Gets the transaction to use for command execution, if specified.
    /// </summary>
    public readonly IDbTransaction? Transaction = transaction;

    /// <summary>
    /// Gets the command timeout in seconds, if specified.
    /// </summary>
    /// <remarks>
    /// If null, the connection's default command timeout is used.
    /// </remarks>
    public readonly int? CommandTimeout = commandTimeout;

    /// <summary>
    /// Gets the type of command to execute.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="CommandType.Text"/> for SQL queries.
    /// Use <see cref="CommandType.StoredProcedure"/> for stored procedures.
    /// </remarks>
    public readonly CommandType CommandType = commandType;

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> with the specified mapper function.
    /// </summary>
    /// <param name="mapper">The custom mapper function to use.</param>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with the specified mapper.</returns>
    /// <example>
    /// <code>
    /// var options = CommandOptions&lt;Product&gt;.WithMapper(reader => new Product
    /// {
    ///     Id = reader.GetInt32(0),
    ///     Name = reader.GetString(1),
    ///     Price = reader.GetDecimal(2)
    /// });
    /// </code>
    /// </example>
    public static CommandOptions<T> WithMapper(Func<IDataReader, T> mapper) => new(mapper: mapper);

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> with the specified transaction.
    /// </summary>
    /// <param name="transaction">The transaction to use for command execution.</param>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with the specified transaction.</returns>
    public static CommandOptions<T> WithTransaction(IDbTransaction transaction) => new(transaction: transaction);

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> with the specified timeout.
    /// </summary>
    /// <param name="seconds">The command timeout in seconds.</param>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with the specified timeout.</returns>
    public static CommandOptions<T> WithTimeout(int seconds) => new(commandTimeout: seconds);

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> configured for stored procedure execution.
    /// </summary>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with CommandType set to StoredProcedure.</returns>
    public static CommandOptions<T> AsStoredProcedure() => new(commandType: CommandType.StoredProcedure);

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> with all options specified.
    /// </summary>
    /// <param name="mapper">The custom mapper function.</param>
    /// <param name="transaction">The transaction to use.</param>
    /// <param name="timeoutSeconds">The command timeout in seconds.</param>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with all options set.</returns>
    public static CommandOptions<T> With(Func<IDataReader, T> mapper, IDbTransaction transaction, int timeoutSeconds) => new(mapper, transaction, timeoutSeconds);

    /// <summary>
    /// Implicitly converts a generic <see cref="CommandOptions{T}"/> to a non-generic <see cref="CommandOptions"/>.
    /// </summary>
    /// <param name="options">The generic command options to convert.</param>
    /// <returns>A non-generic <see cref="CommandOptions"/> with the same transaction, timeout, and command type.</returns>
    public static implicit operator CommandOptions(CommandOptions<T> options) => new(options.Transaction, options.CommandTimeout, options.CommandType);
}

/// <summary>
/// Options for command execution without custom mapper support.
/// </summary>
/// <remarks>
/// <para>
/// This struct provides configuration options for database command execution in Jaunty.
/// It supports transactions, command timeouts, and command type specification.
/// </para>
/// <para>
/// Use the static factory methods for fluent configuration:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="WithTransaction(IDbTransaction)"/> - Execute within a transaction</description></item>
/// <item><description><see cref="WithTimeout(int)"/> - Set a command timeout in seconds</description></item>
/// <item><description><see cref="AsStoredProcedure()"/> - Execute as a stored procedure</description></item>
/// </list>
/// <para>
/// For commands that require a custom mapper, use the generic <see cref="CommandOptions{T}"/> struct.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Use with transaction for non-query operations
/// using var tx = connection.BeginTransaction();
/// var options = CommandOptions.WithTransaction(tx);
/// int affected = connection.ExecuteNonQuery("UPDATE Products SET Price = Price * 1.1", options: options);
/// 
/// // Use with stored procedure
/// var options = CommandOptions.AsStoredProcedure();
/// var result = connection.ExecuteStoredProcedureScalar&lt;int&gt;("GetTotalCount", options: options);
/// </code>
/// </example>
/// <seealso cref="CommandOptions{T}"/>
public readonly struct CommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
{
    /// <summary>
    /// Gets the transaction to use for command execution, if specified.
    /// </summary>
    public readonly IDbTransaction? Transaction = transaction;

    /// <summary>
    /// Gets the command timeout in seconds, if specified.
    /// </summary>
    public readonly int? CommandTimeout = commandTimeout;

    /// <summary>
    /// Gets the type of command to execute.
    /// </summary>
    public readonly CommandType CommandType = commandType;

    /// <summary>
    /// Creates a new <see cref="CommandOptions"/> with the specified transaction.
    /// </summary>
    /// <param name="transaction">The transaction to use for command execution.</param>
    /// <returns>A new <see cref="CommandOptions"/> instance with the specified transaction.</returns>
    public static CommandOptions WithTransaction(IDbTransaction transaction) => new(transaction: transaction);

    /// <summary>
    /// Creates a new <see cref="CommandOptions"/> with the specified timeout.
    /// </summary>
    /// <param name="seconds">The command timeout in seconds.</param>
    /// <returns>A new <see cref="CommandOptions"/> instance with the specified timeout.</returns>
    public static CommandOptions WithTimeout(int seconds) => new(commandTimeout: seconds);

    /// <summary>
    /// Creates a new <see cref="CommandOptions"/> configured for stored procedure execution.
    /// </summary>
    /// <returns>A new <see cref="CommandOptions"/> instance with CommandType set to StoredProcedure.</returns>
    public static CommandOptions AsStoredProcedure() => new(commandType: CommandType.StoredProcedure);

    /// <summary>
    /// Creates a new <see cref="CommandOptions"/> with transaction and timeout specified.
    /// </summary>
    /// <param name="transaction">The transaction to use.</param>
    /// <param name="timeoutSeconds">The command timeout in seconds.</param>
    /// <returns>A new <see cref="CommandOptions"/> instance with transaction and timeout set.</returns>
    public static CommandOptions With(IDbTransaction transaction, int timeoutSeconds) => new(transaction, timeoutSeconds);
}
