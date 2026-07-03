using System.Reflection;

namespace Jaunty.Internals.Parameters;

internal readonly struct ParameterMetadata(string name, Func<object, object?> getter, PropertyInfo? property = null)
{
    public readonly string Name = name;
    public readonly Func<object, object?> Getter = getter;
    public readonly PropertyInfo? Property = property;
}
