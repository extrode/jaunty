using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Base interface for terminal query operations (Select, Count, etc.)
/// All query builder interfaces inherit from this to provide terminal methods.
/// </summary>
public interface IQueryTerminal<T> where T : new()
{
    // Full entity selection (strict mapping - all columns)
    List<T> Select();
    T SelectFirst();
    T? SelectFirstOrDefault();
    T SelectSingle();
    T? SelectSingleOrDefault();

    // Partial entity selection - string-based column specification
    List<T> SelectPartial(params string[] columns);
    T SelectPartialFirst(params string[] columns);
    T? SelectPartialFirstOrDefault(params string[] columns);
    T SelectPartialSingle(params string[] columns);
    T? SelectPartialSingleOrDefault(params string[] columns);

    // Partial entity selection - expression-based column specification
    List<T> SelectPartial(params Expression<Func<T, object?>>[] columns);
    T SelectPartialFirst(params Expression<Func<T, object?>>[] columns);
    T? SelectPartialFirstOrDefault(params Expression<Func<T, object?>>[] columns);
    T SelectPartialSingle(params Expression<Func<T, object?>>[] columns);
    T? SelectPartialSingleOrDefault(params Expression<Func<T, object?>>[] columns);

    // Scalar aggregates
    int Count();
    long LongCount();

    // Async variants - full entity selection
    Task<List<T>> SelectAsync(CancellationToken cancellationToken = default);
    Task<T> SelectFirstAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<T> SelectSingleAsync(CancellationToken cancellationToken = default);
    Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    // Async variants - partial entity selection (string-based)
    Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialFirstAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialFirstOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialSingleAsync(string[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialSingleOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default);

    // Async variants - partial entity selection (expression-based)
    Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialFirstAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialFirstOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T> SelectPartialSingleAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);
    Task<T?> SelectPartialSingleOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default);

    // Async aggregates
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    // SQL introspection (for debugging/logging)
    string ToSql();
    string ToSql(params string[] columns);
    string ToSql(params Expression<Func<T, object?>>[] columns);
}
