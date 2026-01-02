using System.Data;

namespace Jaunty.Internal.Mapping;

internal readonly struct ColumnSetter<T>(int ordinal, Action<T, IDataReader> set)
{
    public readonly int Ordinal = ordinal;
    public readonly Action<T, IDataReader> Set = set;
}
