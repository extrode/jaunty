using System.Data;

using Jaunty.Core;

namespace Jaunty.Internals.Write;

/// <summary>
/// AUD-R35-101 (round-35 batch 03b). The six Bulk* cores passed <c>options.CommandType</c> to
/// <c>CommandObservation</c> and then never applied it to the command, which
/// stayed at the provider default of <see cref="CommandType.Text"/>. Because
/// <see cref="CommandOptions.AsStoredProcedure"/> is public and was accepted here without a
/// diagnostic, an interceptor or <c>DiagnosticListener</c> could be told a bulk delete ran as
/// <see cref="CommandType.StoredProcedure"/> while the executed <see cref="IDbCommand"/> ran as
/// <see cref="CommandType.Text"/>.
/// <para>
/// Propagating it is not the fix. A bulk operation executes SQL Jaunty generated from the entity's
/// metadata - a multi-row INSERT, a <c>DELETE ... WHERE id IN (...)</c> - so there is no procedure
/// name for the provider to resolve and <see cref="CommandType.StoredProcedure"/> could only fail at
/// the server. The option is meaningless on this path, so it is rejected at the entry point with a
/// message that says so, in the manner of the other Bulk* argument validators.
/// </para>
/// </summary>
internal static class BulkCommandTypeValidator
{
    /// <remarks>
    /// <c>default(CommandOptions).CommandType</c> is <c>0</c>, not <see cref="CommandType.Text"/>
    /// (which is <c>1</c>), and every caller that omits options passes <c>default</c>. Unset and Text
    /// are therefore the same thing here, exactly as the <c>is StoredProcedure or TableDirect</c>
    /// allow-lists elsewhere in the codebase treat them.
    /// </remarks>
    internal static void ThrowIfNotText(CommandOptions options, string operation)
    {
        if (options.CommandType is not (default(CommandType) or CommandType.Text))
            throw new ArgumentException(
                $"{operation} generates its own SQL, so CommandOptions.CommandType must be Text; got {options.CommandType}.",
                nameof(options));
    }
}
