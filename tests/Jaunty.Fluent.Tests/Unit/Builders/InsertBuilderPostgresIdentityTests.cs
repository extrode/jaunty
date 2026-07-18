using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// Regression tests for <c>InsertBuilder.BuildInsertWithIdentitySql</c> against the PostgreSQL
/// dialect. <see cref="Jaunty.Dialects.SqlDialectFactory.GetDialect"/> resolves purely on
/// <c>connection.GetType().Name</c>, so a fake <see cref="IDbConnection"/> whose class is simply
/// named <c>NpgsqlConnection</c> (declared below, unrelated to the real Npgsql package) resolves
/// to the real <c>PostgreSqlDialect</c> without requiring the Npgsql package or a live Postgres
/// server. The fake command captures the SQL text handed to <c>ExecuteScalar()</c> /
/// <c>ExecuteNonQuery()</c> so the generated SQL can be asserted directly.
/// </summary>
public class InsertBuilderPostgresIdentityTests
{
    [Fact]
    public void Insert_IdentityEntity_ReturningClauseUsesActualPrimaryKeyColumnName()
    {
        using var connection = new NpgsqlConnection();

        // Product's identity primary key column is "product_id" (mapped via [Key] +
        // [DatabaseGenerated(Identity)] + [Column("product_id")]), not "id". Before the fix,
        // PostgreSqlDialect.GetLastInsertIdSql() was invoked with no arguments and hardcoded
        // "RETURNING id;" regardless of the entity's actual primary key column, which would have
        // produced SQL referencing a column that doesn't exist on the products table.
        var id = connection.Into<Product>()
            .Values(new Product
            {
                ProductName = "Widget",
                SupplierId = 1,
                CategoryId = (short)1,
                QuantityPerUnit = "10 boxes",
                UnitPrice = 9.99m,
                UnitsInStock = (short)10,
                Discontinued = false
            })
            .Insert();

        Assert.Equal(1L, id);
        Assert.True(connection.ExecuteScalarCalled);
        Assert.NotNull(connection.LastCommandText);
        Assert.Contains("RETURNING product_id", connection.LastCommandText);
        Assert.DoesNotContain("RETURNING id", connection.LastCommandText);
    }

    [Fact]
    public void Insert_NonIdentityEntity_DoesNotGoThroughReturningPath()
    {
        using var connection = new NpgsqlConnection();

        // Category's primary key ("category_id") is not identity-generated (no
        // [DatabaseGenerated(Identity)]), so InsertBuilder.HasIdentityColumn() is false and the
        // insert must take the plain ExecuteNonQuery path with no RETURNING clause at all.
        var rowsAffected = connection.Into<Category>()
            .Values(new Category { CategoryId = 99, CategoryName = "TestCategory" })
            .Insert();

        Assert.Equal(1L, rowsAffected);
        Assert.True(connection.ExecuteNonQueryCalled);
        Assert.False(connection.ExecuteScalarCalled);
        Assert.NotNull(connection.LastCommandText);
        Assert.DoesNotContain("RETURNING", connection.LastCommandText);
    }
}

/// <summary>
/// A fake <see cref="IDbConnection"/> whose type name is literally "NpgsqlConnection" so that
/// <see cref="Jaunty.Dialects.SqlDialectFactory"/>'s name-based resolution picks the real
/// PostgreSQL dialect, without any dependency on the actual Npgsql package. Records the SQL text
/// and execution method (scalar vs non-query) used by the most recently executed command.
/// </summary>
internal sealed class NpgsqlConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public string? LastCommandText { get; private set; }
    public bool ExecuteScalarCalled { get; private set; }
    public bool ExecuteNonQueryCalled { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => string.Empty;
    public override string DataSource => string.Empty;
    public override string ServerVersion => string.Empty;
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() => _state = ConnectionState.Closed;
    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new FakeNpgsqlCommand(this);

    internal void RecordScalarExecution(string commandText)
    {
        LastCommandText = commandText;
        ExecuteScalarCalled = true;
    }

    internal void RecordNonQueryExecution(string commandText)
    {
        LastCommandText = commandText;
        ExecuteNonQueryCalled = true;
    }
}

/// <summary>
/// A fake <see cref="DbCommand"/> that does not touch any real database. It only records the
/// <see cref="CommandText"/> handed to it at execution time and returns a dummy scalar (1L) /
/// dummy row count (1), which is all <c>InsertBuilder.ExecuteInsert</c> needs to complete.
/// </summary>
internal sealed class FakeNpgsqlCommand : DbCommand
{
    private readonly NpgsqlConnection _owner;
    private readonly FakeParameterCollection _parameters = new();

    public FakeNpgsqlCommand(NpgsqlConnection owner) => _owner = owner;

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    protected override DbParameter CreateDbParameter() => new FakeParameter();

    public override int ExecuteNonQuery()
    {
        _owner.RecordNonQueryExecution(CommandText);
        return 1;
    }

    public override object? ExecuteScalar()
    {
        _owner.RecordScalarExecution(CommandText);
        return 1L;
    }

    public override void Prepare() { }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => throw new NotSupportedException();
}

/// <summary>
/// A minimal in-memory <see cref="DbParameterCollection"/> backed by a <see cref="List{T}"/>.
/// Only <see cref="Add(object)"/> is exercised by <c>ParameterCollection.BindTo</c>; the
/// remaining members exist solely to satisfy the abstract base class contract.
/// </summary>
internal sealed class FakeParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _items = new();

    public override int Count => _items.Count;
    public override object SyncRoot => this;

    public override int Add(object value)
    {
        _items.Add((DbParameter)value);
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
            Add(value!);
    }

    public override void Clear() => _items.Clear();

    public override bool Contains(object value) => _items.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _items.GetEnumerator();

    protected override DbParameter GetParameter(int index) => _items[index];

    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

    public override bool IsFixedSize => false;
    public override bool IsReadOnly => false;
    public override bool IsSynchronized => false;

    public override void Remove(object value) => _items.Remove((DbParameter)value);

    public override void RemoveAt(int index) => _items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        int idx = IndexOf(parameterName);
        if (idx >= 0)
            _items[idx] = value;
        else
            _items.Add(value);
    }
}

/// <summary>
/// A minimal <see cref="DbParameter"/> that only stores <see cref="ParameterName"/> and
/// <see cref="Value"/>, which is all <c>ParameterCollection.BindTo</c> sets.
/// </summary>
internal sealed class FakeParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override DataRowVersion SourceVersion { get; set; }
    public override object? Value { get; set; }

    public override void ResetDbType() { }
}
