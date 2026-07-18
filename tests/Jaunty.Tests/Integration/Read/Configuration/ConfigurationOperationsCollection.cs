using Xunit;

namespace Jaunty.Tests.Integration.Read.Configuration;

/// <summary>
/// Test collection for tests that mutate the process-wide static
/// <see cref="Jaunty.Configuration.JauntyConfig.ColumnNameResolver"/> /
/// <see cref="Jaunty.Configuration.JauntyConfig.TableNameResolver"/>.
/// This prevents parallel test execution from letting another test class
/// observe the mutated global resolver mid-test.
/// </summary>
[CollectionDefinition("Configuration Operations", DisableParallelization = true)]
public class ConfigurationOperationsCollection
{
}
