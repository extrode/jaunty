namespace Extrode.Jaunty.FlatFiles.Tests.Internals;

/// <summary>
/// The naming resolvers on <c>JauntyConfig</c> are process-wide, and xUnit runs test classes in
/// parallel, so a class that sets one would rename every other class's tables mid-run. A class that
/// mutates <c>JauntyConfig</c> joins this collection, which runs on its own.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalConfigStateCollection
{
    public const string Name = "jaunty-global-config-state";
}
