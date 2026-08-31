using Xunit;

namespace Jaunty.Tests.Unit.Internals;

// BulkCopyDialectFactory.Enable() is a one-way, process-wide static switch. Tests that call it
// (BulkCopyDialectFactoryTests) reset it in a finally block, but without serialization a
// concurrently-running SqlDialectFactoryTests test could still observe it mid-flight and cache a
// wrapped dialect permanently. Both classes join this collection so xUnit never runs them at the
// same time.
[CollectionDefinition("Dialect Factory State", DisableParallelization = true)]
public class DialectFactoryStateCollection
{
}
