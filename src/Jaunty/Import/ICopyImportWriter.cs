using System.Data;

namespace Jaunty.Import;

/// <summary>
/// A provider's client-side bulk-copy stream, as Jaunty needs to use it: something to write CSV
/// text into, and a way to abort rather than commit what has been written.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that core does not have to reach a provider's copy API by reflection. Jaunty
/// core declares no dependency on any database driver, and until this interface the PostgreSQL
/// client-side <c>COPY ... FROM STDIN</c> path was reached with five
/// <c>connection.GetType().GetMethod(...)</c> probes - for <c>BeginTextImport</c>, <c>Cancel</c>
/// and <c>CancelAsync</c>. Every one of them returned null under trimming, and the
/// <c>Cancel</c> ones failed dangerously when they did: see <see cref="Cancel"/>.
/// </para>
/// <para>
/// Install an implementation with <c>JauntyConfig.CopyImportFactory</c>.
/// The <c>Extrode.Jaunty.Extensions.Npgsql</c> package does this for PostgreSQL; you can register
/// your own for any driver with an equivalent API.
/// </para>
/// </remarks>
public interface ICopyImportWriter : IDisposable
{
    /// <summary>
    /// The stream to write CSV text into.
    /// </summary>
    TextWriter Writer { get; }

    /// <summary>
    /// Aborts the copy, discarding everything written so far.
    /// </summary>
    /// <remarks>
    /// <strong>Implementing this is not optional, and a no-op implementation is a data-integrity
    /// bug.</strong> A copy stream of this kind typically <em>completes</em> the copy when it is
    /// disposed and aborts only on an explicit cancel - so if a failure part-way through the file
    /// unwinds through <c>using</c> without a cancel, the rows written so far are committed while
    /// the caller sees an exception saying the import failed. That divergence is silent: no error
    /// names it, and the table simply holds part of the file.
    /// </remarks>
    void Cancel();

    /// <summary>
    /// Aborts the copy asynchronously. Implementations without a native asynchronous abort should
    /// call <see cref="Cancel"/> and return a completed task.
    /// </summary>
    ValueTask CancelAsync();
}

/// <summary>
/// Creates an <see cref="ICopyImportWriter"/> for a connection, or returns <see langword="null"/>
/// if this factory does not handle that connection type.
/// </summary>
/// <param name="connection">The open connection to copy into.</param>
/// <param name="copyCommand">The provider-specific copy command, for example
/// <c>COPY "public"."products" FROM STDIN WITH (FORMAT csv, ...)</c>.</param>
public delegate ICopyImportWriter? CopyImportFactory(IDbConnection connection, string copyCommand);
