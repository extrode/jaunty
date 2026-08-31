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
/// <item><description><see cref="WithExpectedRowCount(int)"/> - Hint for expected row count to optimize list allocation</description></item>
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
/// // Combine multiple options - use the constructor directly, or With(mapper, transaction, timeoutSeconds)
/// var options = new CommandOptions&lt;Product&gt;(transaction: tx, commandTimeout: 60);
/// 
/// // Pre-size list for large result sets
/// var options = CommandOptions&lt;Product&gt;.WithExpectedRowCount(10000);
/// </code>
/// </example>
/// <seealso cref="CommandOptions"/>
public readonly struct CommandOptions<T>(Func<IDataReader, T>? mapper = null, IDbTransaction? transaction = null,
    int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
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
    public readonly int? CommandTimeout = global::Jaunty.Internals.CommandTimeoutHint.Require(commandTimeout);

    /// <summary>
    /// Gets the type of command to execute.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="CommandType.Text"/> for SQL queries.
    /// Use <see cref="CommandType.StoredProcedure"/> for stored procedures.
    /// </remarks>
    public readonly CommandType CommandType = commandType;

    /// <summary>
    /// Gets the expected number of rows for the query result.
    /// </summary>
    /// <remarks>
    /// This is a hint used to pre-size the internal list for better performance
    /// when the approximate result size is known. If not specified, a default capacity is used.
    /// <para>
    /// AUD-R35-122: normalised on the way in, so a hint can never abort the query it was meant to
    /// speed up. A non-positive value reads as "no hint" and falls back to
    /// <see cref="Configuration.JauntyConfig.QueryResultCapacity"/> - matching that property's own
    /// setter, which clamps the same way - and anything above
    /// <c>CapacityHint.MaxExpectedRowCount</c> (1,048,576) is capped, so a mistyped value cannot
    /// allocate its way to <see cref="OutOfMemoryException"/> before the first row is read. The
    /// value read back here is therefore the normalised one, not the one passed in.
    /// </para>
    /// </remarks>
    public readonly int? ExpectedRowCount = global::Jaunty.Internals.CapacityHint.Normalize(expectedRowCount);

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
    /// <param name="seconds">The command timeout in seconds. Zero means no timeout.</param>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with the specified timeout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// AUD-R35-149. <paramref name="seconds"/> is negative. This used to be accepted here and to
    /// surface as a provider-specific exception from inside command execution, naming nothing the
    /// caller had written.
    /// </exception>
    public static CommandOptions<T> WithTimeout(int seconds) => new(commandTimeout: seconds);

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> configured for stored procedure execution.
    /// </summary>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with CommandType set to StoredProcedure.</returns>
    public static CommandOptions<T> AsStoredProcedure() => new(commandType: CommandType.StoredProcedure);

    /// <summary>
    /// Creates a new <see cref="CommandOptions{T}"/> with the expected row count hint.
    /// </summary>
    /// <param name="rowCount">The expected number of rows to allocate space for.</param>
    /// <returns>A new <see cref="CommandOptions{T}"/> instance with the expected row count set.</returns>
    /// <remarks>
    /// This hint helps optimize memory allocation by pre-sizing the internal list.
    /// </remarks>
    public static CommandOptions<T> WithExpectedRowCount(int rowCount) => new(expectedRowCount: rowCount);

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
    /// <remarks>
    /// <para>
    /// <b><see cref="Mapper"/> and <see cref="ExpectedRowCount"/> do not survive this conversion</b>,
    /// and because it is <em>implicit</em> there is no cast at the call site to say so: passing a
    /// <c>CommandOptions&lt;Product&gt;</c> to an API whose parameter is the non-generic
    /// <see cref="CommandOptions"/> compiles clean and silently drops both.
    /// </para>
    /// <para>
    /// AUD-R26-054 (batch 4, low/consistency). Neither field can be carried: <see cref="CommandOptions"/>
    /// has no member for either one, and <see cref="Mapper"/> is typed on <typeparamref name="T"/>,
    /// so a non-generic target could not hold it even if a field were added. The conversion is left
    /// implicit rather than made explicit because that would be a source-breaking change to a
    /// shipped public API.
    /// </para>
    /// <para>
    /// It is documented rather than fixed because as of this writing nothing can be lost that the
    /// destination could have used. Every public API taking a non-generic <see cref="CommandOptions"/>
    /// is a scalar read, a non-query, a dictionary projection, or a multi-entity overload that takes
    /// its <c>map</c> delegate as an explicit parameter - none consults a mapper, and none builds a
    /// list a row-count hint could pre-size. That is a property of the current API surface, not a
    /// guarantee: <b>an API that maps entities must take <see cref="CommandOptions{T}"/>, never the
    /// non-generic form</b>, or this conversion starts losing a mapper the caller supplied.
    /// </para>
    /// </remarks>
    public static implicit operator CommandOptions(CommandOptions<T> options) => new(options.Transaction, options.CommandTimeout, options.CommandType);

    /// <summary>
    /// Implicitly converts a non-generic <see cref="CommandOptions"/> to a
    /// <see cref="CommandOptions{T}"/>, carrying the transaction, timeout and command type.
    /// </summary>
    /// <param name="options">The non-generic command options to convert.</param>
    /// <returns>A <see cref="CommandOptions{T}"/> with no mapper and no row-count hint.</returns>
    /// <remarks>
    /// <para>
    /// AUD-R34-002 (high, design flaw in the overload set). Every entity-mapping API pairs an
    /// <c>(IDbConnection, string sql, object parameters)</c> overload with an
    /// <c>(IDbConnection, string sql, CommandOptions&lt;T&gt; options)</c> one. Without this
    /// conversion a non-generic <see cref="CommandOptions"/> - which is what
    /// <see cref="CommandOptions.WithTransaction"/> and <see cref="CommandOptions.WithTimeout"/>
    /// return - was not convertible to <c>CommandOptions&lt;T&gt;</c>, so the only applicable
    /// candidate was <c>object parameters</c>. The caller's options were bound as a parameters
    /// object, no properties were found on a struct exposing public fields, zero parameters bound,
    /// nothing was reported, and <b>the transaction or timeout was silently discarded</b>. The
    /// repo's own tests made that mistake in three places.
    /// </para>
    /// <para>
    /// The conversion is what fixes it, and it fixes it for every arity of every entity API at
    /// once: overload resolution prefers the conversion to <c>CommandOptions&lt;T&gt;</c> over the
    /// boxing conversion to <c>object</c>, because <c>CommandOptions&lt;T&gt;</c> converts to
    /// <c>object</c> and <c>object</c> does not convert back - the better-conversion-target rule.
    /// So the call that used to bind to <c>object parameters</c> now binds to the overload the
    /// caller meant, with no source change on their side.
    /// </para>
    /// <para>
    /// <see cref="Mapper"/> and <see cref="ExpectedRowCount"/> are null on the result, which loses
    /// nothing: the source type has no member for either. Round-tripping through the non-generic
    /// form still drops them - see the conversion above.
    /// </para>
    /// <para>
    /// <b>The two conversions are now mutually implicit, which constrains what can be added to this
    /// API.</b> Neither type is a better common type than the other, so an expression that must pick
    /// one is ambiguous: <c>cond ? CommandOptions.WithTimeout(1) : CommandOptions&lt;Row&gt;.WithMapper(m)</c>
    /// is CS0172 and <c>new[] { genericOptions, nonGenericOptions }</c> is CS0826, where before this
    /// operator both resolved to the non-generic form. Both are narrow source breaks with an obvious
    /// remedy (annotate the type). The forward-looking constraint matters more: <b>an overload group
    /// must not offer both <see cref="CommandOptions"/> and <see cref="CommandOptions{T}"/> at the
    /// same argument position</b>, because a <c>default</c> argument there would be CS0121 with no
    /// unique best target. No group in this library does.
    /// </para>
    /// <para>
    /// Binary compatibility is unaffected - adding a conversion operator is additive, and compiled
    /// consumers keep their existing bindings. On <i>recompile</i>, though, a call site that was
    /// silently dropping a transaction or timeout starts honouring it with no source diff, so this
    /// belongs in release notes as a behaviour change rather than a pure bug fix.
    /// </para>
    /// </remarks>
    public static implicit operator CommandOptions<T>(CommandOptions options) => new(null, options.Transaction, options.CommandTimeout, options.CommandType);
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
    public readonly int? CommandTimeout = global::Jaunty.Internals.CommandTimeoutHint.Require(commandTimeout);

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
    /// <param name="seconds">The command timeout in seconds. Zero means no timeout.</param>
    /// <returns>A new <see cref="CommandOptions"/> instance with the specified timeout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">AUD-R35-149. <paramref name="seconds"/> is negative.</exception>
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