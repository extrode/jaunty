using System.Reflection;

using Xunit.Sdk;
using Xunit.v3;

namespace Jaunty.Tests.Helpers.Dialects;

public abstract class DialectDataAttributeBase : DataAttribute
{
    private readonly string _skipMessage;

    protected DialectDataAttributeBase(string skipMessage) => _skipMessage = skipMessage;

    /// <summary>Whether a connection string has been configured for this dialect.</summary>
    protected abstract bool IsAvailable { get; }

    protected abstract DialectInfo Dialect { get; }

    /// <summary>
    /// Whether the engine answered. Defaults to <see langword="true"/> for the dialects that need
    /// no server; the three server dialects override it with a cached probe.
    /// </summary>
    protected virtual bool IsReachable => true;

    /// <summary>
    /// Why the engine did not answer, when <see cref="IsReachable"/> is <see langword="false"/>.
    /// </summary>
    protected virtual string? UnreachableReason => null;

    /// <summary>
    /// The environment variable that turns "this engine is missing" from a skip into a failure,
    /// or <see langword="null"/> for a dialect that is always present.
    /// </summary>
    protected virtual string? RequireVariable => null;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        // Always return one data row so theories never fail discovery with "no data";
        // unavailable providers are reported as skipped via ApplySkipIfUnavailable().
        IReadOnlyCollection<ITheoryDataRow> rows = new ITheoryDataRow[] { new TheoryDataRow<DialectInfo>(Dialect) };
        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(rows);
    }

    // DialectInfo is not xunit-serializable; do not pre-enumerate during discovery.
    public override bool SupportsDiscoveryEnumeration() => false;

    /// <summary>
    /// Decides what an absent engine means: skipped by default, failed when CI has declared the
    /// engine required.
    /// </summary>
    /// <remarks>
    /// Two separate absences, deliberately kept apart in the skip message. "Not configured" is a
    /// missing connection string and was always a skip. "Not reachable" is a configured engine
    /// that did not answer, which used to run the test and fail on connect - 800 such failures on
    /// a developer box with the local SQL Server service stopped, not one of them a defect.
    /// <para>
    /// The require switch exists because the safe default runs the wrong way in CI: a service
    /// container that failed to start would skip silently and report a green leg that tested
    /// nothing. With <see cref="RequireVariable"/> set, this method declines to skip, the row runs,
    /// and the test fails on connect exactly as it did before any of this existed.
    /// </para>
    /// <para>
    /// Declining to skip, rather than throwing a better-worded exception from
    /// <see cref="GetData"/>. Measured both ways on <c>Integration/Get/GetTests</c> against an
    /// unreachable server: the throw reports 11 failures and a total of 11, because a
    /// <see cref="DataAttribute"/> that throws fails the whole test method and takes every other
    /// dialect's row down with it. Declining to skip reports the same 11 failures alongside the 22
    /// passing SQLite rows. A red leg that still says whether anything else broke is worth more
    /// than a tidier message.
    /// </para>
    /// </remarks>
    protected void ApplySkipIfUnavailable()
    {
        bool configured = IsAvailable;
        bool reachable = configured && IsReachable;

        if (configured && reachable)
            return;

        if (RequireVariable is not null && DialectReachability.IsRequired(RequireVariable))
            return;

        Skip = configured
            ? $"{Dialect.Name} is configured but not reachable: {UnreachableReason}"
            : _skipMessage;
    }
}
