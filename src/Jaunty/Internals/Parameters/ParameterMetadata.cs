namespace Jaunty.Internals.Parameters;

internal readonly struct ParameterMetadata(string name, Func<object, object?> getter)
{
    public readonly string Name = name;
    public readonly Func<object, object?> Getter = getter;
}