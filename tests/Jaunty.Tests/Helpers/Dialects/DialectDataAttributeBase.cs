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
        yield return !IsAvailable ? throw SkipException.ForSkip(_skipMessage) : (new object[] { Dialect });
    }

    protected void ApplySkipIfUnavailable()
    {
        if (!IsAvailable)
        {
            Skip = _skipMessage;
        }
    }
}
