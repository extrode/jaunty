// Namespace preserved from ParameterBinderTests.cs so every existing reference still resolves.
namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// Minimal <see cref="IDbCommand"/> stand-ins for tests that need to inspect the parameters a
/// binder produced without opening a connection.
///
/// <para>
/// These lived at the bottom of <c>Unit/Read/ParameterBinderTests.cs</c>. That file registers
/// dialects and type handlers on the global statics, so it stays in the serial Jaunty.Tests
/// assembly - but twelve of the files that moved to the parallel Jaunty.UnitTests use these mocks
/// and nothing else from it. Hoisting them into Helpers, which both projects share by Compile
/// link, keeps one definition rather than a copy per assembly.
/// </para>
/// </summary>
public class MockDbCommand : IDbCommand
{
    public MockDbCommand(string commandText)
    {
        CommandText = commandText;
        Parameters = new MockDataParameterCollection();
    }

    public string CommandText { get; set; }
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; }
    public MockDataParameterCollection Parameters { get; }
    IDataParameterCollection IDbCommand.Parameters => Parameters;
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }
    public IDbDataParameter CreateParameter() => new MockDbDataParameter();
    public void Dispose() { }
    public int ExecuteNonQuery() => 0;
    public IDataReader ExecuteReader() => throw new NotImplementedException();
    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();
    public object? ExecuteScalar() => null;
    public void Prepare() { }
}

public class MockDataParameterCollection : List<MockDbDataParameter>, IDataParameterCollection
{
    public bool Contains(string parameterName) => this.Any(p => p.ParameterName == parameterName);
    public int IndexOf(string parameterName) => FindIndex(p => p.ParameterName == parameterName);
    public void RemoveAt(string parameterName) => RemoveAll(p => p.ParameterName == parameterName);
    public object this[string parameterName]
    {
        get => this.First(p => p.ParameterName == parameterName);
        set => throw new NotImplementedException();
    }
}

public class MockDbDataParameter : IDbDataParameter
{
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; }
    public bool IsNullable => true;
    public string ParameterName { get; set; } = "";
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
    public string SourceColumn { get; set; } = "";
    public DataRowVersion SourceVersion { get; set; }
    public object? Value { get; set; }
}
