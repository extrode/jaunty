using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Internals.Read;

namespace Jaunty.Core;

/// <summary>
/// Represents a reader for multiple result sets returned from a query.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="GridReader"/> is used to read multiple result sets from a single database query.
/// It is returned by <see cref="Jaunty.QueryMultiple(IDbConnection, string)"/> and 
/// <see cref="Jaunty.QueryMultipleAsync(IDbConnection, string, CancellationToken)"/>.
/// </para>
/// <para>
/// <strong>Important:</strong> The <see cref="GridReader"/> must be disposed after use to properly 
/// release database resources. Use a <c>using</c> statement or <c>await using</c> for async operations.
/// </para>
/// <para>
/// Result sets are read sequentially. After reading a result set, the reader automatically advances 
/// to the next result set. When all result sets have been consumed, the reader is automatically disposed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Read multiple result sets
/// using var grid = connection.QueryMultiple(
///     "SELECT * FROM Products WHERE Id = @Id; SELECT * FROM Categories WHERE Id = @CategoryId");
/// 
/// var product = grid.ReadFirst&lt;Product&gt;();
/// var category = grid.ReadFirst&lt;Category&gt;();
/// 
/// // GridReader is automatically disposed at the end of the using block
/// </code>
/// </example>
/// <seealso cref="Jaunty.QueryMultiple(IDbConnection, string)"/>
/// <seealso cref="Jaunty.QueryMultipleAsync(IDbConnection, string, CancellationToken)"/>
public sealed class GridReader : IDisposable, IAsyncDisposable
{
    private readonly IDataReader reader;
    private readonly IDbConnection connection;
    private readonly bool closeConnection;
    private readonly IDbCommand? command;

    private bool _consumed;

    /// <summary>
    /// Initializes a new <see cref="GridReader"/> over an open reader.
    /// </summary>
    /// <param name="reader">The reader positioned on the first result set.</param>
    /// <param name="connection">The connection the reader was opened on.</param>
    /// <param name="closeConnection">Whether disposing this instance should close the connection.</param>
    /// <param name="command">The command that produced the reader, disposed with this instance.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="reader"/> or <paramref name="connection"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// AUD-R35-150. This was a primary constructor that validated nothing, on a <c>public sealed</c>
    /// type: <c>new GridReader(null!, null!, false)</c> was accepted and failed later with a bare
    /// <see cref="NullReferenceException"/> from <c>reader.Read()</c>, or from <c>connection.State</c>
    /// during <see cref="Dispose"/> - which is the worse one, because it throws out of a
    /// <c>using</c> block's implicit finally. Every other public entry point in the library opens
    /// with <c>ThrowIfNull</c>; <c>ExecuteQueryMultiple</c> being the only in-repo caller does not
    /// take the type off the public surface.
    /// </remarks>
    public GridReader(IDataReader reader, IDbConnection connection, bool closeConnection, IDbCommand? command = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        this.reader = reader;
        this.connection = connection;
        this.closeConnection = closeConnection;
        this.command = command;
    }

    /// <summary>
    /// Reads all rows from the current result set as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>A list of entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method uses strict mapping mode. All properties on <typeparamref name="T"/> must have 
    /// matching columns in the result set.
    /// </para>
    /// <para>
    /// After calling this method, the reader advances to the next result set.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when all result sets have already been consumed.
    /// </exception>
    public List<T> Read<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadCore(options, MappingMode.Strict);
    }

    /// <summary>
    /// Reads all rows from the current result set as a list of entities of type <typeparamref name="T"/> using partial mapping.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>A list of entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method uses partial mapping mode. Only properties with matching columns are mapped; 
    /// properties without matching columns are left with their default values.
    /// </para>
    /// </remarks>
    public List<T> ReadPartial<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadCore(options, MappingMode.Projection);
    }

    /// <summary>
    /// Reads the first row from the current result set as an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>The first entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// <para>
    /// This method uses strict mapping mode.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result set is empty or when all result sets have already been consumed.
    /// </exception>
    public T ReadFirst<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadFirstCore(options, MappingMode.Strict);
    }

    /// <summary>
    /// Reads the first row from the current result set as an entity of type <typeparamref name="T"/>, 
    /// or returns <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>The first entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// This method uses strict mapping mode.
    /// </para>
    /// </remarks>
    public T? ReadFirstOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadFirstOrDefaultCore(options, MappingMode.Strict);
    }

    /// <summary>
    /// Reads the first row from the current result set as an entity of type <typeparamref name="T"/> using partial mapping.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>The first entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    public T ReadPartialFirst<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadFirstCore(options, MappingMode.Projection);
    }

    /// <summary>
    /// Reads the first row from the current result set as an entity of type <typeparamref name="T"/> using partial mapping,
    /// or returns <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>The first entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.</returns>
    public T? ReadPartialFirstOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadFirstOrDefaultCore(options, MappingMode.Projection);
    }

    /// <summary>
    /// Reads exactly one row from the current result set as an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>The single entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>The result set is empty</description></item>
    /// <item><description>The result set contains more than one row</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result set contains zero or more than one row.
    /// </exception>
    public T ReadSingle<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadSingleCore(options, MappingMode.Strict);
    }

    /// <summary>
    /// Reads exactly one row from the current result set as an entity of type <typeparamref name="T"/>, 
    /// or returns <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>
    /// The single entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the result set contains more than one row.</strong>
    /// </para>
    /// </remarks>
    public T? ReadSingleOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadSingleOrDefaultCore(options, MappingMode.Strict);
    }

    /// <summary>
    /// Reads exactly one row from the current result set as an entity of type <typeparamref name="T"/> using partial mapping.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>The single entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the result set doesn't contain exactly one row.</strong>
    /// </para>
    /// </remarks>
    public T ReadPartialSingle<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadSingleCore(options, MappingMode.Projection);
    }

    /// <summary>
    /// Reads exactly one row from the current result set as an entity of type <typeparamref name="T"/> using partial mapping,
    /// or returns <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>
    /// The single entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the result set contains more than one row.</strong>
    /// </para>
    /// </remarks>
    public T? ReadPartialSingleOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadSingleOrDefaultCore(options, MappingMode.Projection);
    }

    /// <summary>
    /// Reads a scalar value from the first column of the first row of the current result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the scalar value to.</typeparam>
    /// <param name="options">
    /// Accepted for signature symmetry with the other <c>Read*</c> methods and <b>not used</b>.
    /// See the remarks.
    /// </param>
    /// <returns>The scalar value converted to type <typeparamref name="T"/>, or default if there are no rows or the value is null.</returns>
    /// <remarks>
    /// <para>
    /// This method reads a single value from the first column of the first row.
    /// </para>
    /// <para>
    /// A genuine failure to convert the raw value to <typeparamref name="T"/> is not treated
    /// as "no value" — it propagates as an exception so callers can distinguish an empty/null
    /// result from a real type mismatch.
    /// </para>
    /// <para>
    /// AUD-R26-054 (batch 4, low/bug): the finding was that <paramref name="options"/> is never
    /// referenced while every sibling <c>Read*</c> threads its options into the core it delegates
    /// to. It is documented rather than wired up, because there is nothing on
    /// <see cref="CommandOptions"/> that a grid scalar read can honour: <c>Transaction</c>,
    /// <c>CommandTimeout</c> and <c>CommandType</c> configure the execution of a command that has
    /// already run by the time a <see cref="GridReader"/> exists, and <c>ExpectedRowCount</c>
    /// pre-sizes a result list that a scalar does not have. The siblings consume exactly two things
    /// - <c>ExpectedRowCount</c> and the <c>Mapper</c> on the generic
    /// <see cref="CommandOptions{T}"/> - and neither has a meaning here. Removing the parameter
    /// would be a source-breaking change to a public API for no behavioural gain, so it stays and
    /// says so.
    /// </para>
    /// </remarks>
    public T? ReadScalar<T>(CommandOptions options = default)
    {
        EnsureNotConsumed();
        try
        {
            return reader.Read() && !reader.IsDBNull(0) ? ConvertScalar<T>(reader) : default;
        }
        finally
        {
            Advance();
        }
    }

    /// <summary>
    /// Reads column 0 of the current row as <typeparamref name="T"/>: the provider's own typed
    /// accessor where it works, and <c>ScalarConverter&lt;T&gt;</c> where it does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26-054 (batch 4, low/bug). The fallback used to be
    /// <c>Convert.ChangeType(value, typeof(T))</c> written out here, while the
    /// <c>QueryScalar</c>/<c>ExecuteScalar</c> family went through <c>ScalarConverter&lt;T&gt;</c> -
    /// so the library had two scalar conversion behaviours and the grid had the weaker one.
    /// <c>Convert.ChangeType</c> handles
    /// neither a <see cref="Nullable{T}"/> target nor the types that do not implement
    /// <see cref="IConvertible"/> (<see cref="Guid"/>, <see cref="DateTimeOffset"/>,
    /// <see cref="TimeSpan"/>, <c>DateOnly</c>, <c>TimeOnly</c>) nor an enum arriving as its name;
    /// <c>ScalarConverter</c> handles all of them.
    /// </para>
    /// <para>
    /// That mattered most exactly where it was least visible. The fallback's original comment named
    /// SQLite as a provider that throws from <c>GetFieldValue&lt;T&gt;</c> "even though a value is
    /// present" - and SQLite is also the provider that stores dates, times and GUIDs as text. So on
    /// the provider most likely to need the richer conversion, this path was guaranteed to reach the
    /// poorer one.
    /// </para>
    /// <para>
    /// The typed accessor stays as the fast path rather than being replaced: where it succeeds it is
    /// the provider's own answer, and narrowing the change to the fallback is what makes the two
    /// scalar paths agree everywhere they previously disagreed without altering any case that
    /// already worked. A conversion that genuinely cannot be done still propagates, so callers keep
    /// the documented distinction between "no value" and "wrong type".
    /// </para>
    /// </remarks>
    private static T? ConvertScalar<T>(IDataReader reader)
    {
        if (reader is DbDataReader dbReader)
        {
            try
            {
                return dbReader.GetFieldValue<T>(0);
            }
            catch (Exception ex) when (ex is InvalidCastException or NullReferenceException or IndexOutOfRangeException)
            {
                // Provider could not produce T directly; convert from the raw value below.
            }
        }

        return ScalarConverter<T>.Convert(reader.GetValue(0));
    }

    /// <summary>
    /// Streams all rows from the current result set as entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method uses strict mapping mode and streams results without buffering.
    /// </para>
    /// </remarks>
    public IEnumerable<T> ReadStream<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadStreamCore(options, MappingMode.Strict);
    }

    /// <summary>
    /// Streams all rows from the current result set as entities of type <typeparamref name="T"/> using partial mapping.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/>.</returns>
    public IEnumerable<T> ReadPartialStream<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadStreamCore(options, MappingMode.Projection);
    }

    private List<T> ReadCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        var results = new List<T>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

        // Resolve lazily, matching ReadAsyncCore and the four First/Single terminals. Resolution is
        // not side-effect-free: DrDispatcher.Resolve throws when no mapper is registered, and for a
        // source-generated entity in Strict mode it runs MapperFactory, which validates the
        // result-set shape. Resolving up front meant an *empty* result set could throw from
        // grid.Read<T>() while await grid.ReadAsync<T>() on the identical grid returned an empty
        // list and grid.ReadFirstOrDefault<T>() returned null - three public APIs disagreeing on
        // whether an empty result set is an error, with nothing in their docs distinguishing them.
        Func<IDataReader, T>? map = null;

        try
        {
            while (reader.Read())
            {
                map ??= DrDispatcher.Resolve(reader, options, mode);
                results.Add(map(reader));
            }
        }
        finally
        {
            Advance();
        }

        return results;
    }

    private T? ReadFirstOrDefaultCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        try
        {
            if (!reader.Read()) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        }
        finally
        {
            Advance();
        }
    }

    // Separate from ReadFirstOrDefaultCore rather than layering "?? throw" on top of it: for an
    // unconstrained T (no class/struct constraint), T? does not compile to Nullable<T> for value
    // types, so a value-type T's "no rows" default(T) (e.g. 0) is indistinguishable from a real
    // value via "?? throw" -- the throw would never fire. Checking reader.Read() directly here
    // sidesteps that footgun entirely.
    private T ReadFirstCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        try
        {
            if (!reader.Read())
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        }
        finally
        {
            Advance();
        }
    }

    private T? ReadSingleOrDefaultCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        try
        {
            if (!reader.Read()) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);
            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.") : entity;
        }
        finally
        {
            Advance();
        }
    }

    // See ReadFirstCore for why this can't be built on top of ReadSingleOrDefaultCore + "?? throw".
    private T ReadSingleCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        try
        {
            if (!reader.Read())
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);
            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.") : entity;
        }
        finally
        {
            Advance();
        }
    }

    // Eagerly validates (EnsureNotConsumed) rather than deferring to first enumeration: a
    // yield-return method only executes its body once enumerated, so a check placed there would
    // silently never run for a caller who discards the returned IEnumerable<T> (or breaks out of a
    // foreach early) without enumerating it. Splitting into a thin eager wrapper plus a private
    // iterator ensures misuse (e.g. a second read on an already-consumed grid) is caught
    // immediately at call time instead of being deferred to whenever/if enumeration happens.
    private IEnumerable<T> ReadStreamCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        return ReadStreamIterator(options, mode);
    }

    private IEnumerable<T> ReadStreamIterator<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        // Lazy, like ReadStreamAsyncIterator - see ReadCore for why resolving before the first Read
        // makes an empty result set throw here but not on the async or First/Single paths.
        Func<IDataReader, T>? map = null;

        try
        {
            while (reader.Read())
            {
                map ??= DrDispatcher.Resolve(reader, options, mode);
                yield return map(reader);
            }
        }
        finally
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (!reader.NextResult())
        {
            _consumed = true;
            Dispose();
        }
    }

    /// <summary>
    /// Asynchronously reads all rows from the current result set as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="options">Optional command options for custom mapping.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing a list of entities of type <typeparamref name="T"/>.</returns>
    public Task<List<T>> ReadAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadAsyncCore(options, MappingMode.Strict, cancellationToken);

    /// <summary>
    /// Asynchronously reads all rows from the current result set as a list of entities of type <typeparamref name="T"/> using partial mapping.
    /// </summary>
    public Task<List<T>> ReadPartialAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadAsyncCore(options, MappingMode.Projection, cancellationToken);

    /// <summary>
    /// Asynchronously reads the first row from the current result set as an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result set is empty.
    /// </exception>
    public Task<T> ReadFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstAsyncCore(options, MappingMode.Strict, cancellationToken);

    /// <summary>
    /// Asynchronously reads the first row from the current result set as an entity of type <typeparamref name="T"/>, 
    /// or returns <see langword="null"/> if no results are found.
    /// </summary>
    public Task<T?> ReadFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstOrDefaultAsyncCore(options, MappingMode.Strict, cancellationToken);

    /// <summary>
    /// Asynchronously reads the first row from the current result set using partial mapping.
    /// </summary>
    public Task<T> ReadPartialFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstAsyncCore(options, MappingMode.Projection, cancellationToken);

    /// <summary>
    /// Asynchronously reads the first row from the current result set using partial mapping, or returns <see langword="null"/>.
    /// </summary>
    public Task<T?> ReadPartialFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstOrDefaultAsyncCore(options, MappingMode.Projection, cancellationToken);

    /// <summary>
    /// Asynchronously reads exactly one row from the current result set.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result set doesn't contain exactly one row.
    /// </exception>
    public Task<T> ReadSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleAsyncCore(options, MappingMode.Strict, cancellationToken);

    /// <summary>
    /// Asynchronously reads exactly one row from the current result set, or returns <see langword="null"/>.
    /// </summary>
    public Task<T?> ReadSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleOrDefaultAsyncCore(options, MappingMode.Strict, cancellationToken);

    /// <summary>
    /// Asynchronously reads exactly one row using partial mapping.
    /// </summary>
    public Task<T> ReadPartialSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleAsyncCore(options, MappingMode.Projection, cancellationToken);

    /// <summary>
    /// Asynchronously reads exactly one row using partial mapping, or returns <see langword="null"/>.
    /// </summary>
    public Task<T?> ReadPartialSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleOrDefaultAsyncCore(options, MappingMode.Projection, cancellationToken);

    /// <summary>
    /// Asynchronously reads a scalar value from the first column of the first row.
    /// </summary>
    /// <typeparam name="T">The type to convert the scalar value to.</typeparam>
    /// <param name="options">
    /// Accepted for signature symmetry and <b>not used</b> - see <see cref="ReadScalar{T}"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scalar value converted to type <typeparamref name="T"/>, or default if there are no rows or the value is null.</returns>
    public async Task<T?> ReadScalarAsync<T>(CommandOptions options = default, CancellationToken cancellationToken = default)
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        try
        {
            T? result = default;
            if (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)
                && !await dbReader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    result = await dbReader.GetFieldValueAsync<T>(0, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidCastException or NullReferenceException or IndexOutOfRangeException)
                {
                    // Same fallback as the sync ReadScalar<T>, and through the same converter - see
                    // ConvertScalar<T>. A genuine conversion failure still propagates rather than
                    // being reported as "no value" (default).
                    result = ScalarConverter<T>.Convert(dbReader.GetValue(0));
                }
            }
            return result;
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously streams all rows from the current result set as entities of type <typeparamref name="T"/>.
    /// </summary>
    public IAsyncEnumerable<T> ReadStreamAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        return ReadStreamAsyncIterator(dbReader, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously streams all rows from the current result set using partial mapping.
    /// </summary>
    public IAsyncEnumerable<T> ReadPartialStreamAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        return ReadStreamAsyncIterator(dbReader, options, MappingMode.Projection, cancellationToken);
    }

    // See ReadStreamCore for why EnsureNotConsumed/the DbDataReader check run eagerly in the two
    // public wrappers above rather than here: an async-iterator body only executes once
    // enumeration begins, so validation placed here would silently never run for a caller who
    // discards the returned IAsyncEnumerable<T> without enumerating it.
    private async IAsyncEnumerable<T> ReadStreamAsyncIterator<T>(DbDataReader dbReader, CommandOptions<T> options, MappingMode mode, [EnumeratorCancellation] CancellationToken cancellationToken) where T : new()
    {
        Func<IDataReader, T>? map = null;

        try
        {
            while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map ??= DrDispatcher.Resolve(reader, options, mode);
                yield return map(reader);
            }
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<T>> ReadAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        var results = new List<T>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);
        Func<IDataReader, T>? map = null;

        try
        {
            while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map ??= DrDispatcher.Resolve(reader, options, mode);
                results.Add(map(reader));
            }
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    private async Task<T?> ReadFirstOrDefaultAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        try
        {
            if (!await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // See the sync ReadFirstCore for why this can't be built on top of
    // ReadFirstOrDefaultAsyncCore + "?? throw" (unconstrained T? isn't Nullable<T> for value types).
    private async Task<T> ReadFirstAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        try
        {
            if (!await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T?> ReadSingleOrDefaultAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        try
        {
            if (!await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);

            return await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                : entity;
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // See ReadFirstAsyncCore for why this can't be built on top of ReadSingleOrDefaultAsyncCore + "?? throw".
    private async Task<T> ReadSingleAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        try
        {
            if (!await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);

            return await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.")
                : entity;
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AdvanceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            bool hasNext = reader is DbDataReader dbReader
                ? await dbReader.NextResultAsync(cancellationToken).ConfigureAwait(false)
                : reader.NextResult();
            if (!hasNext)
            {
                _consumed = true;
                await DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (NullReferenceException)
        {
            // Some ADO.NET providers (observed with SQLite) throw NullReferenceException from
            // NextResultAsync when there are no more result sets, instead of returning false the
            // way the synchronous NextResult() does on the same providers; treat it as end-of-results.
            _consumed = true;
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    private void EnsureNotConsumed()
    {
        if (_consumed)
            throw new InvalidOperationException("All result sets have already been consumed.");
    }

    /// <summary>
    /// Disposes the reader and closes the connection if requested.
    /// </summary>
    public void Dispose()
    {
        // R27 batch 6: without this, a Read* after an explicit early Dispose passed
        // EnsureNotConsumed and failed inside the provider's disposed reader instead of with
        // the documented InvalidOperationException.
        _consumed = true;
        reader.Dispose();
        command?.Dispose();
        if (closeConnection && connection.State != ConnectionState.Closed)
            connection.Close();
    }

    /// <summary>
    /// Asynchronously disposes the reader and closes the connection if requested.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _consumed = true;
        GC.SuppressFinalize(this);

        if (reader is IAsyncDisposable asyncReader)
            await asyncReader.DisposeAsync().ConfigureAwait(false);
        else
            reader.Dispose();

        command?.Dispose();

        if (closeConnection && connection.State != ConnectionState.Closed)
        {
#if NET8_0_OR_GREATER
            if (connection is DbConnection dbConnection)
                await dbConnection.CloseAsync().ConfigureAwait(false);
            else
                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#else
            await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
        }
    }
}