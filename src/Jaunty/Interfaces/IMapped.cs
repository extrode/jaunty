using System.Data;

namespace Jaunty.Interfaces
{
    public interface IMapped<T> where T : IMapped<T>, new()
    {
#if NET8_0_OR_GREATER
    static abstract T ReadEntity(IDataReader reader, ReadOnlySpan<int> ordinals);
    static abstract ReadOnlySpan<string> ColumnNames { get; }
#else
        T ReadEntity(IDataReader reader, int[] ordinals);
        string[] GetColumnNames();
#endif
    }
}
