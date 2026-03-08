using System.Reflection;

using Xunit.Sdk;

namespace Jaunty.Tests.Helpers.Dialects;

public abstract class DialectDataAttributeBase : DataAttribute
{
    private readonly string _skipMessage;

    protected DialectDataAttributeBase(string skipMessage) => _skipMessage = skipMessage;

    protected abstract bool IsAvailable { get; }

    protected abstract DialectInfo Dialect { get; }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (!IsAvailable)
        {
            // Return one data row to avoid "No data found" failures in theory discovery.
            // ApplySkipIfUnavailable() marks the case as skipped without dynamic skip exceptions.
            yield return new object[] { Dialect };
            yield break;
        }

        yield return new object[] { Dialect };
    }

    protected void ApplySkipIfUnavailable()
    {
        if (!IsAvailable)
        {
            Skip = _skipMessage;
        }
    }
}