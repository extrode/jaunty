using System.Data;

using Jaunty.Configuration;

namespace Jaunty.Internals.Read;

/// <summary>
/// Hook for multi-entity mapping. Actual implementation resides in extensions or source-gen.
/// </summary>
internal sealed class MultiEntityMapper<T1, T2> where T1 : new() where T2 : new()
{
    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
    }

    internal static MultiEntityMapper<T1, T2> Build(IDataReader reader)
    {
        if (JauntyConfig.ReflectionMultiMapperResolver?.Invoke(typeof(T1), typeof(T2)) is Action<T1, T2, IDataRecord> combined)
        {
             // This is a simplified bridge. The extension will provide the actual logic.
             return new MultiEntityMapper<T1, T2>(
                 (t1, r) => combined(t1, default!, r),
                 (t2, r) => combined(default!, t2, r)
             );
        }

        throw new InvalidOperationException(
            $"No multi-mapper found for types '{typeof(T1).Name}' and '{typeof(T2).Name}'. " +
            "Ensure 'Jaunty.Extensions.Reflection' is loaded for runtime multi-mapping.");
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
}
