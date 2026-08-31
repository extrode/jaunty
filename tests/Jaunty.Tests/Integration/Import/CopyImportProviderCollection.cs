using Xunit;

namespace Jaunty.Tests.Integration.Import;

// JauntyConfig.CopyImportFactory is process-wide mutable state. Tests that install one restore the
// previous value, but without serialization a concurrently-running import test could observe the
// fake factory and take the client-side COPY path against a real connection. Every class that
// touches the factory joins this collection so xUnit never runs them at the same time.
[CollectionDefinition("Copy Import Provider", DisableParallelization = true)]
public class CopyImportProviderCollection
{
}
