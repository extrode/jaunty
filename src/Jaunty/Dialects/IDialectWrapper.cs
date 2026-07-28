namespace Jaunty.Dialects;

/// <summary>
/// Implemented by an <see cref="ISqlDialect"/> that decorates another dialect rather than being
/// one in its own right, so callers that need to know which engine they are talking to can see
/// through the decoration.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SqlDialectFactory.GetDialect(System.Data.IDbConnection)"/> runs every resolved
/// dialect through an optional bulk-copy enhancement step, which - once
/// <c>UseNativeBulkCopy()</c> has been called - replaces the concrete
/// <see cref="SQLiteDialect"/>/<see cref="PostgreSqlDialect"/>/<see cref="MySqlDialect"/>/<see cref="SqlServerDialect"/>
/// with a wrapper that <em>implements</em> <see cref="ISqlDialect"/> and delegates to the original
/// rather than deriving from it. Any <c>dialect is SQLiteDialect</c>-style test therefore stops
/// matching the moment that opt-in is enabled, even though the underlying engine has not changed.
/// </para>
/// <para>
/// Wrappers implement this interface and expose the dialect they decorate; callers that need the
/// engine identity call <see cref="SqlDialectFactory.Unwrap"/> before testing the type. Dialects
/// that are not decorators do not implement this interface and are returned by
/// <see cref="SqlDialectFactory.Unwrap"/> unchanged.
/// </para>
/// </remarks>
public interface IDialectWrapper
{
    /// <summary>
    /// Gets the dialect this instance decorates. Never <see langword="null"/>.
    /// </summary>
    ISqlDialect InnerDialect { get; }
}
