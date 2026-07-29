using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty.Internals.Read;

/// <summary>
/// Produces the single value a scalar query returns, honouring a caller-supplied mapper when there
/// is one.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26. All eight <c>CommandOptions&lt;T&gt;</c> overloads across <c>QueryScalar</c>,
/// <c>QueryScalarAsync</c>, <c>ExecuteScalar</c> and <c>ExecuteScalarAsync</c> accepted an options
/// value whose <c>WithMapper(Func&lt;IDataReader, T&gt;)</c> factory was silently discarded. The
/// cores read <c>CommandType</c>, <c>Transaction</c> and <c>CommandTimeout</c> and then called
/// <c>ExecuteScalar()</c>; <c>options.Mapper</c> was never consulted on any of the four code paths.
/// The generic parameter on <see cref="CommandOptions{T}"/> exists <em>only</em> to carry the
/// mapper - the non-generic <see cref="CommandOptions"/> covers transaction, timeout and command
/// type - so these signatures advertised a capability they did not have. A caller writing
/// <c>QueryScalar&lt;decimal&gt;(sql, CommandOptions&lt;decimal&gt;.WithMapper(...))</c> got no
/// compile error and no runtime error; the mapper was simply not called.
/// </para>
/// <para>
/// <strong>Honoured rather than rejected.</strong> Narrowing the four files to the non-generic
/// <see cref="CommandOptions"/> - the more honest shape the neighbouring
/// <c>GridReader.ReadScalar&lt;T&gt;</c> chose - would be a source-breaking change to eight public
/// overloads over a low-severity finding. Throwing on a supplied mapper would break callers who pass
/// one today and get away with it. Calling it costs nothing when there is none and makes the
/// signature true, so that is what happens.
/// </para>
/// <para>
/// <strong>The no-mapper path is byte-identical to what it replaced</strong> - same
/// <c>ExecuteScalar</c>, same <c>null</c>/<see cref="DBNull"/> handling, same
/// <c>is T direct</c> short-circuit before <c>ScalarConverter&lt;T&gt;</c>. Only the presence of a
/// mapper changes anything, and only for callers who supplied one.
/// </para>
/// </remarks>
internal static class ScalarExecution
{
    /// <summary>
    /// Executes <paramref name="command"/> and returns its single value, or
    /// <see langword="default"/> when the query produced nothing.
    /// </summary>
    /// <remarks>
    /// With a mapper the command runs as a reader rather than a scalar, because a mapper is handed
    /// the <see cref="IDataReader"/> and <c>ExecuteScalar</c> never produces one. An empty result set
    /// yields <see langword="default"/>, matching what <c>ExecuteScalar</c> returns for no rows -
    /// the mapper is not called with a reader positioned before any row.
    /// </remarks>
    public static T Execute<T>(IDbCommand command, Func<IDataReader, T>? mapper)
    {
        if (mapper is null)
        {
            object? result = command.ExecuteScalar();

            if (result is null or DBNull)
                return default!;

            return result is T direct ? direct : ScalarConverter<T>.Convert(result);
        }

        using IDataReader reader = command.ExecuteReader();

        return reader.Read() ? mapper(reader) : default!;
    }

    /// <summary>Asynchronous counterpart to <see cref="Execute{T}"/>.</summary>
    public static async ValueTask<T> ExecuteAsync<T>(
        DbCommand command,
        Func<IDataReader, T>? mapper,
        CancellationToken cancellationToken)
    {
        if (mapper is null)
        {
            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (result is null or DBNull)
                return default!;

            return result is T direct ? direct : ScalarConverter<T>.Convert(result);
        }

#if NET8_0_OR_GREATER
        DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposer = reader.ConfigureAwait(false);
#else
        using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? mapper(reader)
            : default!;
    }
}
