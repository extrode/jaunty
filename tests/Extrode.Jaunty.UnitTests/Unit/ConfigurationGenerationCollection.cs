using Xunit;

namespace Extrode.Jaunty.Tests.Unit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConfigurationGenerationCollection
{
    public const string Name = "Configuration generation";
}
