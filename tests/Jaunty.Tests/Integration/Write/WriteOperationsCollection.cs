using Xunit;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Test collection for write operations that require sequential execution.
/// This prevents parallel test execution from causing database isolation issues.
/// </summary>
[CollectionDefinition("Write Operations", DisableParallelization = true)]
public class WriteOperationsCollection
{
}
