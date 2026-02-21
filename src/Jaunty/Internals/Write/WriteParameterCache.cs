using System.Data;
using System.Reflection;
using Jaunty.Interfaces;
using Jaunty.Configuration;

namespace Jaunty.Internals.Write;

/// <summary>
/// Caches compiled binders for high-performance write operations.
/// </summary>
internal static class WriteParameterCache<T> where T : new()
{
    public static readonly Action<IDbCommand, T>? InsertBinder;
    public static readonly Action<IDbCommand, T>? UpdateBinder;
    public static readonly Action<IDbCommand, T>? DeleteBinder;

    public static readonly Action<IDataParameterCollection, T>? InsertValueSetter;
    public static readonly Action<IDataParameterCollection, T>? UpdateValueSetter;
    public static readonly Action<IDataParameterCollection, T>? DeleteValueSetter;

    public static readonly Action<T, long>? IdSetter;

    static WriteParameterCache()
    {
        InsertBinder = TryGetGeneratedBinder("BindInsert") ?? TryGetReflectionBinder(JauntyConfig.ReflectionInsertBinderResolver);
        UpdateBinder = TryGetGeneratedBinder("BindUpdate") ?? TryGetReflectionBinder(JauntyConfig.ReflectionUpdateBinderResolver);
        DeleteBinder = TryGetGeneratedBinder("BindDelete") ?? TryGetReflectionBinder(JauntyConfig.ReflectionDeleteBinderResolver);

        // Bridges for Bulk operations
        InsertValueSetter = InsertBinder != null ? (pc, entity) => {
            using var cmd = new BridgeCommand(pc);
            InsertBinder(cmd, entity);
        } : null;

        UpdateValueSetter = UpdateBinder != null ? (pc, entity) => {
            using var cmd = new BridgeCommand(pc);
            UpdateBinder(cmd, entity);
        } : null;

        DeleteValueSetter = DeleteBinder != null ? (pc, entity) => {
            using var cmd = new BridgeCommand(pc);
            DeleteBinder(cmd, entity);
        } : null;

        IdSetter = CreateIdSetter();
    }

    private static Action<IDbCommand, T>? TryGetGeneratedBinder(string methodName)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, [typeof(IDbCommand), typeof(T)], null);
        return (Action<IDbCommand, T>?)method?.CreateDelegate(typeof(Action<IDbCommand, T>));
    }

    private static Action<IDbCommand, T>? TryGetReflectionBinder(Func<Type, Action<IDbCommand, object>>? resolver)
    {
        if (resolver?.Invoke(typeof(T)) is Action<IDbCommand, object> binder)
        {
            return (cmd, entity) => binder(cmd, entity);
        }
        return null;
    }

    private static Action<T, long>? CreateIdSetter()
    {
        if (typeof(IEntity).IsAssignableFrom(typeof(T)))
        {
            return static (target, value) => ((IEntity)target).Id = value;
        }
        return null;
    }

    private class BridgeCommand(IDataParameterCollection parameters) : IDbCommand
    {
        public IDbConnection? Connection { get; set; }
        public IDbTransaction? Transaction { get; set; }
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDataParameterCollection Parameters => parameters;
        public UpdateRowSource UpdatedRowSource { get; set; }
        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new BridgeParameter();
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => null!;
        public IDataReader ExecuteReader(CommandBehavior behavior) => null!;
        public object? ExecuteScalar() => null;
        public void Prepare() { }
        public void Dispose() { }
    }

    private class BridgeParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }
}
