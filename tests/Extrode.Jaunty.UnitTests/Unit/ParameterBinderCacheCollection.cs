using Xunit;

namespace Extrode.Jaunty.Tests.Unit;

/// <summary>
/// Tests that clear ParameterBinder's or ParameterCache's process-wide caches run alone, so no
/// concurrently running test has its cached shapes wiped mid-test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ParameterBinderCacheCollection
{
    public const string Name = "Parameter binder caches";
}
