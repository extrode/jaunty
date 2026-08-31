using System.Data;
using System.Linq;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Interfaces;
using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R30: the generated <c>BindInsert</c>/<c>BindUpdate</c>/<c>BindDelete</c> handed the raw
/// boxed enum to <see cref="IDbDataParameter.Value"/> with no <see cref="EnumStorageAttribute"/> /
/// <see cref="JauntyConfig.DefaultEnumStorage"/> conversion, while the generated READ path parses
/// string-stored enums and the reflection binder converts on write - so a string-stored enum
/// entity read correctly and wrote its numeric value back. These execute the generated binders
/// against a stub command and pin the write-side conversion.
/// </summary>
public sealed class GeneratedEnumWriteTests
{
    [Fact]
    public void BindInsert_ExplicitStringStorage_WritesTheName()
    {
        var command = new StubCommand();

        GenTicket.BindInsert(command, new GenTicket { StateString = GenTicketState.Closed });

        Assert.Contains(command.Bound, p => p.ParameterName == "@state_string" && Equals(p.Value, "Closed"));
    }

    [Fact]
    public void BindInsert_ExplicitNumericStorage_WritesTheEnumValue()
    {
        var command = new StubCommand();

        GenTicket.BindInsert(command, new GenTicket { StateNumeric = GenTicketState.Closed });

        Assert.Contains(command.Bound, p => p.ParameterName == "@state_numeric" && Equals(p.Value, GenTicketState.Closed));
    }

    [Fact]
    public void BindInsert_NoAttribute_HonoursDefaultEnumStorageAtBindTime()
    {
        var command = new StubCommand();
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;
            GenTicket.BindInsert(command, new GenTicket { StateDefault = GenTicketState.Open });
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }

        Assert.Contains(command.Bound, p => p.ParameterName == "@state_default" && Equals(p.Value, "Open"));
    }

    [Fact]
    public void BindInsert_NullableEnum_NullStaysDbNull()
    {
        var command = new StubCommand();

        GenTicket.BindInsert(command, new GenTicket { StateNullable = null });

        Assert.Contains(command.Bound, p => p.ParameterName == "@state_nullable" && Equals(p.Value, DBNull.Value));
    }

    [Theory]
    [InlineData("state_string", EnumStorage.String)]
    [InlineData("state_numeric", EnumStorage.Numeric)]
    [InlineData("state_nullable", EnumStorage.String)]
    public void EntityColumns_ExplicitStorage_CarriesTheOverride(string columnName, EnumStorage expected)
    {
        EntityColumnInfo column = GenTicket.EntityColumns.Single(c => c.ColumnName == columnName);

        Assert.Equal(expected, column.EnumStorageOverride);
    }

    [Fact]
    public void EntityColumns_NoAttribute_CarriesNoOverride()
    {
        EntityColumnInfo column = GenTicket.EntityColumns.Single(c => c.ColumnName == "state_default");

        Assert.Null(column.EnumStorageOverride);
    }

    [Fact]
    public void EntityColumns_NonEnumColumn_CarriesNoOverride()
    {
        EntityColumnInfo column = GenTicket.EntityColumns.Single(c => c.ColumnName == "ticket_id");

        Assert.Null(column.EnumStorageOverride);
    }

    [Fact]
    public void BindInsert_NullableEnum_ValueConvertsLikeItsNonNullableSibling()
    {
        var command = new StubCommand();

        GenTicket.BindInsert(command, new GenTicket { StateNullable = GenTicketState.Open });

        Assert.Contains(command.Bound, p => p.ParameterName == "@state_nullable" && Equals(p.Value, "Open"));
    }

    [Fact]
    public void BindUpdate_AppliesTheSameConversionAsBindInsert()
    {
        var command = new StubCommand();

        GenTicket.BindUpdate(command, new GenTicket { TicketId = 7, StateString = GenTicketState.Open });

        Assert.Contains(command.Bound, p => p.ParameterName == "@state_string" && Equals(p.Value, "Open"));
        Assert.Contains(command.Bound, p => p.ParameterName == "@ticket_id" && Equals(p.Value, 7));
    }

    private sealed class StubParameter : IDbDataParameter
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

    private sealed class StubCommand : IDbCommand
    {
        public List<StubParameter> Bound { get; } = [];

        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; }
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public StubCommand() => Parameters = new Collection(Bound);

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new StubParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }

        private sealed class Collection(List<StubParameter> bound) : List<object>, IDataParameterCollection
        {
            public object this[string parameterName]
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }

            public new int Add(object value)
            {
                if (value is StubParameter parameter)
                    bound.Add(parameter);

                base.Add(value);
                return Count - 1;
            }
        }
    }
}
