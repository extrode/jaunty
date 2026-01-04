using System.Data;
using System.Reflection;

using Jaunty.Entity;
using Jaunty.Enums;

namespace Jaunty;

internal static class DrDispatcher
{
    public static Func<IDataReader, T> Resolve<T>(IDataReader reader, Func<IDataReader, T>? userMapper, MappingMode mode) where T : new()
    {
        // 1. User override
        if (userMapper is not null)
            return userMapper;

        // 2. IMapped<T> Interface (Optimized Dispatch)
        if (DispatcherCache<T>.IsMapped)
        {
            var ordinals = GetOrdinals(reader, DispatcherCache<T>.ColumnNames!);

#if NET8_0_OR_GREATER
            // Net8: Static Abstract Delegate
            return r => DispatcherCache<T>.ReadDelegate!(r, ordinals);
#else
            // Legacy: Open Instance Delegate (No casting required)
            // We capture the delegate from cache, so this is just a func call
            return r => DispatcherCache<T>.LegacyReadDelegate!(new T(), r, ordinals);
#endif
        }

        // 3. Metadata reflection fallback
        var setters = MetadataCache<T>.GetSetters(reader, mode);

        return reader =>
        {
            var entity = new T();
#if NET8_0_OR_GREATER
            ReadOnlySpan<PropertySetter<T>> localSetters = setters;
            foreach (ref readonly var setter in localSetters)
                setter.Set(entity, reader);
#else
            for (int i = 0; i < setters.Length; i++)
                setters[i].Set(entity, reader);
#endif
            return entity;
        };
    }

    private static int[] GetOrdinals(IDataReader reader, string[] columnNames)
    {
        var ordinals = new int[columnNames.Length];
        for (int i = 0; i < columnNames.Length; i++)
        {
            try { ordinals[i] = reader.GetOrdinal(columnNames[i]); }
            catch { ordinals[i] = -1; }
        }
        return ordinals;
    }

    private static class DispatcherCache<T> where T : new()
    {
        public static readonly bool IsMapped;
        public static readonly string[]? ColumnNames;

#if NET8_0_OR_GREATER
        public delegate T ReadEntityDelegate(IDataReader reader, ReadOnlySpan<int> ordinals);
        public static readonly ReadEntityDelegate? ReadDelegate;
#else
        // Open delegate signature: (Instance, Reader, Ordinals) -> T
        public static readonly Func<T, IDataReader, int[], T>? LegacyReadDelegate;
#endif

        static DispatcherCache()
        {
            var type = typeof(T);
            // Use string "IMapped`1" to avoid compile-time constraint checks on typeof(IMapped<>)
            var interfaceType = type.GetInterface("IMapped`1");

            if (interfaceType != null && interfaceType.GenericTypeArguments[0] == type)
            {
                IsMapped = true;

                // Fix CS0314/CS0305: Use string literals instead of nameof(IMapped<T>...)

#if NET8_0_OR_GREATER
                // 1. ColumnNames
                var propInfo = type.GetProperty("ColumnNames", BindingFlags.Public | BindingFlags.Static);
                if (propInfo != null)
                {
                    // Fix CS0457: Cast object to string[] first
                    var array = (string[])propInfo.GetValue(null)!;
                    ColumnNames = array;
                }

                // 2. ReadEntity
                var methodInfo = type.GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static);
                if (methodInfo != null)
                {
                    ReadDelegate = (ReadEntityDelegate)Delegate.CreateDelegate(typeof(ReadEntityDelegate), methodInfo);
                }
#else
                // Legacy Path

                // 1. ColumnNames (Instance method in legacy interface)
                // We create a temp instance just once to get the names
                var getColsMethod = type.GetMethod("GetColumnNames");
                if (getColsMethod != null)
                {
                    ColumnNames = (string[])getColsMethod.Invoke(new T(), null);
                }

                // 2. ReadEntity (Instance method)
                // We map the interface method to the implementation on T to create an open delegate
                var interfaceMethod = interfaceType.GetMethod("ReadEntity");
                if (interfaceMethod != null)
                {
                    // Map interface method to actual implementation on T (handles explicit implementation too)
                    var map = type.GetInterfaceMap(interfaceType);
                    var methodIndex = Array.IndexOf(map.InterfaceMethods, interfaceMethod);
                    var implMethod = map.TargetMethods[methodIndex];

                    LegacyReadDelegate = (Func<T, IDataReader, int[], T>)
                        Delegate.CreateDelegate(typeof(Func<T, IDataReader, int[], T>), implMethod);
                }
#endif
            }
            else
            {
                IsMapped = false;
                ColumnNames = null;
            }
        }
    }
}