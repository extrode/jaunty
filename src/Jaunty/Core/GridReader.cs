using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Internals;
using Jaunty.Internals.Enums;

namespace Jaunty.Core;

public sealed class GridReader(IDataReader reader, IDbConnection connection, bool closeConnection) : IDisposable, IAsyncDisposable
{
    private bool _consumed;

    public List<T> Read<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadCore(options, MappingMode.Strict);
    }

    public List<T> ReadPartial<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadCore(options, MappingMode.Projection);
    }

    public T ReadFirst<T>(CommandOptions<T> options = default) where T : new()
    {
        var result = ReadFirstOrDefaultCore(options, MappingMode.Strict);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public T? ReadFirstOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadFirstOrDefaultCore(options, MappingMode.Strict);
    }

    public T ReadPartialFirst<T>(CommandOptions<T> options = default) where T : new()
    {
        var result = ReadFirstOrDefaultCore(options, MappingMode.Projection);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public T? ReadPartialFirstOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadFirstOrDefaultCore(options, MappingMode.Projection);
    }

    public T ReadSingle<T>(CommandOptions<T> options = default) where T : new()
    {
        var result = ReadSingleOrDefaultCore(options, MappingMode.Strict);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public T? ReadSingleOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadSingleOrDefaultCore(options, MappingMode.Strict);
    }

    public T ReadPartialSingle<T>(CommandOptions<T> options = default) where T : new()
    {
        var result = ReadSingleOrDefaultCore(options, MappingMode.Projection);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public T? ReadPartialSingleOrDefault<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadSingleOrDefaultCore(options, MappingMode.Projection);
    }

    public T? ReadScalar<T>(CommandOptions options = default)
    {
        EnsureNotConsumed();
        try
        {
            T? result = default;
            if (reader.Read() && !reader.IsDBNull(0))
            {
                if (reader is DbDataReader dbReader)
                    result = dbReader.GetFieldValue<T>(0);
                else
                {
                    var val = reader.GetValue(0);
                    result = (T)Convert.ChangeType(val, typeof(T));
                }
            }
            return result;
        }
        finally
        {
            Advance();
        }
    }

    public IEnumerable<T> ReadStream<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadStreamCore(options, MappingMode.Strict);
    }

    public IEnumerable<T> ReadPartialStream<T>(CommandOptions<T> options = default) where T : new()
    {
        return ReadStreamCore(options, MappingMode.Projection);
    }

    private List<T> ReadCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        var results = new List<T>();
        var map = DrDispatcher.Resolve(reader, options, mode);

        while (reader.Read())
            results.Add(map(reader));

        Advance();
        return results;
    }

    private T? ReadFirstOrDefaultCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        try
        {
            if (!reader.Read()) return default;
            var map = DrDispatcher.Resolve(reader, options, mode);
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
            var map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);
            return reader.Read() ? throw new InvalidOperationException("Sequence contains more than one element") : entity;
        }
        finally
        {
            Advance();
        }
    }

    private IEnumerable<T> ReadStreamCore<T>(CommandOptions<T> options, MappingMode mode) where T : new()
    {
        EnsureNotConsumed();
        var map = DrDispatcher.Resolve(reader, options, mode);

        while (reader.Read())
            yield return map(reader);

        Advance();
    }

    private void Advance()
    {
        if (!reader.NextResult())
        {
            _consumed = true;
            Dispose();
        }
    }

    public Task<List<T>> ReadAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadAsyncCore(options, MappingMode.Strict, cancellationToken);

    public Task<List<T>> ReadPartialAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadAsyncCore(options, MappingMode.Projection, cancellationToken);

    public async Task<T> ReadFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var result = await ReadFirstOrDefaultAsyncCore(options, MappingMode.Strict, cancellationToken);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public Task<T?> ReadFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstOrDefaultAsyncCore(options, MappingMode.Strict, cancellationToken);

    public async Task<T> ReadPartialFirstAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var result = await ReadFirstOrDefaultAsyncCore(options, MappingMode.Projection, cancellationToken);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public Task<T?> ReadPartialFirstOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstOrDefaultAsyncCore(options, MappingMode.Projection, cancellationToken);

    public async Task<T> ReadSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var result = await ReadSingleOrDefaultAsyncCore(options, MappingMode.Strict, cancellationToken);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public Task<T?> ReadSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleOrDefaultAsyncCore(options, MappingMode.Strict, cancellationToken);

    public async Task<T> ReadPartialSingleAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var result = await ReadSingleOrDefaultAsyncCore(options, MappingMode.Projection, cancellationToken);
        return result ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public Task<T?> ReadPartialSingleOrDefaultAsync<T>(CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleOrDefaultAsyncCore(options, MappingMode.Projection, cancellationToken);

    public async Task<T?> ReadScalarAsync<T>(CommandOptions options = default, CancellationToken cancellationToken = default)
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

try
        {
            T? result = default;
            if (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    if (!await dbReader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                        result = await dbReader.GetFieldValueAsync<T>(0, cancellationToken).ConfigureAwait(false);
                }
                catch (NullReferenceException)
                {
                    // Handle SQLite DataReader edge case with empty result sets
                    result = default;
                }
            }
            return result;
        }
        finally
        {
            await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<T> ReadStreamAsync<T>(CommandOptions<T> options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        var opts = options;
        var map = DrDispatcher.Resolve(reader, opts, MappingMode.Strict);

        while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return map(reader);
        await AdvanceAsync(cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<T> ReadPartialStreamAsync<T>(CommandOptions<T> options = default, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        var opts = options;
        var map = DrDispatcher.Resolve(reader, opts, MappingMode.Projection);

        while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return map(reader);
        await AdvanceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<T>> ReadAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        var results = new List<T>();
        var map = DrDispatcher.Resolve(reader, options, mode);

        while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(map(reader));
        await AdvanceAsync(cancellationToken).ConfigureAwait(false);
        return results;
    }

    private async Task<T?> ReadFirstOrDefaultAsyncCore<T>(CommandOptions<T> options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        EnsureNotConsumed();
        if (reader is not DbDataReader dbReader) throw new NotSupportedException("Async operations require a DbDataReader.");

        try
        {
            if (!await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)) return default;
            var map = DrDispatcher.Resolve(reader, options, mode);
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
            var map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);

            return await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? throw new InvalidOperationException("Sequence contains more than one element")
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
                await DisposeAsync();
            }
        }
        catch (NullReferenceException)
        {
            // Handle SQLite DataReader edge case where NextResult throws on empty result sets
            _consumed = true;
            await DisposeAsync();
        }
    }

    private void EnsureNotConsumed()
    {
        if (_consumed)
            throw new InvalidOperationException("All result sets have already been consumed.");
    }

    public void Dispose()
    {
        reader.Dispose();
        if (closeConnection && connection.State != ConnectionState.Closed)
            connection.Close();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (reader is IAsyncDisposable asyncReader)
            await asyncReader.DisposeAsync().ConfigureAwait(false);
        else
            reader.Dispose();

        if (closeConnection && connection.State != ConnectionState.Closed)
        {
#if NET8_0_OR_GREATER
            var dbConn = connection as DbConnection;
            await dbConn!.CloseAsync().ConfigureAwait(false);
#else
            connection.Close();
#endif
        }
    }
}
