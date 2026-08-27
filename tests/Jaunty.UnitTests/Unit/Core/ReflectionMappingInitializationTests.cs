using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// AUD-R26-055 (batch 4, low/consistency). <c>Jaunty.Init.TryEnableReflectionMapping</c>'s
/// catch-all swallowed every unanticipated exception with the note that they were "silently ignored
/// to maintain backward compatibility". The narrow first catch is correct - the extension assembly
/// being absent or trimmed is the supported source-gen-only configuration - but the catch-all is
/// not: if <c>UseReflectionMapping</c> itself throws, reflection mapping is off and every later
/// query fails with "No mapper found for type 'X'", with nothing connecting the two.
///
/// <para>
/// The failure is now recorded on <c>Jaunty.ReflectionMappingInitializationError</c> without
/// changing the no-throw contract a static constructor needs. This test is the canary for the
/// healthy case: the suite runs with Jaunty.Extensions.Reflection present, so a non-null value here
/// means startup broke in a way that would otherwise have surfaced only as unrelated mapper errors
/// much later.
/// </para>
/// </summary>
public class ReflectionMappingInitializationTests
{
    [Fact]
    public void NoInitializationErrorIsRecordedInAHealthyConfiguration()
    {
        // Touching any member forces the static constructor, which is what runs the initialization.
        Exception? error = Jaunty.ReflectionMappingInitializationError;

        Assert.True(
            error is null,
            $"Reflection mapping failed to initialize: {error?.GetType().Name}: {error?.Message}");
    }
}
