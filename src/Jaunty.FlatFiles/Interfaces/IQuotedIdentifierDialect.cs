namespace Jaunty.FlatFiles.Interfaces;

/// <summary>
/// Optional companion to <see cref="IImportDialect"/> that exposes the dialect's identifier
/// quoting to the import pipeline itself, not just to the SQL the dialect generates.
/// </summary>
/// <remarks>
/// <para>
/// The import pipeline occasionally has to emit an identifier outside
/// <see cref="IImportDialect.GenerateInsertSql"/>/<see cref="IImportDialect.GenerateCreateTableSql"/>
/// - the target-table existence probe, for example. Without this it had to hardcode
/// double-quote quoting, which contradicts SQL Server's own rationale for using
/// <c>[brackets]</c>: double quotes only work when <c>QUOTED_IDENTIFIER</c> is ON.
/// </para>
/// <para>
/// This is deliberately a separate interface rather than a member on
/// <see cref="IImportDialect"/>: that interface is public and this assembly targets
/// netstandard2.0, where a default interface implementation isn't available to keep existing
/// external implementers compiling. Dialects that don't implement this get the SQL-standard
/// double-quote form, which is what the pipeline used unconditionally before.
/// </para>
/// </remarks>
public interface IQuotedIdentifierDialect
{
    /// <summary>
    /// Quotes an identifier (table or column name) for this dialect, escaping any embedded
    /// quote characters.
    /// </summary>
    /// <param name="identifier">The raw identifier.</param>
    /// <returns>The quoted identifier, ready to embed in SQL.</returns>
    string QuoteIdentifier(string identifier);
}
