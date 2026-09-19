using System.Data;

using Extrode.Jaunty.Internals.Parameters;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Internals;

public class ParameterBinderStoredProcedureDictionaryTests
{
    [Fact]
    public void Bind_StoredProcedure_Dictionary_BindsEveryKeyWithoutParsingSql()
    {
        var command = new FakeCommand { CommandType = CommandType.StoredProcedure, CommandText = "GetProduct" };
        var parameters = new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "x" };

        ParameterBinder.Bind(command, parameters);

        Assert.Equal(2, command.Parameters.Count);

        var names = new List<string>();
        foreach (object? p in command.Parameters)
            names.Add(((IDbDataParameter)p!).ParameterName);

        Assert.Contains("Id", names);
        Assert.Contains("Name", names);
    }

    private sealed class FakeCommand : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new FakeParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }

    private sealed class FakeParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class FakeParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
    }
}
