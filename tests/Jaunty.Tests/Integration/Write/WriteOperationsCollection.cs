using Xunit;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Test collection for write operations that require sequential execution.
/// This prevents parallel test execution from causing database isolation issues.
/// 
/// NOTE: Due to xUnit running tests on multiple target frameworks (.NET 8 and .NET Framework 4.72)
/// in separate processes, this collection only serializes tests within a single process.
/// For full serialization, run tests with: dotnet test --framework net8.0
/// </summary>
[CollectionDefinition("Write Operations", DisableParallelization = true)]
public class WriteOperationsCollection
{
}
