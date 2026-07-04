using System.Reflection;

using Xunit.Sdk;
using Xunit.v3;

namespace Jaunty.Tests.Helpers.Dialects;

public abstract class DialectDataAttributeBase : DataAttribute
{
    private readonly string _skipMessage;

    protected DialectDataAttributeBase(string skipMessage) => _skipMessage = skipMessage;

    protected abstract bool IsAvailable { get; }

    protected abstract DialectInfo Dialect { get; }

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        // Always return one data row so theories never fail discovery with "no data";
        // unavailable providers are reported as skipped via ApplySkipIfUnavailable().
        IReadOnlyCollection<ITheoryDataRow> rows = new ITheoryDataRow[] { new TheoryDataRow<DialectInfo>(Dialect) };
        return new ValueTask<IReadOnlyCollection<ITheoryDataRow>>(rows);
    }

    // DialectInfo is not xunit-serializable; do not pre-enumerate during discovery.
    public override bool SupportsDiscoveryEnumeration() => false;

    protected void ApplySkipIfUnavailable()
    {
        if (!IsAvailable)
        {
            Skip = _skipMessage;
        }
    }
}
