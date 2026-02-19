using System.Data;
using System.Reflection;

using Jaunty.Interfaces;
using Jaunty.Internals.Entity;

namespace Jaunty.Internals.Write;

/// <summary>
/// Caches compiled column metadata and binders for high-performance write operations.
/// </summary>
internal static class WriteParameterCache<T> where T : class
{
    private static readonly WriteColumnContext<T>[] _insertColumns;
    private static readonly WriteColumnContext<T>[] _updateSetColumns;
    private static readonly WriteColumnContext<T>[] _updateKeyColumns;

    public static readonly Action<IDbCommand, T> InsertBinder;
    public static readonly Action<IDbCommand, T> UpdateBinder;
    public static readonly Action<IDbCommand, T> DeleteBinder;
    
    public static readonly Action<IDataParameterCollection, T> InsertValueSetter;
    public static readonly Action<IDataParameterCollection, T> UpdateValueSetter;
    public static readonly Action<IDataParameterCollection, T> DeleteValueSetter;

    public static readonly Action<T, long>? IdSetter;

    static WriteParameterCache()
    {
        var metadata = MetadataCache<T>.Metadata;
        var properties = MetadataCache<T>.Properties;

        // Build Insert Columns
        var insertList = new List<WriteColumnContext<T>>(metadata.NonIdentityColumns.Count);
        for (int i = 0; i < properties.Length; i++)
        {
            var ctx = properties[i];
            var col = metadata.Columns.FirstOrDefault(c => c.Property == ctx.Property);
            if (col != null && !col.IsIdentity && !col.IsComputed)
            {
                insertList.Add(new WriteColumnContext<T>("@" + ctx.PropertyName, ctx.Getter));
            }
        }
        _insertColumns = [.. insertList];

        // Build Update Set Columns
        var updateSetList = new List<WriteColumnContext<T>>(metadata.Columns.Count);
        for (int i = 0; i < properties.Length; i++)
        {
            var ctx = properties[i];
            var col = metadata.Columns.FirstOrDefault(c => c.Property == ctx.Property);
            if (col != null && !col.IsPrimaryKey && !col.IsIdentity && !col.IsComputed)
            {
                updateSetList.Add(new WriteColumnContext<T>("@" + ctx.PropertyName, ctx.Getter));
            }
        }
        _updateSetColumns = [.. updateSetList];

        // Build Update Key Columns
        var updateKeyList = new List<WriteColumnContext<T>>(metadata.PrimaryKeys.Count);
        for (int i = 0; i < properties.Length; i++)
        {
            var ctx = properties[i];
            var col = metadata.Columns.FirstOrDefault(c => c.Property == ctx.Property);
            if (col != null && col.IsPrimaryKey)
            {
                updateKeyList.Add(new WriteColumnContext<T>("@" + ctx.PropertyName, ctx.Getter));
            }
        }
        _updateKeyColumns = [.. updateKeyList];

        InsertBinder = CreateInsertBinder();
        UpdateBinder = CreateUpdateBinder();
        DeleteBinder = CreateDeleteBinder();

        InsertValueSetter = CreateInsertValueSetter();
        UpdateValueSetter = CreateUpdateValueSetter();
        DeleteValueSetter = CreateDeleteValueSetter();

        IdSetter = CreateIdSetter();
    }

    private static Action<IDbCommand, T> CreateInsertBinder()
    {
        if (_insertColumns.Length == 0)
            return static (_, _) => { };

        return static (cmd, entity) =>
        {
            var parameters = cmd.Parameters;
            for (int i = 0; i < _insertColumns.Length; i++)
            {
                ref readonly var col = ref _insertColumns[i];
                var p = cmd.CreateParameter();
                p.ParameterName = col.ParameterName;
                p.Value = col.Getter(entity) ?? DBNull.Value;
                parameters.Add(p);
            }
        };
    }

    private static Action<IDataParameterCollection, T> CreateInsertValueSetter()
    {
        if (_insertColumns.Length == 0)
            return static (_, _) => { };

        return static (paramsCollection, entity) =>
        {
            for (int i = 0; i < _insertColumns.Length; i++)
            {
                ref readonly var col = ref _insertColumns[i];
                ((IDataParameter)paramsCollection[i]!).Value = col.Getter(entity) ?? DBNull.Value;
            }
        };
    }

    private static Action<IDbCommand, T> CreateUpdateBinder()
    {
        if (_updateSetColumns.Length == 0 && _updateKeyColumns.Length == 0)
            return static (_, _) => { };

        return static (cmd, entity) =>
        {
            var parameters = cmd.Parameters;
            
            // SET clause
            for (int i = 0; i < _updateSetColumns.Length; i++)
            {
                ref readonly var col = ref _updateSetColumns[i];
                var p = cmd.CreateParameter();
                p.ParameterName = col.ParameterName;
                p.Value = col.Getter(entity) ?? DBNull.Value;
                parameters.Add(p);
            }

            // WHERE clause
            for (int i = 0; i < _updateKeyColumns.Length; i++)
            {
                ref readonly var col = ref _updateKeyColumns[i];
                var p = cmd.CreateParameter();
                p.ParameterName = col.ParameterName;
                p.Value = col.Getter(entity) ?? DBNull.Value;
                parameters.Add(p);
            }
        };
    }

    private static Action<IDataParameterCollection, T> CreateUpdateValueSetter()
    {
        if (_updateSetColumns.Length == 0 && _updateKeyColumns.Length == 0)
            return static (_, _) => { };

        return static (paramsCollection, entity) =>
        {
            int index = 0;
            for (int i = 0; i < _updateSetColumns.Length; i++)
            {
                ref readonly var col = ref _updateSetColumns[i];
                ((IDataParameter)paramsCollection[index++]!).Value = col.Getter(entity) ?? DBNull.Value;
            }
            for (int i = 0; i < _updateKeyColumns.Length; i++)
            {
                ref readonly var col = ref _updateKeyColumns[i];
                ((IDataParameter)paramsCollection[index++]!).Value = col.Getter(entity) ?? DBNull.Value;
            }
        };
    }

    private static Action<IDbCommand, T> CreateDeleteBinder()
    {
        if (_updateKeyColumns.Length == 0)
            return static (_, _) => { };

        return static (cmd, entity) =>
        {
            var parameters = cmd.Parameters;
            for (int i = 0; i < _updateKeyColumns.Length; i++)
            {
                ref readonly var col = ref _updateKeyColumns[i];
                var p = cmd.CreateParameter();
                p.ParameterName = col.ParameterName;
                p.Value = col.Getter(entity) ?? DBNull.Value;
                parameters.Add(p);
            }
        };
    }

    private static Action<IDataParameterCollection, T> CreateDeleteValueSetter()
    {
        if (_updateKeyColumns.Length == 0)
            return static (_, _) => { };

        return static (paramsCollection, entity) =>
        {
            for (int i = 0; i < _updateKeyColumns.Length; i++)
            {
                ref readonly var col = ref _updateKeyColumns[i];
                ((IDataParameter)paramsCollection[i]!).Value = col.Getter(entity) ?? DBNull.Value;
            }
        };
    }

    private static Action<T, long>? CreateIdSetter()
    {
        var type = typeof(T);
        
        if (typeof(IEntity).IsAssignableFrom(type))
        {
            return static (target, value) =>
            {
                ((IEntity)target).Id = value;
            };
        }

        return CreateComplexIdSetter();
    }

    private static Action<T, long>? CreateComplexIdSetter()
    {
        var type = typeof(T);
        var target = System.Linq.Expressions.Expression.Parameter(type, "target");
        var value = System.Linq.Expressions.Expression.Parameter(typeof(long), "value");

        var iEntityGeneric = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>));
        if (iEntityGeneric != null)
        {
            var idType = iEntityGeneric.GetGenericArguments()[0];
            var property = type.GetProperty("Id")!;
            var convertedValue = System.Linq.Expressions.Expression.Convert(value, idType);
            var assign = System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Property(target, property), convertedValue);
            return System.Linq.Expressions.Expression.Lambda<Action<T, long>>(assign, target, value).Compile();
        }

        var idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp != null && idProp.CanWrite)
        {
            var idType = idProp.PropertyType;
            try
            {
                var convertedValue = System.Linq.Expressions.Expression.Convert(value, idType);
                var assign = System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Property(target, idProp), convertedValue);
                return System.Linq.Expressions.Expression.Lambda<Action<T, long>>(assign, target, value).Compile();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}

internal readonly struct WriteColumnContext<T>(string parameterName, Func<T, object?> getter)
{
    public readonly string ParameterName = parameterName;
    public readonly Func<T, object?> Getter = getter;
}
